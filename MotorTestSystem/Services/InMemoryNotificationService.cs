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
                    Content = "检测到A4机台(型号:FX5U-64)负载测试阶段异常。噪音传感器读取值为 85dB，超过阈值上限 75dB。测试序列已自动暂停，等待操作员介入确认。",
                    CreatedAt = now.AddHours(-7).AddMinutes(38),
                    Type = NotificationType.Alarm, Severity = NotificationSeverity.Critical,
                    Source = "A4", IsRead = false
                },
                new()
                {
                    Title = "PLC 通信异常",
                    Content = "上位机与工位 3(GW-M02) 丢失心跳包超过 5 秒，当前状态：离线。受影响的工位：B1, B2。请检查以太网连接或重启网关设备。",
                    CreatedAt = now.AddHours(-7).AddMinutes(44),
                    Type = NotificationType.Alarm, Severity = NotificationSeverity.Critical,
                    Source = "A3", IsRead = false
                },
                new()
                {
                    Title = "机温预警限制触发",
                    Content = "工位 1(GW-M01)电机测试温度达到 72°C，接近安全阈值 80°C。请注意观察，如有必要请降低测试功率或暂停以防过热。",
                    CreatedAt = now.AddDays(-1).AddHours(-5).AddMinutes(27),
                    Type = NotificationType.Alarm, Severity = NotificationSeverity.Warning,
                    Source = "A1", IsRead = false
                },

                // ---- 维护 ----
                new()
                {
                    Title = "例行维护周期提醒",
                    Content = "例行维护周期即将到来。C区夹具需要进行润滑和校准。建议在下一班次交接期间 (18:00 - 18:30) 安排停机维护。",
                    CreatedAt = now.AddHours(-12),
                    Type = NotificationType.Maintenance, Severity = NotificationSeverity.Warning,
                    IsRead = false
                },
                new()
                {
                    Title = "传感器标定超期",
                    Content = "A2工位扭矩传感器标定日期已超期 3 天。为了保证测试结果准确性，请尽快安排专业人员进行重新标定与建档。",
                    CreatedAt = now.AddDays(-1).AddHours(3).AddMinutes(40),
                    Type = NotificationType.Maintenance, Severity = NotificationSeverity.Warning,
                    Source = "A2", IsRead = false
                },
                new()
                {
                    Title = "清洁过滤网警示",
                    Content = "配电柜冷风机组过滤网压差传感器报警，请在 24 小时内清洁或更换过滤网，以免影响设备散热性能。",
                    CreatedAt = now.AddDays(-2).AddHours(2).AddMinutes(44),
                    Type = NotificationType.Maintenance, Severity = NotificationSeverity.Info,
                    IsRead = false
                },
                new()
                {
                    Title = "UPS 电池包寿命警告",
                    Content = "主控机柜不间断电源(UPS)内部自检报告：电池寿命预计不足 15%，请提前采购适配电池组并安排停机更换。",
                    CreatedAt = now.AddDays(-3).AddHours(6).AddMinutes(17),
                    Type = NotificationType.Maintenance, Severity = NotificationSeverity.Warning,
                    IsRead = false
                },

                // ---- 系统 ----
                new()
                {
                    Title = "固件更新可用",
                    Content = "测试控制台核心组件 v2.4.2-Stable 已准备就绪。此次更新包含针对高速数据采集模块的性能优化和一些小 bug 修复。可在系统设置中手动触发更新。",
                    CreatedAt = now.AddDays(-1).AddHours(-2),
                    Type = NotificationType.System, Severity = NotificationSeverity.Info,
                    IsRead = false
                },
                new()
                {
                    Title = "数据备份完成",
                    Content = "每日自动化数据库备份已成功完成。存档大小：4.2 GB。已同步至中央数据湖。",
                    CreatedAt = now.AddDays(-1).AddHours(2),
                    Type = NotificationType.System, Severity = NotificationSeverity.Info,
                    IsRead = false
                },
                new()
                {
                    Title = "系统时间校准成功",
                    Content = "系统已成功与局域网 NTP 时间服务器同步，校准偏差为 +0.023 秒。所有上位机节点与 PLC 模块已同步时钟。",
                    CreatedAt = now.AddDays(-3).AddHours(-17).AddMinutes(49),
                    Type = NotificationType.System, Severity = NotificationSeverity.Info,
                    IsRead = false
                },
                new()
                {
                    Title = "网络延迟预警",
                    Content = "局域网交换机主干端口出现突发数据丢包，平均网络延迟上升至 45ms。目前已自动切换至冗余备用端口，运行未受阻碍。",
                    CreatedAt = now.AddDays(-4).AddHours(2).AddMinutes(47),
                    Type = NotificationType.System, Severity = NotificationSeverity.Warning,
                    IsRead = false
                },
                new()
                {
                    Title = "报表导出成功",
                    Content = "2024年第3季度电机能效与测试合格率分析报表已成功导出，格式为 PDF/Excel，已保存至主服务器归档路径：D:\\Reports\\Q3\\。",
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
