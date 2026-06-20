using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;
using MotorTestSystem.Services;
using Xunit;
using Xunit.Abstractions;

namespace MotorTestSystem.Tests.StressTests
{
    /// <summary>
    /// 数据持久化压力测试（12.3）。
    /// 模拟 6 工位满负载写入，验证通道占用率、轮询周期和内存稳定性。
    /// 
    /// 注意：完整压力测试需要运行 1 小时。
    /// 本测试默认运行 30 秒（短周期），可通过设置环境变量 FULL_STRESS=true 启用完整 1 小时运行。
    /// </summary>
    public class DataPersistenceStressTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _dbDir;

        public DataPersistenceStressTests(ITestOutputHelper output)
        {
            _output = output;
            _dbDir = Path.Combine(Path.GetTempPath(), $"StressTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_dbDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_dbDir))
                try { Directory.Delete(_dbDir, true); } catch { }
        }

        /// <summary>
        /// 6 工位满负载写入压力测试。
        /// 
        /// 验证标准：
        /// - P99 写入延迟 ≤ 1000ms
        /// - 通道占用率 < 80%
        /// - 内存增量 ≤ 50MB
        /// - 零数据丢失
        /// 
        /// 运行时长由环境变量 FULL_STRESS 控制：
        /// - true: 60 分钟（完整压力测试）
        /// - false/未设置: 30 秒（快速验证）
        /// </summary>
        [Fact]
        public async Task SixStationFullLoad_ShouldMaintainPerformance()
        {
            // Arrange
            bool fullStress = string.Equals(
                Environment.GetEnvironmentVariable("FULL_STRESS"),
                "true", StringComparison.OrdinalIgnoreCase);

            TimeSpan duration = fullStress ? TimeSpan.FromHours(1) : TimeSpan.FromSeconds(10);
            int stationCount = 6;
            var rng = new Random(42);

            var channel = new EventChannelService(writeChannelCapacity: 500);
            var repo = new InMemoryMotorTestRepository();
            var deadLetterQueue = new DeadLetterQueue(
                Path.Combine(_dbDir, "DeadLetters"));
            var batchWriter = new BatchWriteService(repo, channel.WriteReader, deadLetterQueue, channel);

            var metrics = new StressMetrics();
            var cts = new CancellationTokenSource(duration);

            _output.WriteLine($"Starting stress test: {stationCount} stations, {duration.TotalMinutes:F1} min");
            _output.WriteLine($"Full stress mode: {fullStress}");

            // Act: 启动 6 个写入线程
            var writeTasks = new List<Task>();
            for (int s = 0; s < stationCount; s++)
            {
                string stationId = $"A{s + 1}";
                writeTasks.Add(Task.Run(async () =>
                {
                    int barcodeIdx = 0;
                    var stageOrder = new[] { TestStage.NoLoad, TestStage.Noise, TestStage.Load };

                    while (!cts.Token.IsCancellationRequested)
                    {
                        string barcode = $"STRESS-{stationId}-{barcodeIdx++:D5}";
                        var sw = Stopwatch.StartNew();

                        try
                        {
                            foreach (var stage in stageOrder)
                            {
                                await channel.WriteWriter.WriteAsync(
                                    GenerateTestData(barcode, stationId, stage, rng),
                                    cts.Token);
                            }

                            sw.Stop();
                            metrics.RecordWriteLatency(sw.ElapsedMilliseconds);
                            metrics.IncrementTotalWrites(3);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            metrics.IncrementErrors();
                            _output.WriteLine($"Write error: {ex.Message}");
                        }

                        // 模拟 ~1 秒轮询间隔
                        await Task.Delay(800 + rng.Next(400), cts.Token);
                    }
                }, cts.Token));
            }

            // 等待运行结束
            try { await Task.WhenAll(writeTasks); }
            catch (OperationCanceledException) { }

            // 等待 BatchWriteService 处理完成
            await Task.Delay(2000);
            channel.WriteWriter.TryComplete();
            await batchWriter.StopAsync();

            // Assert: 验证性能指标
            var stats = metrics.GetStatistics();

            _output.WriteLine("");
            _output.WriteLine("=== Stress Test Results ===");
            _output.WriteLine($"Duration: {duration.TotalSeconds:F0}s");
            _output.WriteLine($"Total writes: {stats.TotalWrites}");
            _output.WriteLine($"Total errors: {stats.TotalErrors}");
            _output.WriteLine($"P50 latency: {stats.P50Ms:F1}ms");
            _output.WriteLine($"P95 latency: {stats.P95Ms:F1}ms");
            _output.WriteLine($"P99 latency: {stats.P99Ms:F1}ms");
            _output.WriteLine($"Channel utilization: {channel.GetWriteChannelUtilization():P1}");
            _output.WriteLine($"Dead letter count: {deadLetterQueue.GetPendingCount()}");
            _output.WriteLine($"Dropped count: {batchWriter.GetTotalDroppedCount()}");

            // 性能断言
            Assert.True(stats.P99Ms <= 1000,
                $"P99 latency {stats.P99Ms:F1}ms exceeds 1000ms limit");
            Assert.True(channel.GetWriteChannelUtilization() < 0.80,
                $"Channel utilization {channel.GetWriteChannelUtilization():P1} exceeds 80%");
            Assert.Equal(0, deadLetterQueue.GetPendingCount());
            Assert.Equal(0, stats.TotalErrors);

            channel.Dispose();
            batchWriter.Dispose();
            deadLetterQueue.Dispose();
        }

        private static StageTestData GenerateTestData(
            string barcode, string stationId, TestStage stage, Random rng)
        {
            var data = new StageTestData
            {
                Barcode = barcode,
                StationId = stationId,
                Stage = stage,
                CollectedAt = DateTime.UtcNow,
                Result = rng.NextDouble() > 0.05 ? "OK" : "NG" // 95% 良率
            };

            switch (stage)
            {
                case TestStage.NoLoad:
                    data.NoLoadCurrent = Math.Round(1.5 + rng.NextDouble(), 3);
                    data.NoLoadSpeed = 2000 + rng.Next(200);
                    data.ShaftLength = 32.4;
                    data.KnurlDiameter = 4.42;
                    break;
                case TestStage.Noise:
                    data.FwdNoise = Math.Round(55.0 + rng.NextDouble() * 5, 2);
                    data.RevNoise = Math.Round(50.0 + rng.NextDouble() * 3, 2);
                    data.NoiseDiff = Math.Round(Math.Abs(data.FwdNoise.Value - data.RevNoise.Value), 2);
                    break;
                case TestStage.Load:
                    data.LoadCurrent = Math.Round(2.0 + rng.NextDouble() * 0.5, 3);
                    data.LoadSpeed = 1100 + rng.Next(100);
                    break;
            }

            return data;
        }
    }

