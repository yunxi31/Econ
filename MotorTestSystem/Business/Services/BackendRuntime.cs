using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using MotorTestSystem.Models;
using MotorTestSystem.Models.Entities;

namespace MotorTestSystem.Services
{
    public sealed class BackendRuntime : IDisposable
    {
        private static readonly Random _rng = new(42);
        private readonly INotificationService _notificationService;

        private static readonly Lazy<Task<BackendRuntime>> _sharedInstanceTask = new Lazy<Task<BackendRuntime>>(() => Task.Run(CreateDefaultAsync));

        public static Task<BackendRuntime> GetSharedAsync() => _sharedInstanceTask.Value;

        public SqlSugarDbContext DbContext { get; }
        public ObservableCollection<StationConfig> StationConfigs { get; }
        public IMotorTestRepository Repository { get; }
        public IUserService UserService { get; }
        public IAuthService AuthService { get; }
        public PlcPollingService PollingService { get; }
        public HikvisionSdkService HikvisionService { get; }
        public INotificationService NotificationService => _notificationService;
        public EventChannelService EventChannel { get; }
        public BatchWriteService BatchWriter { get; }
        public IDeadLetterQueue? DeadLetterQueue { get; private set; }
        public NotificationWriter? NotificationWriterService { get; private set; }
        public CloudSyncService? CloudSync { get; private set; }

        private readonly System.Collections.Generic.Dictionary<string, bool> _stationOnlineState = new();
        private readonly System.Collections.Generic.Dictionary<string, DateTime> _lastNgAlertTime = new();
        private static readonly TimeSpan NgAlertCooldown = TimeSpan.FromMinutes(5);

        public BackendRuntime(
            ObservableCollection<StationConfig> stationConfigs,
            SqlSugarDbContext dbContext,
            IMotorTestRepository repository,
            IPlcClientFactory plcClientFactory,
            IUserService userService,
            IAuthService authService,
            INotificationService notificationService,
            EventChannelService eventChannel,
            HikvisionSdkService? hikvisionService = null,
            IDeadLetterQueue? deadLetterQueue = null,
            NotificationWriter? notificationWriter = null)
        {
            StationConfigs = stationConfigs;
            DbContext = dbContext;
            Repository = repository;
            UserService = userService;
            AuthService = authService;
            _notificationService = notificationService;
            EventChannel = eventChannel;
            DeadLetterQueue = deadLetterQueue;
            NotificationWriterService = notificationWriter;

            PollingService = new PlcPollingService(StationConfigs, Repository, plcClientFactory, eventChannel: eventChannel);
            BatchWriter = new BatchWriteService(Repository, eventChannel.WriteReader, deadLetterQueue, eventChannel);

            PollingService.SnapshotReceived += OnSnapshotReceivedForNotification;
            PollingService.LogReceived += OnLogReceivedForNotification;
        }

        private void OnSnapshotReceivedForNotification(object? sender, StationSnapshot snapshot)
        {
            bool wasOnline = _stationOnlineState.TryGetValue(snapshot.StationId, out var prev) && prev;
            bool isNowOnline = snapshot.IsOnline;

            if (wasOnline && !isNowOnline)
            {
                var config = StationConfigs.FirstOrDefault(c => c.Id == snapshot.StationId);
                string plcModel = config?.PlcModel ?? "未知型号";
                _notificationService.Add(new NotificationItem
                {
                    Title = $"{snapshot.StationId}工位通信中断",
                    Content = $"工位 {snapshot.StationId}(PLC型号:{plcModel})与上位机失去连接，当前状态：离线。请检查网络连接及PLC电源状态。",
                    Type = NotificationType.Alarm,
                    Severity = NotificationSeverity.Critical,
                    Source = snapshot.StationId
                });
            }

            _stationOnlineState[snapshot.StationId] = isNowOnline;

            if (snapshot.CompletionSignal && snapshot.CompletedData != null
                && snapshot.CompletedData.Result == "NG")
            {
                string barcode = snapshot.CompletedData.Barcode;
                string stage = snapshot.CompletedData.Stage.ToString();

                if (!_lastNgAlertTime.TryGetValue(barcode, out var lastTime)
                    || DateTime.Now - lastTime > NgAlertCooldown)
                {
                    _lastNgAlertTime[barcode] = DateTime.Now;

                    string reason = GetNgReason(snapshot.CompletedData);
                    _notificationService.Add(new NotificationItem
                    {
                        Title = $"{snapshot.StationId}工位测试不合格",
                        Content = $"电机 [{barcode}] 在{stage}阶段测试未通过。{reason}请及时处理。",
                        Type = NotificationType.Alarm,
                        Severity = NotificationSeverity.Warning,
                        Source = snapshot.StationId
                    });
                }
            }
        }

