using System;
using System.Collections.ObjectModel;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 通知服务接口 — 管理通知的增删改查、已读标记、实时推送
    /// </summary>
    public interface INotificationService
    {
        /// <summary>全量通知集合（可观察，UI可直接绑定）</summary>
        ObservableCollection<NotificationItem> Notifications { get; }

        /// <summary>当前未读数量</summary>
        int UnreadCount { get; }

        /// <summary>未读数量变化事件</summary>
        event EventHandler<int>? UnreadCountChanged;

        /// <summary>新通知到达事件</summary>
        event EventHandler<NotificationItem>? NotificationReceived;

        /// <summary>添加一条通知</summary>
        void Add(NotificationItem notification);

        /// <summary>批量添加通知</summary>
        void AddRange(System.Collections.Generic.IEnumerable<NotificationItem> notifications);

        /// <summary>标记指定通知为已读</summary>
        void MarkAsRead(string notificationId);

        /// <summary>标记全部为已读</summary>
        void MarkAllAsRead();

        /// <summary>删除指定通知</summary>
        void Remove(string notificationId);

        /// <summary>清空所有通知</summary>
        void ClearAll();

        /// <summary>获取指定类型的通知数量</summary>
        int GetCountByType(NotificationType type);

        /// <summary>获取总通知数量</summary>
        int GetTotalCount();
    }
}
