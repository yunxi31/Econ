using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MotorTestSystem.Models;
using MotorTestSystem.Services;
using MotorTestSystem.ViewModels;
using Xunit;

namespace MotorTestSystem.Tests.Performance
{
    public class UiThreadFloodingTests
    {
        [Fact]
        public async Task TestUiThreadFlooding_ShouldExposePerformanceBottleneck()
        {
            // Run the test in an STA thread to support WPF Dispatcher
            await StaHelper.RunAsync(async () =>
            {
                // Arrange
                var uiThreadId = Thread.CurrentThread.ManagedThreadId;
                var mockRepo = new MockMotorTestRepository(uiThreadId) { QueryDelayMs = 50 };
                
                var configs = new ObservableCollection<StationConfig>
                {
                    new() { Id = "A1", PlcModel = "S7-1200", IpAddress = "127.0.0.1" },
                    new() { Id = "A2", PlcModel = "S7-1200", IpAddress = "127.0.0.1" },
                    new() { Id = "A3", PlcModel = "S7-1200", IpAddress = "127.0.0.1" },
                    new() { Id = "A4", PlcModel = "S7-1200", IpAddress = "127.0.0.1" },
                    new() { Id = "A5", PlcModel = "S7-1200", IpAddress = "127.0.0.1" },
                    new() { Id = "A6", PlcModel = "S7-1200", IpAddress = "127.0.0.1" }
                };

                // Clear/Reset static state
                ResetStaticState();

                // Mock dependencies for BackendRuntime
                var dbContext = new SqlSugarDbContext();
                var mockFactory = new MockPlcClientFactory();
                var mockUser = new InMemoryUserService();
                var mockAuth = new AuthService(mockUser);
                var mockNotification = new SqlSugarNotificationService(dbContext);

                var eventChannel = new EventChannelService();
                using var runtime = new BackendRuntime(
                    configs,
                    dbContext,
                    mockRepo,
                    mockFactory,
                    mockUser,
                    mockAuth,
                    mockNotification,
                    eventChannel
                );

                var viewModel = new DashboardViewModel(mockRepo, runtime);

                // Act
                var sw = Stopwatch.StartNew();
                
                // Simulate 6 stations producing snapshot events in quick succession
                var method = typeof(DashboardViewModel).GetMethod("OnSnapshotReceived", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.NotNull(method);

                for (int i = 0; i < 6; i++)
                {
                    var snapshot = new StationSnapshot
                    {
                        StationId = $"A{i + 1}",
                        IsOnline = true,
                        Status = 1,
                        CompletionSignal = true,
                        CompletedData = new StageTestData
                        {
                            Barcode = $"TEST-FLOOD-{i}",
                            StationId = $"A{i + 1}",
                            Stage = TestStage.NoLoad,
                            Result = "OK"
                        }
                    };
                    method.Invoke(viewModel, new object[] { runtime.PollingService, snapshot });
                }

                // Process all pending Dispatcher operations
                StaHelper.DoEvents();
                sw.Stop();

                double durationSeconds = sw.Elapsed.TotalSeconds;
                if (durationSeconds < 0.1) durationSeconds = 0.1; // Prevent division by zero / extreme values

                double uiThreadDbQueryRate = mockRepo.UiThreadQueryCount / durationSeconds;
                double uiThreadBlockingTime = mockRepo.TotalUiThreadBlockingTimeMs;

                // Assert (Bug Condition validation)
                // Before fix, every snapshot triggers 4 DB queries on the UI thread.
                // 6 snapshots * 4 queries = 24 queries in total.
                // Assert rate > 6 times/sec and blocking time > 100ms.
                Assert.True(uiThreadDbQueryRate > 6, $"Expected UI thread DB query rate > 6, but was {uiThreadDbQueryRate}");
                Assert.True(uiThreadBlockingTime > 100, $"Expected UI thread blocking time > 100ms, but was {uiThreadBlockingTime}ms");
            });
        }

        private static void ResetStaticState()
        {
            var field = typeof(BackendRuntime).GetField("<Shared>k__BackingField", 
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(null, null);
            }

            var initField = typeof(SqlSugarDbContext).GetField("_initialized", 
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (initField != null)
            {
                initField.SetValue(null, false);
            }
        }
    }

    public static class StaHelper
    {
        public static Task RunAsync(Func<Task> action)
        {
            var tcs = new TaskCompletionSource();
            var thread = new Thread(() =>
            {
                try
                {
                    if (Application.Current == null)
                    {
                        new Application();
                    }
                    action().GetAwaiter().GetResult();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return tcs.Task;
        }

        public static void DoEvents()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new DispatcherOperationCallback(ExitFrame), frame);
            Dispatcher.PushFrame(frame);
        }

        private static object ExitFrame(object f)
        {
            ((DispatcherFrame)f).Continue = false;
            return null;
        }
    }

    public class MockMotorTestRepository : IMotorTestRepository
    {
        private readonly int _uiThreadId;

        public int UiThreadQueryCount { get; private set; }
        public int NonUiThreadQueryCount { get; private set; }
        public double TotalUiThreadBlockingTimeMs { get; private set; }
        public int QueryDelayMs { get; set; } = 50;

        public MockMotorTestRepository(int uiThreadId)
        {
            _uiThreadId = uiThreadId;
        }

        private void RecordCall()
        {
            if (Thread.CurrentThread.ManagedThreadId == _uiThreadId)
            {
                UiThreadQueryCount++;
                Thread.Sleep(QueryDelayMs);
                TotalUiThreadBlockingTimeMs += QueryDelayMs;
            }
            else
            {
                NonUiThreadQueryCount++;
                Thread.Sleep(QueryDelayMs);
            }
        }

        public Task UpsertStageResultAsync(StageTestData data, CancellationToken cancellationToken = default)
        {
            RecordCall();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MotorTestResult>> QueryAsync(MotorTestQuery query, CancellationToken cancellationToken = default)
        {
            RecordCall();
            return Task.FromResult<IReadOnlyList<MotorTestResult>>(new List<MotorTestResult>());
        }

        public Task<IReadOnlyList<MotorTestResult>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        {
            RecordCall();
            return Task.FromResult<IReadOnlyList<MotorTestResult>>(new List<MotorTestResult>());
        }

        public Task<ProductionSummary> GetSummaryAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
        {
            RecordCall();
            return Task.FromResult(new ProductionSummary());
        }

        public Task<DefectSummary> GetDefectSummaryAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
        {
            RecordCall();
            return Task.FromResult(new DefectSummary());
        }

        public Task<IReadOnlyList<FaultRankItem>> GetFaultRankingAsync(DateTime startTime, DateTime endTime, int topN = 5, CancellationToken cancellationToken = default)
        {
            RecordCall();
            return Task.FromResult<IReadOnlyList<FaultRankItem>>(new List<FaultRankItem>());
        }

        public Task BulkUpsertAsync(IEnumerable<StageTestData> results, CancellationToken cancellationToken = default)
        {
            RecordCall();
            return Task.CompletedTask;
        }
    }

    public class MockPlcClient : IPlcClient
    {
        public StationConfig Config { get; }
        public int ReadCount { get; private set; }

        public MockPlcClient(StationConfig config)
        {
            Config = config;
        }

        public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<StationSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return Task.FromResult(new StationSnapshot
            {
                StationId = Config.Id,
                IsOnline = true,
                Status = 1,
                CompletionSignal = false
            });
        }

        public Task ResetCompletionSignalAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }

    public class MockPlcClientFactory : IPlcClientFactory
    {
        public Dictionary<string, MockPlcClient> Clients { get; } = new();

        public IPlcClient Create(StationConfig config)
        {
            var client = new MockPlcClient(config);
            Clients[config.Id] = client;
            return client;
        }
    }
}