        private static string GetNgReason(StageTestData data)
        {
            var reasons = new System.Collections.Generic.List<string>();

            if (data.NoLoadCurrent > 2.5) reasons.Add($"空载电流超限({data.NoLoadCurrent:F2}A)");
            if (data.NoLoadSpeed > 2200 || data.NoLoadSpeed < 1800) reasons.Add($"空载转速异常({data.NoLoadSpeed}r/min)");
            if (data.FwdNoise > 70) reasons.Add($"正转噪音超限({data.FwdNoise:F1}dB)");
            if (data.NoiseDiff > 15) reasons.Add($"噪音差值过大({data.NoiseDiff:F1}dB)");
            if (data.LoadCurrent > 3.0) reasons.Add($"负载电流超限({data.LoadCurrent:F2}A)");
            if (data.LoadSpeed < 1000) reasons.Add($"负载转速偏低({data.LoadSpeed}r/min)");

            return reasons.Count > 0
                ? string.Join("；", reasons) + "。"
                : "具体原因待诊断。";
        }

        private void OnLogReceivedForNotification(object? sender, string log)
        {
            if (log.Contains("error", StringComparison.OrdinalIgnoreCase)
                || log.Contains("异常", StringComparison.OrdinalIgnoreCase)
                || log.Contains("故障", StringComparison.OrdinalIgnoreCase))
            {
                _notificationService.Add(new NotificationItem
                {
                    Title = "PLC通信异常",
                    Content = $"PLC轮询服务报告异常：{log}",
                    Type = NotificationType.Alarm,
                    Severity = NotificationSeverity.Warning
                });
            }
        }

        private static async Task<BackendRuntime> CreateDefaultAsync()
        {
            // 加载数据持久化配置
            var persistenceConfig = DataPersistenceConfig.Load();

            var dbContext = new SqlSugarDbContext(persistenceConfig);

            var configEntities = await dbContext.Db.Queryable<StationConfigEntity>().ToListAsync();
            var configs = new ObservableCollection<StationConfig>(
                configEntities.Select(ToStationConfigModel));

            var repository = new SqlMotorTestRepository(dbContext);
            var userService = new SqlSugarUserService(dbContext);
            var authService = new AuthService(userService);
            var notificationService = new SqlSugarNotificationService(dbContext);
            var hikvisionService = new HikvisionSdkService();

            await SeedRepositoryIfEmptyAsync(repository, dbContext);

            // 创建 EventChannelService（使用配置容量）
            var eventChannel = new EventChannelService(writeChannelCapacity: persistenceConfig.WriteChannelCapacity);

            // 创建死信队列
            var deadLetterQueue = new DeadLetterQueue(storagePath: persistenceConfig.DeadLetterQueuePath);

            // 启动时自动补传死信队列
            await RetryDeadLettersOnStartupAsync(deadLetterQueue, repository);

            // 创建通知通道消费者
            var notificationWriter = new NotificationWriter(
                eventChannel.NotificationReader,
                notificationService);

            // 创建云端同步服务（10.3）
            var cloudSync = new CloudSyncService(dbContext)
            {
                IsEnabled = persistenceConfig.CloudSyncEnabled,
                Endpoint = persistenceConfig.CloudSyncEndpoint
            };
            if (cloudSync.IsEnabled)
            {
                await cloudSync.StartAsync();
            }

            return new BackendRuntime(
                configs, dbContext, repository, new PlcClientFactory(useSimulation: false),
                userService, authService, notificationService, eventChannel,
                hikvisionService, deadLetterQueue, notificationWriter)
            { CloudSync = cloudSync };
        }

