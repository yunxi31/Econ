using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MotorTestSystem.Models;
using MotorTestSystem.Services;
using MotorTestSystem.ViewModels;
using Xunit;

namespace MotorTestSystem.Tests.Performance
{
    public class HistoryViewModelTests
    {
        [Fact]
        public async Task TestPrintConcurrency_ShouldFailOnConcurrentClicks_BugCondition()
        {
            await StaHelper.RunAsync(async () =>
            {
                // Arrange
                var mockRepo = new MockMotorTestRepository(Thread.CurrentThread.ManagedThreadId);
                var mockDialog = new ConcurrencyPrintDialogService { DelayMs = 200 };
                var mockDispatcher = new MockDispatcherService();
                
                var viewModel = new HistoryViewModel(mockRepo, mockDialog, mockDispatcher);
                
                // Select a motor
                var motor = new MotorTestRecordModel
                {
                    Barcode = "SN-TEST-CONCURRENCY",
                    TestTime = DateTime.Now,
                    FinalResult = "OK"
                };
                viewModel.SelectedMotor = motor;

                // Act - Trigger multiple print commands concurrently.
                var t1 = viewModel.PrintTraceCommand.ExecuteAsync(null);
                var t2 = viewModel.PrintTraceCommand.ExecuteAsync(null);

                await Task.WhenAll(t1, t2);

                // Assert (Expected Behavior - should FAIL on unmodified code)
                // We expect only 1 print call because concurrent clicks should be blocked.
                Assert.Equal(1, mockDialog.PrintCallCount);
            });
        }

        [Fact]
        public async Task TestPrintFunctionality_Preservation()
        {
            await StaHelper.RunAsync(async () =>
            {
                // Arrange
                var mockRepo = new MockMotorTestRepository(Thread.CurrentThread.ManagedThreadId);
                var mockDialog = new ConcurrencyPrintDialogService { DelayMs = 0 }; // No delay
                var mockDispatcher = new MockDispatcherService();
                
                var viewModel = new HistoryViewModel(mockRepo, mockDialog, mockDispatcher);
                
                // Select a motor
                var motor = new MotorTestRecordModel
                {
                    Barcode = "SN-TEST-PRESERVATION",
                    TestTime = DateTime.Now,
                    FinalResult = "OK"
                };
                viewModel.SelectedMotor = motor;

                // Act
                await viewModel.PrintTraceCommand.ExecuteAsync(null);

                // Assert
                Assert.Equal(1, mockDialog.PrintCallCount);
                Assert.False(viewModel.IsPrinting);
                Assert.Empty(viewModel.PrintStatus);
            });
        }

        [Fact]
        public void TestPaginationBoundary_ShouldPreventOutOfRange_BugCondition()
        {
            // Arrange
            var mockRepo = new MockMotorTestRepository(Thread.CurrentThread.ManagedThreadId);
            var mockDialog = new ConcurrencyPrintDialogService();
            var mockDispatcher = new MockDispatcherService();
            var viewModel = new HistoryViewModel(mockRepo, mockDialog, mockDispatcher);

            // Act & Assert
            // 1. CurrentPage = 1, PreviousPageCommand.CanExecute should be false
            viewModel.CurrentPage = 1;
            viewModel.TotalPages = 3;
            Assert.False(viewModel.PreviousPageCommand.CanExecute(null));

            // 2. CurrentPage = 3 (TotalPages = 3), NextPageCommand.CanExecute should be false
            viewModel.CurrentPage = 3;
            Assert.False(viewModel.NextPageCommand.CanExecute(null));
        }

        [Fact]
        public void TestPaginationFunctionality_Preservation()
        {
            // Arrange
            var mockRepo = new MockMotorTestRepository(Thread.CurrentThread.ManagedThreadId);
            var mockDialog = new ConcurrencyPrintDialogService();
            var mockDispatcher = new MockDispatcherService();
            var viewModel = new HistoryViewModel(mockRepo, mockDialog, mockDispatcher);

            viewModel.TotalPages = 5;
            viewModel.CurrentPage = 3;

            // Act & Assert
            // PreviousPage should be executable and decrease CurrentPage
            Assert.True(viewModel.PreviousPageCommand.CanExecute(null));
            viewModel.PreviousPageCommand.Execute(null);
            Assert.Equal(2, viewModel.CurrentPage);

            // NextPage should be executable and increase CurrentPage
            Assert.True(viewModel.NextPageCommand.CanExecute(null));
            viewModel.NextPageCommand.Execute(null);
            Assert.Equal(3, viewModel.CurrentPage);
        }
    }

    public class ConcurrencyPrintDialogService : IDialogService
    {
        private int _inUse = 0;
        public int PrintCallCount { get; private set; }
        public int DelayMs { get; set; } = 3000;
        public List<string> Messages { get; } = new();

        public bool ShowPrintDialog(System.Windows.Documents.FlowDocument document)
        {
            PrintCallCount++;
            if (Interlocked.CompareExchange(ref _inUse, 1, 0) != 0)
            {
                throw new InvalidOperationException("XPS writer is already in use");
            }
            try
            {
                if (DelayMs > 0)
                {
                    // Delay to allow concurrency
                    Thread.Sleep(DelayMs);
                }
                return true;
            }
            finally
            {
                _inUse = 0;
            }
        }

        public async Task<MessageBoxResult> ShowMessageAsync(string message, string title, MessageBoxButton button, MessageBoxImage icon)
        {
            Messages.Add(message);
            await Task.Delay(100);
            return MessageBoxResult.OK;
        }

        public string? ShowSaveFileDialog(string filter, string defaultFileName) => "dummy.csv";

        public void ShowReportWindow(
            MotorTestRecordModel motor,
            LiveChartsCore.ISeries[] noLoadSeries,
            LiveChartsCore.ISeries[] noiseSeries,
            LiveChartsCore.SkiaSharpView.Axis[] xAxes,
            LiveChartsCore.SkiaSharpView.Axis[] noLoadYAxes,
            LiveChartsCore.SkiaSharpView.Axis[] noiseYAxes,
            LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint tooltipBg,
            LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint tooltipText)
        {
        }

        public UserEditResult? ShowUserEditDialog(string title, string account, string name, string role, bool isEnabled) => null;

        public void SetClipboardText(string text) { }
    }

    public class MockDispatcherService : IDispatcherService
    {
        public void Invoke(Action action) => action();
        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
        public Task InvokeAsync(Func<Task> action) => action();
        public Task<T> InvokeAsync<T>(Func<T> func) => Task.FromResult(func());
    }
}
