using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 内存通知服务实现 — 管理通知集合、种子数据、实时通知生成
    /// </summary>
    public class InMemoryNotificationService : INotificationService
    {
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

        public InMemoryNotificationService()
        {
            SeedNotifications();
        }

        // ========================================
        // 种子数据 — 模拟历史通知
        // ========================================
        private void SeedNotifications()
        {
            var now = DateTime.Now;
            var seeds = new List<NotificationItem>
            {
                // ---- 报警 ----
                new()
                {
                    Title = "A4机台噪音超标",
                    Content = "A4机台噪音 85dB，超出安全阈值上限 75dB (自动暂停)",
                    CreatedAt = now.AddHours(-7).AddMinutes(38),
                    Type = NotificationType.Alarm, Severity = NotificationSeverity.Critical,
                    Source = "A4", IsRead = false
                },
                new()
                {
                    Title = "PLC 通信异常",
                    Content = "工位3 (GW-M02) 丢失心跳包超 5s (状态: 离线)",
                    CreatedAt = now.AddHours(-7).AddMinutes(44),
                    Type = NotificationType.Alarm, Severity = NotificationSeverity.Critical,
                    Source = "A3", IsRead = false
                },
                new()
                {
                    Title = "机温预警限制触发",
                    Content = "工位1 (GW-M01) 电机测试温度 72°C，接近安全阈值 80°C",
                    CreatedAt = now.AddDays(-1).AddHours(-5).AddMinutes(27),
                    Type = NotificationType.Alarm, Severity = NotificationSeverity.Warning,
                    Source = "A1", IsRead = false
                },

                // ---- 维护 ----
                new()
                {
                    Title = "例行维护周期提醒",
                    Content = "C区夹具例行润滑与校准到期，建议交班停机维护",
                    CreatedAt = now.AddHours(-12),
                    Type = NotificationType.Maintenance, Severity = NotificationSeverity.Warning,
                    IsRead = false
                },
                new()
                {
                    Title = "传感器标定超期",
                    Content = "工位A2 扭矩传感器标定超期 3 天",
                    CreatedAt = now.AddDays(-1).AddHours(3).AddMinutes(40),
                    Type = NotificationType.Maintenance, Severity = NotificationSeverity.Warning,
                    Source = "A2", IsRead = false
                },
                new()
                {
                    Title = "清洁过滤网警告",
                    Content = "配电柜冷风机过滤网压差异常，请清洁更换",
                    CreatedAt = now.AddDays(-2).AddHours(2).AddMinutes(44),
                    Type = NotificationType.Maintenance, Severity = NotificationSeverity.Info,
                    IsRead = false
                },
                new()
                {
                    Title = "UPS 电池包寿命警告",
                    Content = "主UPS电池自检警告: 电池寿命不足15%",
                    CreatedAt = now.AddDays(-3).AddHours(6).AddMinutes(17),
                    Type = NotificationType.Maintenance, Severity = NotificationSeverity.Warning,
                    IsRead = false
                },

                // ---- 系统 ----
                new()
                {
                    Title = "固件更新可用",
                    Content = "系统固件 v2.4.2-Stable 可用，包含高采样性能优化",
                    CreatedAt = now.AddDays(-1).AddHours(-2),
                    Type = NotificationType.System, Severity = NotificationSeverity.Info,
                    IsRead = false
                },
                new()
                {
                    Title = "数据备份完成",
                    Content = "每日数据库备份完成，大小 4.2 GB (同步至数据湖)",
                    CreatedAt = now.AddDays(-1).AddHours(2),
                    Type = NotificationType.System, Severity = NotificationSeverity.Info,
                    IsRead = false
                },
                new()
                {
                    Title = "系统时间校准成功",
                    Content = "NTP 时间同步成功，偏差 +0.023s，全节点时钟已同步",
                    CreatedAt = now.AddDays(-3).AddHours(-17).AddMinutes(49),
                    Type = NotificationType.System, Severity = NotificationSeverity.Info,
                    IsRead = false
                },
                new()
                {
                    Title = "网络延迟预警",
                    Content = "交换机丢包，延迟 45ms (已切换至冗余物理链路)",
                    CreatedAt = now.AddDays(-4).AddHours(2).AddMinutes(47),
                    Type = NotificationType.System, Severity = NotificationSeverity.Warning,
                    IsRead = false
                },
                new()
                {
                    Title = "报表导出成功",
                    Content = "电机能效及测试合格率分析报表导出成功",
                    CreatedAt = now.AddDays(-5).AddHours(4).AddMinutes(29),
                    Type = NotificationType.System, Severity = NotificationSeverity.Info,
                    IsRead = false
                }
            };

            foreach (var item in seeds)
            {
                _notifications.Add(item);
            }
        }

        // ========================================
        // INotificationService 实现
        // ========================================

        public void Add(NotificationItem notification)
        {
            lock (_lock)
            {
                _notifications.Insert(0, notification); // 最新的排最前面
            }
            NotificationReceived?.Invoke(this, notification);
            RaiseUnreadCountChanged();
        }

        public void AddRange(IEnumerable<NotificationItem> notifications)
        {
            foreach (var n in notifications)
            {
                Add(n);
            }
        }

        public void MarkAsRead(string notificationId)
        {
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
