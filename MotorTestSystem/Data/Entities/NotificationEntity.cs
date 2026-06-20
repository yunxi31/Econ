using SqlSugar;
using System;

namespace MotorTestSystem.Models.Entities
{
    /// <summary>
    /// 通知记录实体 — 对应数据库表
    /// </summary>
    [SugarTable("Notifications")]
    public class NotificationEntity
    {
        /// <summary>唯一标识</summary>
        [SugarColumn(IsPrimaryKey = true, Length = 20)]
        public string Id { get; set; } = string.Empty;

        /// <summary>通知标题</summary>
        [SugarColumn(Length = 200, IsNullable = false)]
        public string Title { get; set; } = string.Empty;

        /// <summary>通知内容详情</summary>
        [SugarColumn(ColumnDataType = "TEXT", IsNullable = false)]
        public string Content { get; set; } = string.Empty;

        /// <summary>创建时间</summary>
        [SugarColumn(IsNullable = false)]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>通知类型 0=Alarm 1=Maintenance 2=System</summary>
        [SugarColumn(IsNullable = false)]
        public int Type { get; set; } = 2;

        /// <summary>严重程度 0=Info 1=Warning 2=Critical</summary>
        [SugarColumn(IsNullable = false)]
        public int Severity { get; set; } = 0;

        /// <summary>是否已读</summary>
        [SugarColumn(IsNullable = false)]
        public bool IsRead { get; set; }

        /// <summary>关联来源</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? Source { get; set; }
    }
}
