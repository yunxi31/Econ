using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    public sealed class BackendRuntime
    {
        private static readonly Random _rng = new(42); // 固定种子保证可复现 — 必须在 Shared 之前

        public static BackendRuntime Shared { get; } = CreateDefault();

        public BackendRuntime(
            ObservableCollection<StationConfig> stationConfigs,
            IMotorTestRepository repository,
            IPlcClientFactory plcClientFactory,
            IUserService userService,
            IAuthService authService)
        {
            StationConfigs = stationConfigs;
            Repository = repository;
            UserService = userService;
            AuthService = authService;
            PollingService = new PlcPollingService(StationConfigs, Repository, plcClientFactory);
        }

        public ObservableCollection<StationConfig> StationConfigs { get; }
        public IMotorTestRepository Repository { get; }
        public IUserService UserService { get; }
        public IAuthService AuthService { get; }
        public PlcPollingService PollingService { get; }

        private static BackendRuntime CreateDefault()
        {
            var configs = new ObservableCollection<StationConfig>
            {
                new() { Id = "A1", Name = "A1", PlcModel = "FX5U", IpAddress = "192.168.10.11", Port = 502, Protocol = "ModbusTCP", IsConnected = true, Status = "在线" },
                new() { Id = "A2", Name = "A2", PlcModel = "S7-1200", IpAddress = "192.168.10.12", Port = 102, Protocol = "S7 Protocol (TCP)", IsConnected = true, Status = "在线" },
                new() { Id = "A3", Name = "A3", PlcModel = "AM600", IpAddress = "192.168.10.13", Port = 502, Protocol = "ModbusTCP", IsConnected = false, Status = "故障" },
                new() { Id = "A4", Name = "A4", PlcModel = "FX5U", IpAddress = "192.168.10.14", Port = 502, Protocol = "ModbusTCP", IsConnected = false, Status = "离线" },
                new() { Id = "A5", Name = "A5", PlcModel = "S7-1500", IpAddress = "192.168.10.15", Port = 102, Protocol = "S7 Protocol (TCP)", IsConnected = true, Status = "在线" },
                new() { Id = "A6", Name = "A6", PlcModel = "AM600", IpAddress = "192.168.10.16", Port = 502, Protocol = "ModbusTCP", IsConnected = true, Status = "在线" }
            };

            var repository = new InMemoryMotorTestRepository();
            SeedRepositoryAsync(repository).GetAwaiter().GetResult();

            var userService = new InMemoryUserService();
            var authService = new AuthService(userService);

            return new BackendRuntime(configs, repository, new PlcClientFactory(useSimulation: false), userService, authService);
        }

        private static async Task SeedRepositoryAsync(IMotorTestRepository repository)
        {
            var now = DateTime.Now;
            int idx = 0; // 全局 barcode 计数器

            // ========================================
            // 1. 本月前 3 周的历史数据（测试"本月"良率趋势）
            // ========================================
            for (int weekAgo = 3; weekAgo >= 1; weekAgo--)
            {
                DateTime weekStart = now.Date.AddDays(-weekAgo * 7);
                int weekOk = 500 + _rng.Next(300);
                int weekNg = 8 + _rng.Next(13);
                idx = await SeedWeekDataAsync(repository, weekStart, weekOk, weekNg, idx);
            }

            // ========================================
            // 2. 本周每天的数据（测试"本周"良率趋势），跳过今天
            // ========================================
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

            // ========================================
            // 3. 今天过去 8 小时（测试"今日"小时级图表 + 不良分布 + 故障排行）
            // ========================================
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

        private static string MakeBarcode(int index) => $"DES-SR-150GEN{1992900399000 + index}";

        // ========================================
        // NG 故障模式 — 覆盖所有 6 种分类
        // ========================================
        private enum NgPattern
        {
            NoLoadHighCurrent,   // 空载电流超限 (>2.5) → "电机起动电流超限"
            NoLoadHighSpeed,     // 空载转速异常 (>2200) → "空载转速异常"
            NoiseHighDiff,       // 噪音差值过大 (>15)   → "空载噪声过大"
            NoiseFwdLoud,        // 正转噪声超限 (>70)   → "正转噪声超限"
            LoadHighCurrent,     // 负载电流超限 (>3.0)   → "负载电流超限"
            LoadLowSpeed,        // 负载转速偏低 (<1000)  → "负载转速偏低"
        }

        // ========================================
        // OK 记录 — 全阶段合格
        // ========================================
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

        // ========================================
        // NG 记录 — 根据故障模式生成异常数据
        // ========================================
        private static async Task SeedNgRecordAsync(IMotorTestRepository repo, string barcode, DateTime time, NgPattern pattern)
        {
            bool noLoadNg = pattern is NgPattern.NoLoadHighCurrent or NgPattern.NoLoadHighSpeed;
            await repo.UpsertStageResultAsync(new StageTestData
            {
                Barcode = barcode, StationId = "A1", Stage = TestStage.NoLoad, CollectedAt = time,
                Result = noLoadNg ? "NG" : "OK",
                NoLoadCurrent = pattern == NgPattern.NoLoadHighCurrent ? 2.80 + _rng.NextDouble() * 0.5 : 1.82 + _rng.NextDouble() * 0.1,
                NoLoadSpeed = pattern == NgPattern.NoLoadHighSpeed ? 2300 + _rng.Next(200) : 2050 + _rng.Next(50),
                ShaftLength = 32.4,
                KnurlDiameter = 4.42
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

        // ========================================
        // 批量生成一天的数据（返回新的 barcode 计数器）
        // ========================================
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

        // ========================================
        // 批量生成一周的数据（返回新的 barcode 计数器）
        // ========================================
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
    }
}
