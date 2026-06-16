using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 基于文件系统的死信队列实现。
    /// 每个死信条目序列化为一个 JSON 文件，存储在指定目录。
    /// 文件名格式：{yyyyMMdd-HHmmss-fffffff}_{guid}.json
    /// </summary>
    public sealed class DeadLetterQueue : IDeadLetterQueue, IDisposable
    {
        private readonly string _storagePath;
        private readonly DeadLetterSerializer _serializer;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private int _pendingCount;

        public string StoragePath => _storagePath;

        public DeadLetterQueue(string? storagePath = null, DeadLetterSerializer? serializer = null)
        {
            _storagePath = string.IsNullOrWhiteSpace(storagePath)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeadLetterQueue")
                : storagePath;

            _serializer = serializer ?? new DeadLetterSerializer();

            // 确保目录存在
            Directory.CreateDirectory(_storagePath);

            // 初始化计数
            _pendingCount = Directory.EnumerateFiles(_storagePath, "*.json")
                .Count(f => DeadLetterParser.IsValidFileName(Path.GetFileName(f)));
        }

        public async Task EnqueueAsync(
            IReadOnlyList<StageTestData> failedData,
            string errorMessage,
            string? exceptionType = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(failedData);
            ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

            var metadata = new DeadLetterMetadata
            {
                Timestamp = DateTime.UtcNow,
                Stage = "BulkUpsert",
                ErrorMessage = errorMessage,
                ExceptionType = exceptionType,
                FailedData = failedData.ToList(),
                StationIds = failedData
                    .Select(d => d.StationId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct()
                    .ToList()
            };

            string fileName = DeadLetterParser.GenerateFileName(metadata.Timestamp, metadata.Id);
            string filePath = Path.Combine(_storagePath, fileName);

            await _serializer.SerializeToFileAsync(filePath, metadata, cancellationToken).ConfigureAwait(false);

            Interlocked.Increment(ref _pendingCount);
        }

        public async Task<IReadOnlyList<DeadLetterEntry>> ScanAsync(CancellationToken cancellationToken = default)
        {
            var entries = new List<DeadLetterEntry>();

            await Task.Run(() =>
            {
                var files = Directory.EnumerateFiles(_storagePath, "*.json")
                    .Select(fp => new FileInfo(fp))
                    .Where(fi => DeadLetterParser.IsValidFileName(fi.Name))
                    .OrderBy(fi =>
                    {
                        var ts = DeadLetterParser.ParseTimestamp(fi.Name);
                        return ts ?? fi.CreationTimeUtc;
                    })
                    .ToList();

                foreach (var fi in files)
                {
                    var ts = DeadLetterParser.ParseTimestamp(fi.Name);
                    entries.Add(new DeadLetterEntry
                    {
                        FileName = fi.Name,
                        FilePath = fi.FullName,
                        Timestamp = ts ?? fi.CreationTimeUtc,
                        FileSize = fi.Length
                    });
                }
            }, cancellationToken).ConfigureAwait(false);

            return entries;
        }

        public async Task<bool> RetryAsync(
            DeadLetterEntry entry,
            Func<IReadOnlyList<StageTestData>, CancellationToken, Task> retryCallback,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentNullException.ThrowIfNull(retryCallback);

            // 反序列化元数据（如果尚未加载）
            var metadata = entry.Metadata ?? await _serializer.DeserializeFromFileAsync(entry.FilePath, cancellationToken).ConfigureAwait(false);

            try
            {
                await retryCallback(metadata.FailedData.AsReadOnly(), cancellationToken).ConfigureAwait(false);

                // 重试成功，删除死信文件
                await DeleteAsync(entry, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                // 重试失败，更新重试计数
                metadata.RetryCount++;
                metadata.LastRetryAt = DateTime.UtcNow;
                metadata.ErrorMessage = ex.Message;
                metadata.ExceptionType = ex.GetType().FullName;

                entry.Metadata = metadata;

                // 写回更新后的元数据
                await _serializer.SerializeToFileAsync(entry.FilePath, metadata, cancellationToken).ConfigureAwait(false);

                return false;
            }
        }

        public Task DeleteAsync(DeadLetterEntry entry, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entry);

            if (File.Exists(entry.FilePath))
            {
                File.Delete(entry.FilePath);
            }

            Interlocked.Decrement(ref _pendingCount);
            return Task.CompletedTask;
        }

        public Task MarkAsFailedAsync(DeadLetterEntry entry, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entry);

            if (File.Exists(entry.FilePath))
            {
                string failedPath = entry.FilePath + ".failed";
                File.Move(entry.FilePath, failedPath);
            }

            Interlocked.Decrement(ref _pendingCount);
            return Task.CompletedTask;
        }

        public int GetPendingCount()
        {
            return Math.Max(0, Interlocked.CompareExchange(ref _pendingCount, 0, 0));
        }

        public void Dispose()
        {
            _lock.Dispose();
        }
    }
}
