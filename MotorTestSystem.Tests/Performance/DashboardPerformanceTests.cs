using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using MotorTestSystem.Models;
using MotorTestSystem.Services;
using MotorTestSystem.ViewModels;
using Xunit;

namespace MotorTestSystem.Tests.Performance
{
    public class DashboardPerformanceTests
    {
        [Fact]
        public async Task TestChartRefresh_ShouldMaintainSeriesReferences_BugCondition()
        {
            // Run on STA thread because DashboardViewModel uses DispatcherTimer and Dispatcher
            await StaHelper.RunAsync(async () =>
            {
                // Arrange
                var uiThreadId = Thread.CurrentThread.ManagedThreadId;
                var mockRepo = new MockMotorTestRepositoryForDashboard(uiThreadId);
                var configs = new ObservableCollection<StationConfig>();
                var dbContext = new SqlSugarDbContext();
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

                var viewModel = new DashboardViewModel(mockRepo, runtime);

                // Wait for any initial async loads to process
                await Task.Delay(200);
                StaHelper.DoEvents();

                var initialOutputSeries = viewModel.OutputSeries;
                var initialPassRateSeries = viewModel.PassRateSeries;
                var initialDefectSeries = viewModel.DefectDistributionSeries;

                // Act - trigger chart refresh
                var refreshMethod = typeof(DashboardViewModel).GetMethod("RefreshHourlyChartsAsync",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.NotNull(refreshMethod);
                
                await (Task)refreshMethod.Invoke(viewModel, null);

                // Assert - Bug Condition: The series collections/arrays must be identical references (incremental updates)
                // On unfixed code, this will FAIL because new arrays are created on every refresh.
                Assert.Same(initialOutputSeries, viewModel.OutputSeries);
                Assert.Same(initialPassRateSeries, viewModel.PassRateSeries);
                Assert.Same(initialDefectSeries, viewModel.DefectDistributionSeries);
            });
        }

        [Fact]
        public async Task TestChartDataCorrectness_Preservation()
        {
            await StaHelper.RunAsync(async () =>
            {
                // Arrange
                var uiThreadId = Thread.CurrentThread.ManagedThreadId;
                var mockRepo = new MockMotorTestRepositoryForDashboard(uiThreadId);
                
                // Add some mock records to count
                var now = DateTime.Now;
                mockRepo.Records.Add(new MotorTestResult { TestTime = now.AddHours(-1), FinalResult = "OK" });
                mockRepo.Records.Add(new MotorTestResult { TestTime = now.AddHours(-1), FinalResult = "NG" });
                mockRepo.Records.Add(new MotorTestResult { TestTime = now.AddHours(-2), FinalResult = "OK" });

                var configs = new ObservableCollection<StationConfig>();
                var dbContext = new SqlSugarDbContext();
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

                var viewModel = new DashboardViewModel(mockRepo, runtime);

                // Act
                var refreshMethod = typeof(DashboardViewModel).GetMethod("RefreshHourlyChartsAsync",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.NotNull(refreshMethod);
                await (Task)refreshMethod.Invoke(viewModel, null);

                // Assert
                Assert.NotNull(viewModel.OutputSeries);
                Assert.Equal(2, viewModel.OutputSeries.Count());

                var okSeries = viewModel.OutputSeries.ElementAt(0);
                var ngSeries = viewModel.OutputSeries.ElementAt(1);

                Assert.Equal("合格", okSeries.Name);
                Assert.Equal("不合格", ngSeries.Name);

                var okValues = (okSeries.Values as IEnumerable<int>)?.ToArray();
                var ngValues = (ngSeries.Values as IEnumerable<int>)?.ToArray();

                Assert.NotNull(okValues);
                Assert.NotNull(ngValues);
                Assert.Equal(8, okValues.Length);
                Assert.Equal(8, ngValues.Length);

                // In the last 8 hours, at now.AddHours(-1) we had 1 OK, 1 NG
                // and at now.AddHours(-2) we had 1 OK.
                // Since index 7 is now, index 6 is now.AddHours(-1), index 5 is now.AddHours(-2)
                Assert.Equal(1, okValues[6]);
                Assert.Equal(1, ngValues[6]);
                Assert.Equal(1, okValues[5]);
                Assert.Equal(0, ngValues[5]);
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

    public class MockMotorTestRepositoryForDashboard : IMotorTestRepository
    {
        private readonly int _uiThreadId;
        public List<MotorTestResult> Records { get; } = new();

        public MockMotorTestRepositoryForDashboard(int uiThreadId)
        {
            _uiThreadId = uiThreadId;
        }

        public Task UpsertStageResultAsync(StageTestData data, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MotorTestResult>> QueryAsync(MotorTestQuery query, CancellationToken cancellationToken = default)
        {
            var filtered = Records.Where(r => r.TestTime >= query.StartTime && r.TestTime <= query.EndTime).ToList();
            return Task.FromResult<IReadOnlyList<MotorTestResult>>(filtered);
        }

        public Task<IReadOnlyList<MotorTestResult>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<MotorTestResult>>(Records.Take(count).ToList());
        }

        public Task<ProductionSummary> GetSummaryAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
        {
            var summary = new ProductionSummary
            {
                TotalChecked = Records.Count,
                OkCount = Records.Count(r => r.FinalResult == "OK"),
                NgCount = Records.Count(r => r.FinalResult == "NG")
            };
            return Task.FromResult(summary);
        }

        public Task<DefectSummary> GetDefectSummaryAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DefectSummary());
        }

        public Task<IReadOnlyList<FaultRankItem>> GetFaultRankingAsync(DateTime startTime, DateTime endTime, int topN = 5, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<FaultRankItem>>(new List<FaultRankItem>());
        }

        public Task BulkUpsertAsync(IEnumerable<StageTestData> results, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