        /// <summary>
        /// 启动时扫描死信队列目录并尝试补传失败的数据（3.4）。
        /// </summary>
        private static async Task RetryDeadLettersOnStartupAsync(
            IDeadLetterQueue deadLetterQueue,
            IMotorTestRepository repository)
        {
            try
            {
                var entries = await deadLetterQueue.ScanAsync();
                if (entries.Count == 0) return;

                int successCount = 0;
                int failCount = 0;

                foreach (var entry in entries)
                {
                    bool retried = await deadLetterQueue.RetryAsync(entry,
                        async (data, ct) =>
                        {
                            foreach (var item in data)
                            {
                                await repository.UpsertStageResultAsync(item, ct);
                            }
                        });

                    if (retried)
                        successCount++;
                    else
                        failCount++;
                }

                System.Diagnostics.Trace.WriteLine(
                    $"Dead letter retry completed: {successCount} succeeded, {failCount} failed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"Dead letter retry error during startup: {ex.Message}");
            }
        }

        private static async Task SeedRepositoryIfEmptyAsync(IMotorTestRepository repository, SqlSugarDbContext dbContext)
        {
            if (await dbContext.Db.Queryable<MotorTestRecordEntity>().AnyAsync())
                return;

            var now = DateTime.Now;
            int idx = 0;

            for (int weekAgo = 3; weekAgo >= 1; weekAgo--)
            {
                DateTime weekStart = now.Date.AddDays(-weekAgo * 7);
                int weekOk = 500 + _rng.Next(300);
                int weekNg = 8 + _rng.Next(13);
                idx = await SeedWeekDataAsync(repository, weekStart, weekOk, weekNg, idx);
            }

            DateTime thisWeekStart = now.Date.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday);
            for (int dayOffset = 0; dayOffset <= Math.Min((int)now.DayOfWeek, 6); dayOffset++)
            {
                DateTime day = thisWeekStart.AddDays(dayOffset);
                if (day.Date >= now.Date) break;

                bool isWeekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                int dayOk = isWeekend ? 30 + _rng.Next(40) : 80 + _rng.Next(60);
                int dayNg = isWeekend ? 1 + _rng.Next(3) : 2 + _rng.Next(5);

                idx = await SeedDayDataAsync(repository, day, dayOk, dayNg, idx);
            }

            var todayHourly = new (int hoursAgo, int ok, int ng, NgPattern[] patterns)[]
            {
                (7, 45, 3, new[] { NgPattern.NoiseHighDiff, NgPattern.NoLoadHighCurrent, NgPattern.NoiseHighDiff }),
                (6, 52, 2, new[] { NgPattern.NoiseFwdLoud, NgPattern.LoadHighCurrent }),
                (5, 48, 1, new[] { NgPattern.NoLoadHighSpeed }),
                (4, 61, 4, new[] { NgPattern.NoiseHighDiff, NgPattern.NoiseHighDiff, NgPattern.LoadHighCurrent, NgPattern.NoLoadHighCurrent }),
                (3, 38, 2, new[] { NgPattern.LoadLowSpeed, NgPattern.NoiseFwdLoud }),
                (2, 55, 3, new[] { NgPattern.NoiseHighDiff, NgPattern.LoadHighCurrent, NgPattern.NoLoadHighCurrent }),
                (1, 58, 2, new[] { NgPattern.NoiseHighDiff, NgPattern.LoadHighCurrent }),
                (0, 42, 3, new[] { NgPattern.NoiseHighDiff, NgPattern.NoLoadHighSpeed, NgPattern.LoadLowSpeed })
            };

            foreach (var (hoursAgo, okCount, ngCount, patterns) in todayHourly)
            {
                DateTime hourTime = now.AddHours(-hoursAgo);
                for (int i = 0; i < okCount; i++)
                {
                    string barcode = MakeBarcode(idx++);
                    DateTime collectedAt = new DateTime(hourTime.Year, hourTime.Month, hourTime.Day, hourTime.Hour, i % 60, _rng.Next(60));
                    await SeedOkRecordAsync(repository, barcode, collectedAt, i);
                }
                for (int i = 0; i < ngCount; i++)
                {
                    string barcode = MakeBarcode(idx++);
                    DateTime collectedAt = new DateTime(hourTime.Year, hourTime.Month, hourTime.Day, hourTime.Hour, (i * 7 + 3) % 60, _rng.Next(60));
                    var pattern = patterns[i % patterns.Length];
                    await SeedNgRecordAsync(repository, barcode, collectedAt, pattern);
                }
            }
        }

