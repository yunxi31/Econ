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
        public static Exception LastAppCreateException;

        [Fact]
        public async Task TestUiThreadFlooding_ShouldExposePerformanceBottleneck()
        {
            LastAppCreateException = null;
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

                var viewModel = new DashboardViewModel(mockRepo, runtime, new TestDispatcherService(Dispatcher.CurrentDispatcher));

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
                    Console.WriteLine($"[TEST-DEBUG] Invoking OnSnapshotReceived for A{i + 1}");
                    method.Invoke(viewModel, new object[] { runtime.PollingService, snapshot });
                }

                // Process all pending Dispatcher operations
                Console.WriteLine("[TEST-DEBUG] Before DoEvents");
                StaHelper.DoEvents();
                Console.WriteLine($"[TEST-DEBUG] After DoEvents, UiThreadQueryCount = {mockRepo.UiThreadQueryCount}, NonUiThreadQueryCount = {mockRepo.NonUiThreadQueryCount}");
                sw.Stop();

                double durationSeconds = sw.Elapsed.TotalSeconds;
                if (durationSeconds < 0.1) durationSeconds = 0.1;

                double uiThreadDbQueryRate = mockRepo.UiThreadQueryCount / durationSeconds;
                double uiThreadBlockingTime = mockRepo.TotalUiThreadBlockingTimeMs;

                if (uiThreadDbQueryRate <= 6 || uiThreadBlockingTime <= 100)
                {
                    var diagMsg = $"[DIAG] Current Thread ID: {Thread.CurrentThread.ManagedThreadId}, " +
                                  $"Application.Current is null: {Application.Current == null}, " +
                                  $"App.Current.Dispatcher Thread ID: {Application.Current?.Dispatcher?.Thread?.ManagedThreadId}, " +
                                  $"Dispatcher.CurrentDispatcher Thread ID: {Dispatcher.CurrentDispatcher?.Thread?.ManagedThreadId}, " +
                                  $"UiThreadQueryCount: {mockRepo.UiThreadQueryCount}, " +
                                  $"NonUiThreadQueryCount: {mockRepo.NonUiThreadQueryCount}, " +
                                  $"uiThreadDbQueryRate: {uiThreadDbQueryRate}, " +
                                  $"uiThreadBlockingTime: {uiThreadBlockingTime}, " +
                                  $"LastAppCreateException: {LastAppCreateException}";
                    throw new Exception(diagMsg);
                }
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
        private static void ResetApplicationStaticFields()
        {
            var appField = typeof(Application).GetField("_appInstance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (appField != null)
            {
                appField.SetValue(null, null);
            }
            var appCreatedField = typeof(Application).GetField("_appCreatedInThisAppDomain",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (appCreatedField != null)
            {
                appCreatedField.SetValue(null, false);
            }
        }

         public static Task RunAsync(Func<Task> action)
        {
            var tcs = new TaskCompletionSource();
            var thread = new Thread(() =>
            {
                try
                {
                    ResetApplicationStaticFields();
                    Exception appCreateEx = null;
                    if (Application.Current == null)
                    {
                        try
                        {
                            new Application();
                        }
                        catch (Exception ex)
                        {
                            appCreateEx = ex;
                        }
                    }
                    UiThreadFloodingTests.LastAppCreateException = appCreateEx;

                    var dispatcher = Dispatcher.CurrentDispatcher;
                    var syncContext = new DispatcherSynchronizationContext(dispatcher);
                    SynchronizationContext.SetSynchronizationContext(syncContext);

                    var task = action();
                    var frame = new DispatcherFrame();
                    task.ContinueWith(t =>
                    {
                        dispatcher.BeginInvoke(new Action(() => { frame.Continue = false; }));
                    });

                    if (!task.IsCompleted)
                    {
                        Dispatcher.PushFrame(frame);
                    }

                    task.GetAwaiter().GetResult();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
                finally
                {
                    ResetApplicationStaticFields();
                    SynchronizationContext.SetSynchronizationContext(null);
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

        public Task BulkUpsertWithRawSqlAsync(IEnumerable<StageTestData> results, CancellationToken cancellationToken = default)
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

    public class TestDispatcherService : IDispatcherService
    {
        private readonly Dispatcher _dispatcher;

        public TestDispatcherService(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public void Invoke(Action action)
        {
            if (_dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                _dispatcher.Invoke(action);
            }
        }

        public Task InvokeAsync(Action action)
        {
            if (_dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }
            else
            {
                return _dispatcher.InvokeAsync(action).Task;
            }
        }

        public Task InvokeAsync(Func<Task> action)
        {
            if (_dispatcher.CheckAccess())
            {
                return action();
            }
            else
            {
                return _dispatcher.InvokeAsync(action).Task.Unwrap();
            }
        }

        public Task<T> InvokeAsync<T>(Func<T> func)
        {
            if (_dispatcher.CheckAccess())
            {
                return Task.FromResult(func());
            }
            else
            {
                return _dispatcher.InvokeAsync(func).Task;
            }
        }
    }
}
