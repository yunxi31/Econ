using System;
using System.Collections.Generic;

namespace MotorTestSystem.Models
{
    /// <summary>
    /// 死信队列元数据，记录写入失败的测试数据批次及失败原因。
    /// </summary>
    public sealed class DeadLetterMetadata
    {
        /// <summary>唯一标识</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>原始时间戳（UTC）</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>写入阶段（如 "BulkUpsert"）</summary>
        public string Stage { get; set; } = string.Empty;

        /// <summary>失败原因</summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>失败时的异常类型</summary>
        public string? ExceptionType { get; set; }

        /// <summary>失败的测试数据批次</summary>
        public List<StageTestData> FailedData { get; set; } = new();

        /// <summary>来源工位 ID 列表</summary>
        public List<string> StationIds { get; set; } = new();

        /// <summary>重试次数</summary>
        public int RetryCount { get; set; }

        /// <summary>最后重试时间</summary>
        public DateTime? LastRetryAt { get; set; }

        /// <summary>是否已标记为永久失败</summary>
        public bool IsFailed { get; set; }
    }
}
