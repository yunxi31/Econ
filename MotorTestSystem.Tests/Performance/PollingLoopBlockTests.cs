using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;
using MotorTestSystem.Services;
using Xunit;

namespace MotorTestSystem.Tests.Performance
{
    public class PollingLoopBlockTests
    {
        [Fact]
        public async Task TestPollingLoopBlock_ShouldShowJitterUnderHighDbLatency()
        {
            // Arrange
            var mockRepo = new JitterMockRepository { LatencyMs = 150 };
            var config = new StationConfig { Id = "A1", PlcModel = "S7-1200", IpAddress = "127.0.0.1" };
            var mockFactory = new JitterMockPlcClientFactory();
            
            // 50ms poll interval
            using var pollingService = new PlcPollingService(
                new[] { config }, 
                mockRepo, 
                mockFactory, 
                TimeSpan.FromMilliseconds(50)
            );

            // Act
            pollingService.Start();
            await Task.Delay(1500); // Let it run for 1.5 seconds
            await pollingService.StopAsync();

            // Analyze jitter
            var client = mockFactory.CreatedClient;
            Assert.NotNull(client);

            var readTimes = client.ReadTimes;
            Assert.True(readTimes.Count >= 3, "Expected at least 3 reads to calculate jitter.");

            var intervals = new List<double>();
            for (int i = 1; i < readTimes.Count; i++)
            {
                intervals.Add((readTimes[i] - readTimes[i - 1]).TotalMilliseconds);
            }

            double mean = intervals.Average();
            double sumOfSquares = intervals.Select(val => (val - mean) * (val - mean)).Sum();
            double standardDeviation = Math.Sqrt(sumOfSquares / intervals.Count);

            // Assert
            // With 150ms DB latency on alternate reads, intervals should alternate between ~50ms and ~200ms.
            // Standard deviation (jitter) will be high (> 40ms).
            Assert.True(standardDeviation > 40, 
                $"Expected loop jitter (standard deviation) to be > 40ms under high DB latency, but was {standardDeviation:F2}ms");
        }

        [Fact]
        public async Task TestDbLatencyImpact_ShouldDemonstrateDirectLoopDeceleration()
        {
            // Arrange
            // Case 1: Low latency (0ms)
            var mockRepoLow = new JitterMockRepository { LatencyMs = 0 };
            var configLow = new StationConfig { Id = "A1", PlcModel = "S7-1200", IpAddress = "127.0.0.1" };
            var mockFactoryLow = new JitterMockPlcClientFactory();
            using var pollingLow = new PlcPollingService(new[] { configLow }, mockRepoLow, mockFactoryLow, TimeSpan.FromMilliseconds(10));

            // Case 2: High latency (200ms)
            var mockRepoHigh = new JitterMockRepository { LatencyMs = 200 };
            var configHigh = new StationConfig { Id = "A2", PlcModel = "S7-1200", IpAddress = "127.0.0.1" };
            var mockFactoryHigh = new JitterMockPlcClientFactory();
            using var pollingHigh = new PlcPollingService(new[] { configHigh }, mockRepoHigh, mockFactoryHigh, TimeSpan.FromMilliseconds(10));

            // Act
            pollingLow.Start();
            pollingHigh.Start();
            await Task.Delay(1000); // Run both for 1 second
            await pollingLow.StopAsync();
            await pollingHigh.StopAsync();

            int lowLatencyPollCount = mockFactoryLow.CreatedClient.ReadTimes.Count;
            int highLatencyPollCount = mockFactoryHigh.CreatedClient.ReadTimes.Count;

            // Assert
            // With low latency, we should have around 30-50 polls in 1s (alternating completion signal which writes to DB).
            // With 200ms latency on alternating completion signals, the polling rate should decelerate significantly.
            // We assert that the low latency poll count is at least 2x the high latency poll count.
            Assert.True(lowLatencyPollCount > 2 * highLatencyPollCount, 
                $"Expected low latency poll count ({lowLatencyPollCount}) to be > 2x high latency poll count ({highLatencyPollCount}) due to direct deceleration.");
        }
    }

    public class JitterMockRepository : IMotorTestRepository
    {
        public int LatencyMs { get; set; }

        public async Task UpsertStageResultAsync(StageTestData data, CancellationToken cancellationToken = default)
        {
            if (LatencyMs > 0)
            {
                await Task.Delay(LatencyMs, cancellationToken);
            }
        }

        public Task<IReadOnlyList<MotorTestResult>> QueryAsync(MotorTestQuery query, CancellationToken cancellationToken = default) => 
            Task.FromResult<IReadOnlyList<MotorTestResult>>(new List<MotorTestResult>());

        public Task<IReadOnlyList<MotorTestResult>> GetRecentAsync(int count, CancellationToken cancellationToken = default) => 
            Task.FromResult<IReadOnlyList<MotorTestResult>>(new List<MotorTestResult>());

        public Task<ProductionSummary> GetSummaryAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default) => 
            Task.FromResult(new ProductionSummary());

        public Task<DefectSummary> GetDefectSummaryAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default) => 
            Task.FromResult(new DefectSummary());

        public Task<IReadOnlyList<FaultRankItem>> GetFaultRankingAsync(DateTime startTime, DateTime endTime, int topN = 5, CancellationToken cancellationToken = default) => 
            Task.FromResult<IReadOnlyList<FaultRankItem>>(new List<FaultRankItem>());

        public async Task BulkUpsertAsync(IEnumerable<StageTestData> results, CancellationToken cancellationToken = default)
        {
            if (LatencyMs > 0)
            {
                await Task.Delay(LatencyMs, cancellationToken);
            }
        }

        public Task BulkUpsertWithRawSqlAsync(IEnumerable<StageTestData> results, CancellationToken cancellationToken = default)
        {
            return BulkUpsertAsync(results, cancellationToken);
        }
    }

    public class JitterMockPlcClient : IPlcClient
    {
        public StationConfig Config { get; }
        public List<DateTime> ReadTimes { get; } = new();

        public JitterMockPlcClient(StationConfig config)
        {
            Config = config;
        }

        public Task<bool> ConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<StationSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default)
        {
            lock (ReadTimes)
            {
                ReadTimes.Add(DateTime.UtcNow);
            }

            // Toggle completion signal to simulate sporadic test completion
            bool completionSignal = ReadTimes.Count % 2 == 0;

            var snapshot = new StationSnapshot
            {
                StationId = Config.Id,
                IsOnline = true,
                Status = 1,
                CompletionSignal = completionSignal,
                CompletedData = completionSignal ? new StageTestData
                {
                    Barcode = $"BARCODE-{Config.Id}-{ReadTimes.Count}",
                    StationId = Config.Id,
                    Stage = TestStage.NoLoad,
                    Result = "OK"
                } : null
            };

            return Task.FromResult(snapshot);
        }

        public Task ResetCompletionSignalAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Dispose() { }
    }

    public class JitterMockPlcClientFactory : IPlcClientFactory
    {
        public JitterMockPlcClient CreatedClient { get; private set; }

        public IPlcClient Create(StationConfig config)
        {
            CreatedClient = new JitterMockPlcClient(config);
            return CreatedClient;
        }
    }
}
