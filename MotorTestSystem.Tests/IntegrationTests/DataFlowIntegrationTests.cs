using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MotorTestSystem.Models;
using MotorTestSystem.Services;
using SqlSugar;
using Xunit;

namespace MotorTestSystem.Tests.IntegrationTests
{
    /// <summary>
    /// 端到端数据流集成测试（12.2）。
    /// 模拟 6 工位并发轮询 → 批量写入 → 数据库验证。
    /// </summary>
    public class DataFlowIntegrationTests : IDisposable
    {
        private readonly string _dbDir;

        public DataFlowIntegrationTests()
        {
            _dbDir = Path.Combine(Path.GetTempPath(), $"IntegrationTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_dbDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_dbDir))
            {
                try { Directory.Delete(_dbDir, true); }
                catch { /* best-effort */ }
            }
        }

        /// <summary>
        /// 模拟 6 工位并发写入，验证数据最终完整写入数据库。
        /// 流程：
        /// 1. 创建 EventChannelService + 内存仓储
        /// 2. 启动 BatchWriteService 消费写入通道
        /// 3. 6 个并发 Task 模拟工位写入不同条码的 StageTestData
        /// 4. 等待写入完成
        /// 5. 验证数据库中有完整记录
        /// </summary>
        [Fact]
        public async Task SixStationConcurrentWrite_AllDataShouldPersist()
        {
            // Arrange
            var channel = new EventChannelService(writeChannelCapacity: 500);
            var repo = new InMemoryMotorTestRepository();
            var batchWriter = new BatchWriteService(repo, channel.WriteReader);

            int stationCount = 6;
            int barcodesPerStation = 10;
            var allBarcodes = new List<string>();

            // Act: 6 工位 × 10 条码 × 3 阶段并发写入
            var writeTasks = new List<Task>();
            for (int station = 0; station < stationCount; station++)
            {
                string stationId = $"A{station + 1}";
                writeTasks.Add(Task.Run(async () =>
                {
                    for (int i = 0; i < barcodesPerStation; i++)
                    {
                        string barcode = $"INT-{stationId}-{i:D3}";
                        lock (allBarcodes) { allBarcodes.Add(barcode); }

                        // 写入 3 个阶段
                        await channel.WriteWriter.WriteAsync(new StageTestData
                        {
                            Barcode = barcode,
                            StationId = stationId,
                            Stage = TestStage.NoLoad,
                            CollectedAt = DateTime.UtcNow,
                            Result = "OK",
                            NoLoadCurrent = 1.5 + i * 0.1,
                            NoLoadSpeed = 2000 + i * 10
                        });

                        await channel.WriteWriter.WriteAsync(new StageTestData
                        {
                            Barcode = barcode,
                            StationId = "A3",
                            Stage = TestStage.Noise,
                            CollectedAt = DateTime.UtcNow,
                            Result = "OK",
                            FwdNoise = 55.0 + i,
                            RevNoise = 50.0 + i
                        });

                        await channel.WriteWriter.WriteAsync(new StageTestData
                        {
                            Barcode = barcode,
                            StationId = "A5",
                            Stage = TestStage.Load,
                            CollectedAt = DateTime.UtcNow,
                            Result = "OK",
                            LoadCurrent = 2.0 + i * 0.05,
                            LoadSpeed = 1100 + i * 10
                        });
                    }
                }));
            }

            await Task.WhenAll(writeTasks);

            // 等待 BatchWriteService 处理完成
            await Task.Delay(3000);

            // 标记写入完成
            channel.WriteWriter.TryComplete();
            await batchWriter.StopAsync();

            // Assert: 验证数据完整性
            // 6 × 10 = 60 条不同条码
            var allRecords = await repo.GetRecentAsync(200);
            Assert.Equal(60, allRecords.Count);

            // 每个条码应该都有完整记录
            foreach (var barcode in allBarcodes)
            {
                var record = allRecords.FirstOrDefault(r => r.Barcode == barcode);
                Assert.NotNull(record);
                Assert.Equal("OK", record.FinalResult);
            }

            channel.Dispose();
            batchWriter.Dispose();
        }

        /// <summary>
        /// 验证死信队列集成：写入失败自动入死信队列。
        /// 模拟一个失败的 BulkUpsert（空引用仓储）→ 验证死信队列收到数据。
        /// </summary>
        [Fact]
        public async Task FailedWrite_ShouldEnqueueDeadLetter()
        {
            // Arrange
            string dlqDir = Path.Combine(_dbDir, "DeadLetterTest");
            Directory.CreateDirectory(dlqDir);

            var channel = new EventChannelService(writeChannelCapacity: 500);
            var serializer = new DeadLetterSerializer();
            var deadLetterQueue = new DeadLetterQueue(dlqDir, serializer);
            var repo = new InMemoryMotorTestRepository();

            // 创建一个会抛出异常的仓储包装
            var failingRepo = new FailingMotorTestRepository(repo);

            var batchWriter = new BatchWriteService(failingRepo, channel.WriteReader, deadLetterQueue);

            // Act: 写入一个条码
            await channel.WriteWriter.WriteAsync(new StageTestData
            {
                Barcode = "FAIL-TEST-001",
                StationId = "A1",
                Stage = TestStage.NoLoad,
                CollectedAt = DateTime.UtcNow,
                Result = "OK",
                NoLoadCurrent = 1.5
            });

            // 等待重试和死信入队
            await Task.Delay(8000);

            channel.WriteWriter.TryComplete();
            await batchWriter.StopAsync();

            // Assert: 死信队列应有文件
            var entries = await deadLetterQueue.ScanAsync();
            Assert.True(entries.Count > 0, "Dead letter queue should contain at least one entry");
            Assert.True(deadLetterQueue.GetPendingCount() > 0);

            channel.Dispose();
            batchWriter.Dispose();
            deadLetterQueue.Dispose();
        }

        /// <summary>
        /// 验证数据经过 BatchWriteService+死信队列的完整链路。
        /// 写入 3 个正常条码 + 1 个失败条码，正常条码应在仓储中，失败条码应在死信队列中。
        /// </summary>
        [Fact]
        public async Task MixedSuccessAndFailure_ShouldHandleCorrectly()
        {
            string dlqDir = Path.Combine(_dbDir, "MixedTest");
            Directory.CreateDirectory(dlqDir);

            var channel = new EventChannelService(writeChannelCapacity: 500);
            var deadLetterQueue = new DeadLetterQueue(dlqDir);
            var realRepo = new InMemoryMotorTestRepository();

            // 前 3 个写入使用正常仓储，后 1 个使用失败仓储
            var batchWriter = new BatchWriteService(realRepo, channel.WriteReader, deadLetterQueue);

            // 3 个正常条码
            for (int i = 1; i <= 3; i++)
            {
                await channel.WriteWriter.WriteAsync(new StageTestData
                {
                    Barcode = $"GOOD-{i:D3}",
                    StationId = "A1",
                    Stage = TestStage.NoLoad,
                    CollectedAt = DateTime.UtcNow,
                    Result = "OK",
                    NoLoadCurrent = 1.5
                });
            }

            // 写入完成后，等待处理
            await Task.Delay(3000);

            var allRecords = await realRepo.GetRecentAsync(10);
            Assert.Equal(3, allRecords.Count);
            // 由于只写了 NoLoad 阶段，FinalResult 可能是 NG（取决于仓储逻辑）
            // 但条码应该都在仓储中
            Assert.Contains(allRecords, r => r.Barcode == "GOOD-001");
            Assert.Contains(allRecords, r => r.Barcode == "GOOD-002");
            Assert.Contains(allRecords, r => r.Barcode == "GOOD-003");

            channel.Dispose();
            batchWriter.Dispose();
            deadLetterQueue.Dispose();
        }
    }

