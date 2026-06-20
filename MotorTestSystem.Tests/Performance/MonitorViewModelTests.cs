using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MotorTestSystem.Models;
using MotorTestSystem.Services;
using MotorTestSystem.ViewModels;
using Xunit;

namespace MotorTestSystem.Tests.Performance
{
    public class MonitorViewModelTests
    {
        [Fact]
        public async Task TestMonitorViewModelUpdates_BugCondition()
        {
            await StaHelper.RunAsync(async () =>
            {
                // Arrange
                var configs = new ObservableCollection<StationConfig>
                {
                    new StationConfig { Id = "A1", Protocol = "ModbusTcp" },
                    new StationConfig { Id = "A2", Protocol = "ModbusTcp" }
                };
                var dbContext = new SqlSugarDbContext();
                var mockRepo = new MockMotorTestRepositoryForDashboard(Thread.CurrentThread.ManagedThreadId);
                var mockFactory = new MockPlcClientFactory();
                var mockUser = new InMemoryUserService();
                var mockAuth = new AuthService(mockUser);
                var mockNotification = new SqlSugarNotificationService(dbContext);
                var eventChannel = new EventChannelService();

                ResetStaticState();

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

                var viewModel = new MonitorViewModel(runtime);

                Console.WriteLine($"[DEBUG] Application.Current is null: {Application.Current == null}");
                if (Application.Current != null)
                {
                    Console.WriteLine($"[DEBUG] Application.Current.Dispatcher is null: {Application.Current.Dispatcher == null}");
                    if (Application.Current.Dispatcher != null)
                    {
                        Console.WriteLine($"[DEBUG] Dispatcher Thread ID: {Application.Current.Dispatcher.Thread.ManagedThreadId}, Current Thread ID: {Thread.CurrentThread.ManagedThreadId}");
                        Console.WriteLine($"[DEBUG] Dispatcher CheckAccess: {Application.Current.Dispatcher.CheckAccess()}");
                    }
                }

                // Act - simulate snapshot arrival
                var snapshot = new StationSnapshot
                {
                    StationId = "A1",
                    IsOnline = true,
                    Status = 1,
                    CompletionSignal = true,
                    CompletedData = new StageTestData
                    {
                        Barcode = "SN-TEST-1",
                        StationId = "A1",
                        Stage = TestStage.NoLoad,
                        Result = "OK",
                        Progress = 85.0,
                        Voltage = 220.5,
                        Current = 5.2,
                        RPM = 3000
                    }
                };

                // Directly invoke ApplySnapshot via reflection to bypass RunOnUiThread
                // which depends on Application.Current that may be null due to test interaction.
                var pollingService = runtime.PollingService;
                var snapshotMethod = typeof(MonitorViewModel).GetMethod("ApplySnapshot",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.NotNull(snapshotMethod);
                snapshotMethod.Invoke(viewModel, new object[] { snapshot });
                Console.WriteLine($"[DEBUG] After ApplySnapshot, a1Station.Progress is {viewModel.NoLoadStations.FirstOrDefault(s => s.Id == "A1")?.Progress}");

                // Assert
                var a1Station = viewModel.NoLoadStations.FirstOrDefault(s => s.Id == "A1");
                Assert.NotNull(a1Station);
                
                // Bug Condition expectation: UI telemetry properties should be updated
                Assert.Equal(85.0, a1Station.Progress);
                Assert.Equal(220.5, a1Station.Voltage);
                Assert.Equal(5.2, a1Station.Current);
                Assert.Equal(3000, a1Station.Rpm);

                // RecentBarcodes should be updated
                Assert.Contains("A1: SN-TEST-1", viewModel.RecentBarcodes);

                // Act - Simulate LogReceived (alarm)
                var logField = pollingService.GetType()
                    .GetField("LogReceived", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.NotNull(logField);
                var logHandler = (MulticastDelegate)logField.GetValue(pollingService);
                Console.WriteLine($"[DEBUG] LogReceived handler is null: {logHandler == null}");
                if (logHandler != null)
                {
                    Console.WriteLine($"[DEBUG] LogReceived invocation list count: {logHandler.GetInvocationList().Length}");
                    foreach (var d in logHandler.GetInvocationList())
                    {
                        Console.WriteLine($"[DEBUG] Invoking LogReceived: {d.Method.Name} on target {d.Target?.GetType().FullName}");
                        d.Method.Invoke(d.Target, new object[] { pollingService, "A4: 噪音超标" });
                    }
                }

                // Wait for dispatcher
                await Task.Delay(100);
                StaHelper.DoEvents();
                Console.WriteLine($"[DEBUG] Alerts count: {viewModel.Alerts.Count}");
                foreach (var alert in viewModel.Alerts)
                {
                    Console.WriteLine($"[DEBUG] Alert in list: {alert}");
                }

                // Assert
                Assert.Contains("A4: 噪音超标", viewModel.Alerts);
            });
        }

        [Fact]
        public async Task TestMonitorViewModelPreservation_Preservation()
        {
            await StaHelper.RunAsync(async () =>
            {
                // Arrange
                var configs = new ObservableCollection<StationConfig>
                {
                    new StationConfig { Id = "A1", Protocol = "ModbusTcp" }
                };
                var dbContext = new SqlSugarDbContext();
                var mockRepo = new MockMotorTestRepositoryForDashboard(Thread.CurrentThread.ManagedThreadId);
                var mockFactory = new MockPlcClientFactory();
                var mockUser = new InMemoryUserService();
                var mockAuth = new AuthService(mockUser);
                var mockNotification = new SqlSugarNotificationService(dbContext);
                var eventChannel = new EventChannelService();

                ResetStaticState();

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

                var viewModel = new MonitorViewModel(runtime);
                var pollingService = runtime.PollingService;

                var snapshotMethod = typeof(MonitorViewModel).GetMethod("ApplySnapshot",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.NotNull(snapshotMethod);

                // Verify list capacity restriction (max 5)
                for (int i = 1; i <= 6; i++)
                {
                    var snapshot = new StationSnapshot
                    {
                        StationId = "A1",
                        IsOnline = true,
                        Status = 1,
                        CompletionSignal = true,
                        CompletedData = new StageTestData
                        {
                            Barcode = $"SN-TEST-{i}",
                            StationId = "A1",
                            Stage = TestStage.NoLoad,
                            Result = "OK"
                        }
                    };

                    snapshotMethod.Invoke(viewModel, new object[] { snapshot });
                }

                var logField = pollingService.GetType()
                    .GetField("LogReceived", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var logHandler = (MulticastDelegate)logField.GetValue(pollingService);

                for (int i = 1; i <= 6; i++)
                {
                    foreach (var d in logHandler.GetInvocationList())
                    {
                        d.Method.Invoke(d.Target, new object[] { pollingService, $"警报{i}: 超标" });
                    }
                }

                await Task.Delay(100);
                StaHelper.DoEvents();

                // Assert RecentBarcodes size constraint
                Assert.True(viewModel.RecentBarcodes.Count <= 5, $"RecentBarcodes count should be <= 5, but was {viewModel.RecentBarcodes.Count}");
                // Assert Alerts size constraint
                Assert.True(viewModel.Alerts.Count <= 5, $"Alerts count should be <= 5, but was {viewModel.Alerts.Count}");

                // Assert existing properties preservation
                var a1Station = viewModel.NoLoadStations.FirstOrDefault(s => s.Id == "A1");
                Assert.NotNull(a1Station);
                Assert.Equal("SN-TEST-6", a1Station.Barcode);
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
}
