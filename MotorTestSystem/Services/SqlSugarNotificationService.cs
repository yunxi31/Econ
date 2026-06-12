using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using MotorTestSystem.Models;
using MotorTestSystem.Models.Entities;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 基于 SQLite + SqlSugar 的通知服务实现
    /// 内存中维护 ObservableCollection 用于 UI 绑定，每次操作同步写入数据库
    /// </summary>
    public class SqlSugarNotificationService : INotificationService
    {
        private readonly SqlSugarDbContext _dbContext;
        private readonly ObservableCollection<NotificationItem> _notifications = new();
        private readonly object _lock = new();

        public ObservableCollection<NotificationItem> Notifications => _notifications;

        public int UnreadCount
        {
            get
            {
                lock (_lock) return _notifications.Count(n => !n.IsRead);
            }
        }

        public event EventHandler<int>? UnreadCountChanged;
        public event EventHandler<NotificationItem>? NotificationReceived;

        public SqlSugarNotificationService(SqlSugarDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            LoadFromDatabase();
        }

        /// <summary>
        /// 启动时从数据库加载所有通知进入内存
        /// </summary>
        private void LoadFromDatabase()
        {
            var entities = _dbContext.Db.Queryable<NotificationEntity>()
                .OrderByDescending(e => e.CreatedAt)
                .ToList();

            lock (_lock)
            {
                _notifications.Clear();
                foreach (var entity in entities)
                {
                    _notifications.Add(EntityToModel(entity));
                }
            }
        }

        // ========================================
        // Entity ↔ Model 转换
        // ========================================

        private static NotificationItem EntityToModel(NotificationEntity entity)
        {
            return new NotificationItem
            {
                Id = entity.Id,
                Title = entity.Title,
                Content = entity.Content,
                CreatedAt = entity.CreatedAt,
                Type = (NotificationType)entity.Type,
                Severity = (NotificationSeverity)entity.Severity,
                IsRead = entity.IsRead,
                Source = entity.Source
            };
        }

        private static NotificationEntity ModelToEntity(NotificationItem model)
        {
            return new NotificationEntity
            {
                Id = model.Id,
                Title = model.Title,
                Content = model.Content,
                CreatedAt = model.CreatedAt,
                Type = (int)model.Type,
                Severity = (int)model.Severity,
                IsRead = model.IsRead,
                Source = model.Source
            };
        }

        // ========================================
        // INotificationService 实现
        // ========================================

        public void Add(NotificationItem notification)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => Add(notification));
                return;
            }

            // 先写入数据库
            var entity = ModelToEntity(notification);
            _dbContext.Db.Insertable(entity).ExecuteCommand();

            // 再添加到内存集合
            lock (_lock)
            {
                _notifications.Insert(0, notification);
            }

            NotificationReceived?.Invoke(this, notification);
            RaiseUnreadCountChanged();
        }

        public void AddRange(IEnumerable<NotificationItem> notifications)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => AddRange(notifications));
                return;
            }

            var items = notifications.ToList();
            if (items.Count == 0) return;

            // 批量写入数据库
            var entities = items.Select(ModelToEntity).ToList();
            _dbContext.Db.Insertable(entities).ExecuteCommand();

            // 添加到内存集合（保持最新在前）
            lock (_lock)
            {
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    _notifications.Insert(0, items[i]);
                }
            }

            foreach (var item in items)
            {
                NotificationReceived?.Invoke(this, item);
            }
            RaiseUnreadCountChanged();
        }

        public void MarkAsRead(string notificationId)
        {
            // 更新数据库
            _dbContext.Db.Updateable<NotificationEntity>()
                .SetColumns(e => new NotificationEntity { IsRead = true })
                .Where(e => e.Id == notificationId)
                .ExecuteCommand();

            // 更新内存
            lock (_lock)
            {
                var item = _notifications.FirstOrDefault(n => n.Id == notificationId);
                if (item != null && !item.IsRead)
                {
                    item.IsRead = true;
                }
            }

            RaiseUnreadCountChanged();
        }

        public void MarkAllAsRead()
        {
            // 批量更新数据库
            _dbContext.Db.Updateable<NotificationEntity>()
                .SetColumns(e => new NotificationEntity { IsRead = true })
                .Where(e => !e.IsRead)
                .ExecuteCommand();

            // 更新内存
            lock (_lock)
            {
                foreach (var n in _notifications)
                {
                    n.IsRead = true;
                }
            }

            RaiseUnreadCountChanged();
        }

        public void Remove(string notificationId)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => Remove(notificationId));
                return;
            }

            // 从数据库删除
            _dbContext.Db.Deleteable<NotificationEntity>()
                .Where(e => e.Id == notificationId)
                .ExecuteCommand();

            // 从内存移除
            lock (_lock)
            {
                var item = _notifications.FirstOrDefault(n => n.Id == notificationId);
                if (item != null)
                {
                    _notifications.Remove(item);
                }
            }

            RaiseUnreadCountChanged();
        }

        public void ClearAll()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => ClearAll());
                return;
            }

            // 清空数据库
            _dbContext.Db.Deleteable<NotificationEntity>()
                .Where(e => true) // SqlSugar 需要条件，用永真式
                .ExecuteCommand();

            // 清空内存
            lock (_lock)
            {
                _notifications.Clear();
            }

            RaiseUnreadCountChanged();
        }

        public int GetCountByType(NotificationType type)
        {
            lock (_lock) return _notifications.Count(n => n.Type == type);
        }

        public int GetTotalCount()
        {
            lock (_lock) return _notifications.Count;
        }

        private void RaiseUnreadCountChanged()
        {
            UnreadCountChanged?.Invoke(this, UnreadCount);
        }
    }
}
