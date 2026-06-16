using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MotorTestSystem.Services;
using Xunit;

namespace MotorTestSystem.Tests.PropertyTests
{
    /// <summary>
    /// Property 4: 死信队列文件处理的时间顺序性
    /// Validates: Requirements 6.2
    /// 生成随机时间戳的文件，验证扫描结果按时间戳升序排列。
    /// </summary>
    public class DeadLetterTimeOrderingTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly DeadLetterQueue _queue;
        private readonly DeadLetterSerializer _serializer;

        public DeadLetterTimeOrderingTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"DLQ_Test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _serializer = new DeadLetterSerializer();
            _queue = new DeadLetterQueue(_tempDir, _serializer);
        }

        public void Dispose()
        {
            _queue.Dispose();
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        /// <summary>
        /// 验证扫描结果按时间戳升序排列（使用随机时间戳生成文件）。
        /// 测试 100 次迭代，每次生成 5-20 个随机时间戳的文件。
        /// </summary>
        [Fact]
        public async Task ScanAsync_ShouldReturnFilesInChronologicalOrder()
        {
            var rng = new Random(42);

            for (int iteration = 0; iteration < 100; iteration++)
            {
                // Arrange: 清理目录
                foreach (var f in Directory.GetFiles(_tempDir, "*.json"))
                    File.Delete(f);
                foreach (var f in Directory.GetFiles(_tempDir, "*.failed"))
                    File.Delete(f);

                int fileCount = rng.Next(5, 21);
                var timestamps = new List<DateTime>();

                for (int i = 0; i < fileCount; i++)
                {
                    // 生成随机时间戳（过去 7 天内）
                    var ts = DateTime.UtcNow
                        .AddDays(-rng.Next(0, 7))
                        .AddHours(-rng.Next(0, 24))
                        .AddMinutes(-rng.Next(0, 60))
                        .AddSeconds(-rng.Next(0, 60))
                        .AddTicks(-rng.Next(0, 10000000));

                    string fileName = DeadLetterParser.GenerateFileName(ts, Guid.NewGuid().ToString("N"));
                    string filePath = Path.Combine(_tempDir, fileName);

                    // 写入一个最小化的合法 JSON 文件
                    var metadata = new MotorTestSystem.Models.DeadLetterMetadata
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Timestamp = ts,
                        Stage = "BulkUpsert",
                        ErrorMessage = "Test",
                        FailedData = new()
                    };
                    await _serializer.SerializeToFileAsync(filePath, metadata);
                    timestamps.Add(ts);
                }

                // Act
                var entries = await _queue.ScanAsync();

                // Assert: 按时间戳升序排列
                Assert.Equal(fileCount, entries.Count);
                for (int i = 1; i < entries.Count; i++)
                {
                    Assert.True(
                        entries[i - 1].Timestamp <= entries[i].Timestamp,
                        $"Iteration {iteration}: Entry[{i - 1}] ({entries[i - 1].Timestamp:O}) should be <= Entry[{i}] ({entries[i].Timestamp:O})"
                    );
                }
            }
        }

        /// <summary>
        /// 验证同名时间戳的文件按 GUID 顺序排列（时间戳相同时）。
        /// </summary>
        [Fact]
        public async Task ScanAsync_WithSameTimestamp_ShouldStillReturnSorted()
        {
            // Arrange
            var now = DateTime.UtcNow;
            for (int i = 0; i < 5; i++)
            {
                string fileName = DeadLetterParser.GenerateFileName(now, Guid.NewGuid().ToString("N"));
                string filePath = Path.Combine(_tempDir, fileName);
                var metadata = new MotorTestSystem.Models.DeadLetterMetadata
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Timestamp = now,
                    Stage = "BulkUpsert",
                    ErrorMessage = "Test",
                    FailedData = new()
                };
                await _serializer.SerializeToFileAsync(filePath, metadata);
            }

            // Act
            var entries = await _queue.ScanAsync();

            // Assert: 5 个文件都应该被扫描到
            Assert.Equal(5, entries.Count);
        }

        /// <summary>
        /// 验证 .failed 后缀的文件不会被扫描为待处理条目。
        /// </summary>
        [Fact]
        public async Task ScanAsync_ShouldExcludeFailedFiles()
        {
            // Arrange: 创建 3 个正常文件和 2 个失败文件
            for (int i = 0; i < 3; i++)
            {
                var ts = DateTime.UtcNow.AddMinutes(-i);
                string fileName = DeadLetterParser.GenerateFileName(ts, Guid.NewGuid().ToString("N"));
                string filePath = Path.Combine(_tempDir, fileName);
                var metadata = new MotorTestSystem.Models.DeadLetterMetadata
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Timestamp = ts,
                    Stage = "BulkUpsert",
                    ErrorMessage = "Test",
                    FailedData = new()
                };
                await _serializer.SerializeToFileAsync(filePath, metadata);
            }

            // 创建 .failed 文件
            for (int i = 0; i < 2; i++)
            {
                var ts = DateTime.UtcNow.AddMinutes(-10 - i);
                string fileName = DeadLetterParser.GenerateFileName(ts, Guid.NewGuid().ToString("N")) + ".failed";
                string filePath = Path.Combine(_tempDir, fileName);
                await File.WriteAllTextAsync(filePath, "{}");
            }

            // Act
            var entries = await _queue.ScanAsync();

            // Assert: 只应返回 3 个正常文件
            Assert.Equal(3, entries.Count);
        }
    }
}