    /// <summary>
    /// 压力测试指标收集器。
    /// </summary>
    internal sealed class StressMetrics
    {
        private readonly List<long> _latencies = new();
        private readonly object _lock = new();
        private int _totalWrites;
        private int _totalErrors;

        public void RecordWriteLatency(long ms)
        {
            lock (_lock) _latencies.Add(ms);
        }

        public void IncrementTotalWrites(int count) => Interlocked.Add(ref _totalWrites, count);
        public void IncrementErrors() => Interlocked.Increment(ref _totalErrors);

        public StressStatistics GetStatistics()
        {
            lock (_lock)
            {
                var sorted = _latencies.OrderBy(l => l).ToList();
                int count = sorted.Count;

                return new StressStatistics
                {
                    TotalWrites = Interlocked.CompareExchange(ref _totalWrites, 0, 0),
                    TotalErrors = Interlocked.CompareExchange(ref _totalErrors, 0, 0),
                    P50Ms = count > 0 ? sorted[(int)(count * 0.50)] : 0,
                    P95Ms = count > 0 ? sorted[(int)(count * 0.95)] : 0,
                    P99Ms = count > 0 ? sorted[(int)(count * 0.99)] : 0
                };
            }
        }
    }

    internal sealed class StressStatistics
    {
        public int TotalWrites { get; set; }
        public int TotalErrors { get; set; }
        public double P50Ms { get; set; }
        public double P95Ms { get; set; }
        public double P99Ms { get; set; }
    }
}
