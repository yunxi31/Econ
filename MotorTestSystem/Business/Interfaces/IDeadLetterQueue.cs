using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 死信队列 — 持久化存储写入失败的数据，支持后续重试。
    /// </summary>
    public interface IDeadLetterQueue : IDisposable
    {
        /// <summary>
        /// 将失败的批次加入死信队列（序列化为 JSON 文件）。
        /// </summary>
        Task EnqueueAsync(IReadOnlyList<StageTestData> failedData, string errorMessage, string? exceptionType = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 扫描死信队列目录，返回所有未处理的死信条目（按时间戳升序）。
        /// </summary>
        Task<IReadOnlyList<DeadLetterEntry>> ScanAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 重试指定死信条目：反序列化并调用重试回调。
        /// </summary>
        Task<bool> RetryAsync(DeadLetterEntry entry, Func<IReadOnlyList<StageTestData>, CancellationToken, Task> retryCallback, CancellationToken cancellationToken = default);

        /// <summary>
        /// 删除指定死信条目（文件）。
        /// </summary>
        Task DeleteAsync(DeadLetterEntry entry, CancellationToken cancellationToken = default);

        /// <summary>
        /// 将指定死信条目标记为永久失败（文件重命名为 .failed 后缀）。
        /// </summary>
        Task MarkAsFailedAsync(DeadLetterEntry entry, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取当前未处理的死信文件数量。
        /// </summary>
        int GetPendingCount();

        /// <summary>
        /// 死信存储目录路径。
        /// </summary>
        string StoragePath { get; }
    }

    /// <summary>
    /// 死信队列条目（表示一个序列化的死信文件）。
    /// </summary>
    public sealed class DeadLetterEntry
    {
        /// <summary>文件名（不含路径）</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>完整文件路径</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>创建时间戳（从文件名解析）</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>反序列化后的元数据（若已解析）</summary>
        public DeadLetterMetadata? Metadata { get; set; }

        /// <summary>文件大小（字节）</summary>
        public long FileSize { get; set; }
    }
}