        private static StationConfig ToStationConfigModel(StationConfigEntity entity) => new()
        {
            Id = entity.Id, Name = entity.Name, PlcModel = entity.PlcModel,
            IpAddress = entity.IpAddress, Port = entity.Port,
            Protocol = entity.Protocol, StationId = (byte)entity.StationId,
            IsConnected = entity.IsConnected, Status = entity.Status
        };

        private static string MakeBarcode(int index) => $"DES-SR-150GEN{1992900399000 + index}";

        private enum NgPattern
        {
            NoLoadHighCurrent, NoLoadHighSpeed, NoiseHighDiff,
            NoiseFwdLoud, LoadHighCurrent, LoadLowSpeed,
        }

        private static async Task SeedOkRecordAsync(IMotorTestRepository repo, string barcode, DateTime time, int variant)
        {
            await repo.UpsertStageResultAsync(new StageTestData
            {
                Barcode = barcode, StationId = "A1", Stage = TestStage.NoLoad, CollectedAt = time, Result = "OK",
                NoLoadCurrent = 1.80 + (variant % 7) * 0.03,
                NoLoadSpeed = 2050 + (variant % 5) * 10,
                ShaftLength = 32.40 + (variant % 3) * 0.01,
                KnurlDiameter = 4.42
            });
            await repo.UpsertStageResultAsync(new StageTestData
            {
                Barcode = barcode, StationId = "A3", Stage = TestStage.Noise, CollectedAt = time.AddMinutes(1), Result = "OK",
                FwdNoise = 58.0 + (variant % 6) * 1.5,
                RevNoise = 52.0 + (variant % 4) * 1.2,
                NoiseDiff = 6.0 + (variant % 5) * 0.8
            });
            await repo.UpsertStageResultAsync(new StageTestData
            {
                Barcode = barcode, StationId = "A5", Stage = TestStage.Load, CollectedAt = time.AddMinutes(2), Result = "OK",
                LoadCurrent = 2.30 + (variant % 8) * 0.02,
                LoadSpeed = 1200 + (variant % 6) * 15
            });
        }

        private static async Task SeedNgRecordAsync(IMotorTestRepository repo, string barcode, DateTime time, NgPattern pattern)
        {
            bool noLoadNg = pattern is NgPattern.NoLoadHighCurrent or NgPattern.NoLoadHighSpeed;
            await repo.UpsertStageResultAsync(new StageTestData
            {
                Barcode = barcode, StationId = "A1", Stage = TestStage.NoLoad, CollectedAt = time,
                Result = noLoadNg ? "NG" : "OK",
                NoLoadCurrent = pattern == NgPattern.NoLoadHighCurrent ? 2.80 + _rng.NextDouble() * 0.5 : 1.82 + _rng.NextDouble() * 0.1,
                NoLoadSpeed = pattern == NgPattern.NoLoadHighSpeed ? 2300 + _rng.Next(200) : 2050 + _rng.Next(50),
                ShaftLength = 32.4, KnurlDiameter = 4.42
            });

            bool noiseNg = pattern is NgPattern.NoiseHighDiff or NgPattern.NoiseFwdLoud;
            await repo.UpsertStageResultAsync(new StageTestData
            {
                Barcode = barcode, StationId = "A3", Stage = TestStage.Noise, CollectedAt = time.AddMinutes(1),
                Result = noiseNg ? "NG" : "OK",
                FwdNoise = pattern == NgPattern.NoiseFwdLoud ? 75.0 + _rng.NextDouble() * 8 : 62.0 + _rng.NextDouble() * 3,
                RevNoise = 55.0 + _rng.NextDouble() * 3,
                NoiseDiff = pattern == NgPattern.NoiseHighDiff ? 18.0 + _rng.NextDouble() * 8 : 7.0 + _rng.NextDouble() * 3
            });

            bool loadNg = pattern is NgPattern.LoadHighCurrent or NgPattern.LoadLowSpeed;
            await repo.UpsertStageResultAsync(new StageTestData
            {
                Barcode = barcode, StationId = "A5", Stage = TestStage.Load, CollectedAt = time.AddMinutes(2),
                Result = loadNg ? "NG" : "OK",
                LoadCurrent = pattern == NgPattern.LoadHighCurrent ? 3.20 + _rng.NextDouble() * 0.6 : 2.30 + _rng.NextDouble() * 0.1,
                LoadSpeed = pattern == NgPattern.LoadLowSpeed ? 850 + _rng.Next(120) : 1200 + _rng.Next(50)
            });
        }