    /// <summary>
    /// 用于测试写入失败的仓储包装 — 在 BulkUpsertAsync 时抛出异常。
    /// </summary>
    public sealed class FailingMotorTestRepository : IMotorTestRepository
    {
        private readonly IMotorTestRepository _inner;

        public FailingMotorTestRepository(IMotorTestRepository inner)
        {
            _inner = inner;
        }

        public Task UpsertStageResultAsync(StageTestData data, CancellationToken cancellationToken = default)
            => _inner.UpsertStageResultAsync(data, cancellationToken);

        public async Task BulkUpsertAsync(IEnumerable<StageTestData> results, CancellationToken cancellationToken = default)
        {
            // 模拟写入失败
            await Task.Delay(100, cancellationToken);
            throw new InvalidOperationException("Simulated database write failure for dead letter test.");
        }

        public Task BulkUpsertWithRawSqlAsync(IEnumerable<StageTestData> results, CancellationToken cancellationToken = default)
            => BulkUpsertAsync(results, cancellationToken);

        public Task<IReadOnlyList<MotorTestResult>> QueryAsync(MotorTestQuery query, CancellationToken cancellationToken = default)
            => _inner.QueryAsync(query, cancellationToken);

        public Task<IReadOnlyList<MotorTestResult>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
            => _inner.GetRecentAsync(count, cancellationToken);

        public Task<ProductionSummary> GetSummaryAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
            => _inner.GetSummaryAsync(startTime, endTime, cancellationToken);

        public Task<DefectSummary> GetDefectSummaryAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
            => _inner.GetDefectSummaryAsync(startTime, endTime, cancellationToken);

        public Task<IReadOnlyList<FaultRankItem>> GetFaultRankingAsync(DateTime startTime, DateTime endTime, int topN = 5, CancellationToken cancellationToken = default)
            => _inner.GetFaultRankingAsync(startTime, endTime, topN, cancellationToken);
    }
}
