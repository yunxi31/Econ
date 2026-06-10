using System;

namespace MotorTestSystem.Models
{
    /// <summary>
    /// 通知类型枚举
    /// </summary>
    public enum NotificationType
    {
        Alarm,       // 报警
        Maintenance, // 维护
        System       // 系统
    }

    /// <summary>
    /// 通知严重程度
    /// </summary>
    public enum NotificationSeverity
    {
        Info,     // 信息
        Warning,  // 警告
        Critical  // 严重
    }

    /// <summary>
    /// 通知条目模型
    /// </summary>
    public class NotificationItem
    {
        /// <summary>唯一标识</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>通知标题</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>通知内容详情</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>通知时间</summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>通知类型</summary>
        public NotificationType Type { get; set; } = NotificationType.System;

        /// <summary>严重程度</summary>
        public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

        /// <summary>是否已读</summary>
        public bool IsRead { get; set; }

        /// <summary>关联来源（如工位ID、条码等）</summary>
        public string? Source { get; set; }

        // ---- 显示辅助属性 ----

        /// <summary>类型中文名（用于 UI 筛选器绑定）</summary>
        public string TypeDisplay => Type switch
        {
            NotificationType.Alarm => "报警",
            NotificationType.Maintenance => "维护",
            NotificationType.System => "系统",
            _ => "未知"
        };

        /// <summary>格式化时间戳</summary>
        public string Timestamp => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");

        /// <summary>已读/未读徽章文本</summary>
        public string BadgeText => IsRead ? "已读" : "未读";
    }
}
