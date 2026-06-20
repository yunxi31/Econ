using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using MotorTestSystem.Models;
using MotorTestSystem.Services;

namespace MotorTestSystem.Tests.Benchmarks
{
    /// <summary>
    /// 数据持久化性能基准测试（12.4）。
    /// 使用 BenchmarkDotNet 验证关键操作性能。
    /// 运行方式：dotnet run -c Release -f net8.0-windows -- --benchmarks
    /// 注意：SQLite 基准需要 SqlSugarDbContext 实例，暂不在此处运行（依赖静态 DbPath）。
    /// </summary>
    [MemoryDiagnoser]
    [MinColumn, MaxColumn, MeanColumn, MedianColumn]
    public class DataPersistenceBenchmarks
    {
        private InMemoryMotorTestRepository? _memoryRepo;
        private EventChannelService? _channel;

        private readonly List<StageTestData> _testBatch = new();

        [Params(1, 10, 50)]
        public int BatchSize { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _memoryRepo = new InMemoryMotorTestRepository();
            _channel = new EventChannelService(writeChannelCapacity: 500);

            for (int i = 0; i < 50; i++)
            {
                _testBatch.Add(new StageTestData
                {
                    Barcode = $"BENCH-{i:D4}",
                    StationId = "A1",
                    Stage = (TestStage)(i % 3),
                    CollectedAt = DateTime.UtcNow,
                    Result = "OK",
                    NoLoadCurrent = 1.5 + i * 0.01,
                    NoLoadSpeed = 2000 + i
                });
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _channel?.Dispose();
        }

        [Benchmark(Baseline = true)]
        public async Task SingleUpsert_InMemory()
        {
            await _memoryRepo!.UpsertStageResultAsync(_testBatch[0]);
        }

        [Benchmark]
        public async Task BatchUpsert_InMemory()
        {
            await _memoryRepo!.BulkUpsertAsync(_testBatch.Take(BatchSize));
        }

        [Benchmark]
        public async Task NotificationWrite()
        {
            for (int i = 0; i < BatchSize; i++)
            {
                await _channel!.NotificationWriter.WriteAsync(
                    new NotificationItem { Title = "B", Content = "T" });
            }
        }
    }
}