        private static async Task<int> SeedDayDataAsync(IMotorTestRepository repo, DateTime day, int okCount, int ngCount, int idx)
        {
            var allPatterns = System.Enum.GetValues<NgPattern>();
            for (int i = 0; i < okCount; i++)
            {
                string barcode = MakeBarcode(idx++);
                DateTime collectedAt = day.AddHours(8 + (i * 10.0 / okCount) % 12).AddMinutes(_rng.Next(60));
                await SeedOkRecordAsync(repo, barcode, collectedAt, i);
            }
            for (int i = 0; i < ngCount; i++)
            {
                string barcode = MakeBarcode(idx++);
                DateTime collectedAt = day.AddHours(8 + (i * 10.0 / Math.Max(ngCount, 1)) % 12).AddMinutes(_rng.Next(60));
                var pattern = allPatterns[_rng.Next(allPatterns.Length)];
                await SeedNgRecordAsync(repo, barcode, collectedAt, pattern);
            }
            return idx;
        }

        private static async Task<int> SeedWeekDataAsync(IMotorTestRepository repo, DateTime weekStart, int totalOk, int totalNg, int idx)
        {
            var allPatterns = System.Enum.GetValues<NgPattern>();
            for (int d = 0; d < 7; d++)
            {
                DateTime day = weekStart.AddDays(d);
                bool isWeekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                double ratio = isWeekend ? 0.043 : 0.174;
                int dayOk = Math.Max(1, (int)(totalOk * ratio));
                int dayNg = Math.Max(0, (int)(totalNg * ratio));
                for (int i = 0; i < dayOk; i++)
                {
                    string barcode = MakeBarcode(idx++);
                    DateTime collectedAt = day.AddHours(8 + (i * 10.0 / Math.Max(dayOk, 1)) % 12).AddMinutes(_rng.Next(60));
                    await SeedOkRecordAsync(repo, barcode, collectedAt, i);
                }
                for (int i = 0; i < dayNg; i++)
                {
                    string barcode = MakeBarcode(idx++);
                    DateTime collectedAt = day.AddHours(8 + (i * 10.0 / Math.Max(dayNg, 1)) % 12).AddMinutes(_rng.Next(60));
                    var pattern = allPatterns[_rng.Next(allPatterns.Length)];
                    await SeedNgRecordAsync(repo, barcode, collectedAt, pattern);
                }
            }
            return idx;
        }

        private bool _isDisposed;

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            // 按依赖顺序释放：NotificationWriter → BatchWriter → PollingService → EventChannel
            NotificationWriterService?.Dispose();
            BatchWriter?.Dispose();
            PollingService?.Dispose();
            EventChannel?.Dispose();
            HikvisionService?.Dispose();
            DeadLetterQueue?.Dispose();

            if (PollingService != null)
            {
                PollingService.SnapshotReceived -= OnSnapshotReceivedForNotification;
                PollingService.LogReceived -= OnLogReceivedForNotification;
            }
        }
    }
}
