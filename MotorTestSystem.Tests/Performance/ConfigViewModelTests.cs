using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MotorTestSystem.Models;
using MotorTestSystem.Services;
using MotorTestSystem.ViewModels;
using Xunit;

namespace MotorTestSystem.Tests.Performance
{
    public class ConfigViewModelTests
    {
        [Fact]
        public async Task TestConcurrency_ShouldFailOnConcurrentClicks_BugCondition()
        {
            await StaHelper.RunAsync(async () =>
            {
                // Arrange
                var config = new StationConfig
                {
                    Id = "A1",
                    Name = "测试工位",
                    IpAddress = "127.0.0.1",
                    Port = 502,
                    Protocol = "ModbusTCP",
                    PlcModel = "S7-1200",
                    Status = "离线",
                    IsConnected = false
                };

                var configs = new ObservableCollection<StationConfig> { config };
                var dbContext = new SqlSugarDbContext();
                var mockRepo = new MockMotorTestRepository(Thread.CurrentThread.ManagedThreadId);
                var mockClientFactory = new DelayPlcClientFactory(200); // 200ms delay to allow concurrency
                var mockUser = new InMemoryUserService();
                var mockAuth = new AuthService(mockUser);
                var mockNotification = new SqlSugarNotificationService(dbContext);
                var eventChannel = new EventChannelService();

                using var runtime = new BackendRuntime(
                    configs,
                    dbContext,
                    mockRepo,
                    mockClientFactory,
                    mockUser,
                    mockAuth,
                    mockNotification,
                    eventChannel
                );

                var dialogService = new MockDialogService();
                var viewModel = new ConfigViewModel(runtime, dialogService);

                // Act - Trigger multiple executions concurrently without waiting
                var t1 = viewModel.TestConnectionCommand.ExecuteAsync(config);
                var t2 = viewModel.TestConnectionCommand.ExecuteAsync(config);
                var t3 = viewModel.TestConnectionCommand.ExecuteAsync(config);

                await Task.WhenAll(t1, t2, t3);

                // Assert (Expected Behavior - should FAIL on unmodified code)
                // We expect only 1 call to ConnectAsync because concurrent clicks should be blocked.
                var client = mockClientFactory.LastCreatedClient;
                Assert.NotNull(client);
                Assert.Equal(1, client.ConnectCount);
                Assert.Equal(5, viewModel.DiagnosticLogs.Count); // 4 initial + 1 new
            });
        }

        [Fact]
        public async Task TestConnectionFunctionality_Preservation()
        {
            await StaHelper.RunAsync(async () =>
            {
                // Arrange
                var config = new StationConfig
                {
                    Id = "A1",
                    Name = "测试工位",
                    IpAddress = "127.0.0.1",
                    Port = 502,
                    Protocol = "ModbusTCP",
                    PlcModel = "S7-1200",
                    Status = "离线",
                    IsConnected = false
                };

                var configs = new ObservableCollection<StationConfig> { config };
                var dbContext = new SqlSugarDbContext();
                var mockRepo = new MockMotorTestRepository(Thread.CurrentThread.ManagedThreadId);
                var mockClientFactory = new DelayPlcClientFactory(0); // No delay
                var mockUser = new InMemoryUserService();
                var mockAuth = new AuthService(mockUser);
                var mockNotification = new SqlSugarNotificationService(dbContext);
                var eventChannel = new EventChannelService();

                using var runtime = new BackendRuntime(
                    configs,
                    dbContext,
                    mockRepo,
                    mockClientFactory,
                    mockUser,
                    mockAuth,
                    mockNotification,
                    eventChannel
                );

                var dialogService = new MockDialogService();
                var viewModel = new ConfigViewModel(runtime, dialogService);

                // Act - Test successful connection
                mockClientFactory.ShouldConnectSucceed = true;
                await viewModel.TestConnectionCommand.ExecuteAsync(config);

                // Assert
                Assert.True(config.IsConnected);
                Assert.Equal("在线", config.Status);
                Assert.Single(dialogService.Messages);
                Assert.Contains("连接正常", dialogService.Messages[0].Message);
                Assert.Equal(5, viewModel.DiagnosticLogs.Count); // 4 initial + 1 new

                // Act - Test failed connection
                dialogService.Messages.Clear();
                mockClientFactory.ShouldConnectSucceed = false;
                await viewModel.TestConnectionCommand.ExecuteAsync(config);

                // Assert
                Assert.False(config.IsConnected);
                Assert.Equal("离线", config.Status);
                Assert.Single(dialogService.Messages);
                Assert.Contains("无法建立连接", dialogService.Messages[0].Message);
                Assert.Equal(6, viewModel.DiagnosticLogs.Count); // 5 + 1 new
            });
        }
    }

    public class DelayPlcClient : IPlcClient
    {
        private readonly int _delayMs;
        public StationConfig Config { get; }
        
        private int _connectCount;
        public int ConnectCount => _connectCount;

        public bool ShouldSucceed { get; set; } = true;

        public DelayPlcClient(StationConfig config, int delayMs)
        {
            Config = config;
            _delayMs = delayMs;
        }

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _connectCount);
            if (_delayMs > 0)
            {
                await Task.Delay(_delayMs, cancellationToken);
            }
            return ShouldSucceed;
        }

        public Task<StationSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new StationSnapshot { StationId = Config.Id, IsOnline = true });
        }

        public Task ResetCompletionSignalAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }

    public class DelayPlcClientFactory : IPlcClientFactory
    {
        private readonly int _delayMs;
        public DelayPlcClient? LastCreatedClient { get; private set; }
        public bool ShouldConnectSucceed { get; set; } = true;

        public DelayPlcClientFactory(int delayMs)
        {
            _delayMs = delayMs;
        }

        public IPlcClient Create(StationConfig config)
        {
            if (LastCreatedClient == null || LastCreatedClient.Config.Id != config.Id)
            {
                LastCreatedClient = new DelayPlcClient(config, _delayMs);
            }
            LastCreatedClient.ShouldSucceed = ShouldConnectSucceed;
            return LastCreatedClient;
        }
    }

    public class MockDialogService : IDialogService
    {
        public string? SavedFilter { get; set; }
        public string? SavedFileName { get; set; }
        public string? ClipboardText { get; set; }
        public List<(string Message, string Title, MessageBoxButton Button, MessageBoxImage Icon)> Messages { get; } = new();

        public Task<MessageBoxResult> ShowMessageAsync(string message, string title, MessageBoxButton button, MessageBoxImage icon)
        {
            Messages.Add((message, title, button, icon));
            return Task.FromResult(MessageBoxResult.OK);
        }

        public string? ShowSaveFileDialog(string filter, string defaultFileName)
        {
            SavedFilter = filter;
            SavedFileName = defaultFileName;
            return "dummy_path.csv";
        }

        public bool ShowPrintDialog(System.Windows.Documents.FlowDocument document)
        {
            return true;
        }

        public void ShowReportWindow(MotorTestRecordModel motor, ISeries[] noLoadSeries, ISeries[] noiseSeries, Axis[] xAxes, Axis[] noLoadYAxes, Axis[] noiseYAxes, SolidColorPaint tooltipBg, SolidColorPaint tooltipText)
        {
        }

        public UserEditResult? ShowUserEditDialog(string title, string account, string name, string role, bool isEnabled)
        {
            return null;
        }

        public void SetClipboardText(string text)
        {
            ClipboardText = text;
        }
    }
}
