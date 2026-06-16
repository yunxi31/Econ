using System;
using SqlSugar;

namespace MotorTestSystem.Models
{
    /// <summary>
    /// 云端同步队列实体，记录待同步到 MES/云端系统的测试记录。
    /// P3 功能，当前仅创建数据模型供后续迭代使用。
    /// </summary>
    [SugarTable("SyncQueue")]
    public sealed class SyncQueueEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>关联的测试记录 ID</summary>
        public string RecordId { get; set; } = string.Empty;

        /// <summary>同步状态：0=Pending, 1=Syncing, 2=Completed, 3=Failed</summary>
        public int SyncStatus { get; set; }

        /// <summary>已重试次数</summary>
        public int RetryCount { get; set; }

        /// <summary>最后同步尝试时间</summary>
        public DateTime? LastAttempt { get; set; }

        /// <summary>创建时间</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
