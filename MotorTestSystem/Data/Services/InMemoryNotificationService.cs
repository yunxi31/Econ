using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
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
            var seeds = new List<NotificationItem>
            {
                // 1. A4机台噪音超标
                new()
                {
                    Title = "A4 机台噪音超标",
                    Content = "检测到 A4 测试工位 (FX5U-04) 负载测试阶段异常。噪音传感器读取值为 85dB，超过阈值上限 75dB。测试序列已自动暂停，等待操作员介入确认。",
                    CreatedAt = DateTime.Parse("2024-10-24 14:21:10"),
                    Type = NotificationType.Alarm,
                    Severity = NotificationSeverity.Critical,
                    Source = "A4",
                    IsRead = false
                },
                // 2. PLC 通信异常
                new()
                {
                    Title = "PLC 通信异常",
                    Content = "工控机与网关 [GW-N02] 丢失心跳包超过 5 秒。当前状态：离线。受影响的工位：B1, B2。请检查以太网连接或重启网关设备。",
                    CreatedAt = DateTime.Parse("2024-10-24 14:15:33"),
                    Type = NotificationType.Alarm,
                    Severity = NotificationSeverity.Critical,
                    Source = "A3",
                    IsRead = false
                },
                // 3. 系统维护提醒
                new()
                {
                    Title = "系统维护提醒",
                    Content = "例行维护周期即将到来。C区夹具需要进行润滑和校准。建议在下一班次交接期间 ( 18:00 - 18:30 ) 安排停机维护。",
                    CreatedAt = DateTime.Parse("2024-10-24 10:00:00"),
                    Type = NotificationType.Maintenance,
                    Severity = NotificationSeverity.Warning,
                    IsRead = true
                },
                // 4. 固件更新可用
                new()
                {
                    Title = "固件更新可用",
                    Content = "测试控制台核心组件 v2.4.2-Stable 已准备就绪。此次更新包含针对高速数据采集模块的性能优化和一些 bug 修复。可在系统设置中手动触发更新。",
                    CreatedAt = DateTime.Parse("2024-10-23 23:45:12"),
                    Type = NotificationType.System,
                    Severity = NotificationSeverity.Info,
                    IsRead = true
                },
                // 5. 数据备份完成
                new()
                {
                    Title = "数据备份完成",
                    Content = "每日自动化数据库备份已成功完成。存档大小：4.2 GB。已同步至中央数据湖。",
                    CreatedAt = DateTime.Parse("2024-10-23 02:00:00"),
                    Type = NotificationType.System,
                    Severity = NotificationSeverity.Info,
                    IsRead = true
                },
                // 6. 其他报警（为了凑齐 3 个报警）
                new()
                {
                    Title = "机温预警限制触发",
                    Content = "工位1 (GW-M01) 电机测试温度 72°C，接近安全阈值 80°C",
                    CreatedAt = DateTime.Parse("2024-10-22 11:30:15"),
                    Type = NotificationType.Alarm,
                    Severity = NotificationSeverity.Warning,
                    Source = "A1",
                    IsRead = true
                },
                // 7. 其他维护（为了凑齐 4 个维护）
                new()
                {
                    Title = "传感器标定超期",
                    Content = "工位A2 扭矩传感器标定超期 3 天，请尽快安排标定以确保测试精度。",
                    CreatedAt = DateTime.Parse("2024-10-22 09:15:00"),
                    Type = NotificationType.Maintenance,
                    Severity = NotificationSeverity.Warning,
                    Source = "A2",
                    IsRead = true
                },
                new()
                {
                    Title = "清洁过滤网警告",
                    Content = "配电柜冷风机过滤网压差异常，请及时清洁或更换滤网。",
                    CreatedAt = DateTime.Parse("2024-10-22 08:30:00"),
                    Type = NotificationType.Maintenance,
                    Severity = NotificationSeverity.Info,
                    IsRead = true
                },
                new()
                {
                    Title = "UPS 电池包寿命警告",
                    Content = "主UPS电池自检警告: 电池寿命不足15%，请联系维护人员更换。",
                    CreatedAt = DateTime.Parse("2024-10-21 16:45:00"),
                    Type = NotificationType.Maintenance,
                    Severity = NotificationSeverity.Warning,
                    IsRead = true
                },
                // 8. 其他系统（为了凑齐 5 个系统）
                new()
                {
                    Title = "系统时间校准成功",
                    Content = "NTP 时间同步成功，偏差 +0.023s，全节点时钟已同步。",
                    CreatedAt = DateTime.Parse("2024-10-22 10:30:00"),
                    Type = NotificationType.System,
                    Severity = NotificationSeverity.Info,
                    IsRead = true
                },
                new()
                {
                    Title = "网络延迟预警",
                    Content = "交换机丢包，延迟 45ms (已切换至冗余物理链路)。",
                    CreatedAt = DateTime.Parse("2024-10-21 11:20:00"),
                    Type = NotificationType.System,
                    Severity = NotificationSeverity.Warning,
                    IsRead = true
                },
                new()
                {
                    Title = "报表导出成功",
                    Content = "电机能效及测试合格率分析报表导出成功，已保存至默认导出路径。",
                    CreatedAt = DateTime.Parse("2024-10-20 14:55:00"),
                    Type = NotificationType.System,
                    Severity = NotificationSeverity.Info,
                    IsRead = true
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
            // ObservableCollection 的 CollectionChanged 会触发 WPF UI 更新，必须在 UI 线程执行
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => Add(notification));
                return;
            }

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
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => Remove(notificationId));
                return;
            }

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
