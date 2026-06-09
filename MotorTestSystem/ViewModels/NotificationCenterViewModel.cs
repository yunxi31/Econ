using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MotorTestSystem.ViewModels
{
    public partial class NotificationCenterViewModel : ViewModelBase
    {
        public class NotificationItem : ObservableObject
        {
            public string Id { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public string Timestamp { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty; // "报警", "维护", "系统"

            private bool _isRead;
            public bool IsRead
            {
                get => _isRead;
                set
                {
                    if (SetProperty(ref _isRead, value))
                    {
                        OnPropertyChanged(nameof(BadgeText));
                    }
                }
            }

            public string BadgeText => IsRead ? "已读" : "未读";
        }

        private readonly ObservableCollection<NotificationItem> _allNotifications = new();

        [ObservableProperty]
        private ObservableCollection<NotificationItem> _notifications = new();

        [ObservableProperty]
        private string _selectedFilter = "全部"; // "全部", "报警", "维护", "系统"

        [ObservableProperty]
        private int _allCount;

        [ObservableProperty]
        private int _alarmCount;

        [ObservableProperty]
        private int _maintenanceCount;

        [ObservableProperty]
        private int _systemCount;

        [ObservableProperty]
        private int _unreadCount;

        public NotificationCenterViewModel()
        {
            LoadMockNotifications();
            UpdateCounts();
            FilterNotifications();
        }

        private void LoadMockNotifications()
        {
            // 报警 (3个), 维护 (4个), 系统 (5个) = 12个
            _allNotifications.Add(new NotificationItem
            {
                Id = "1",
                Title = "A4机台噪音超标",
                Content = "检测到A4机台(型号:FX5U-64)负载测试阶段异常。噪音传感器读取值为 85dB，超过阈值上限 75dB。测试序列已自动暂停，等待操作员介入确认。",
                Timestamp = "2024-10-24 14:21:10",
                Type = "报警",
                IsRead = false
            });

            _allNotifications.Add(new NotificationItem
            {
                Id = "2",
                Title = "PLC 通信异常",
                Content = "上位机与工位 3(GW-M02) 丢失心跳包超过 5 秒，当前状态：离线。受影响的工位：B1, B2。请检查以太网连接或重启网关设备。",
                Timestamp = "2024-10-24 14:15:33",
                Type = "报警",
                IsRead = false
            });

            _allNotifications.Add(new NotificationItem
            {
                Id = "3",
                Title = "系统维护提醒",
                Content = "例行维护周期即将到来。C区夹具需要进行润滑和校准。建议在下一班次交接期间 (18:00 - 18:30) 安排停机维护。",
                Timestamp = "2024-10-24 10:00:00",
                Type = "维护",
                IsRead = false
            });

            _allNotifications.Add(new NotificationItem
            {
                Id = "4",
                Title = "固件更新可用",
                Content = "测试控制台核心组件 v2.4.2-Stable 已准备就绪。此次更新包含针对高速数据采集模块的性能优化 and 一些小 bug 修复。可在系统设置中手动触发更新。",
                Timestamp = "2024-10-23 23:45:12",
                Type = "系统",
                IsRead = false
            });

            _allNotifications.Add(new NotificationItem
            {
                Id = "5",
                Title = "数据备份完成",
                Content = "每日自动化数据库备份已成功完成。存档大小：4.2 GB。已同步至中央数据湖。",
                Timestamp = "2024-10-23 02:00:00",
                Type = "系统",
                IsRead = false
            });

            _allNotifications.Add(new NotificationItem
            {
                Id = "6",
                Title = "机温预警限制触发",
                Content = "工位 1(GW-M01)电机测试温度达到 72°C，接近安全阈值 80°C。请注意观察，如有必要请降低测试功率或暂停以防过热。",
                Timestamp = "2024-10-22 16:30:22",
                Type = "报警",
                IsRead = false
            });

            _allNotifications.Add(new NotificationItem
            {
                Id = "7",
                Title = "传感器标定超期",
                Content = "A2工位扭矩传感器标定日期已超期 3 天。为了保证测试结果准确性，请尽快安排专业人员进行重新标定与建档。",
                Timestamp = "2024-10-22 11:20:00",
                Type = "维护",
                IsRead = false
            });

            _allNotifications.Add(new NotificationItem
            {
                Id = "8",
                Title = "清洁过滤网警示",
                Content = "配电柜冷风机组过滤网压差传感器报警，请在 24 小时内清洁或更换过滤网，以免影响设备散热性能。",
                Timestamp = "2024-10-21 09:15:00",
                Type = "维护",
                IsRead = false
            });

            _allNotifications.Add(new NotificationItem
            {
                Id = "9",
                Title = "UPS 电池包寿命警告",
                Content = "主控机柜不间断电源(UPS)内部自检报告：电池寿命预计不足 15%，请提前采购适配电池组并安排停机更换。",
                Timestamp = "2024-10-20 15:40:00",
                Type = "维护",
                IsRead = false
            });

            _allNotifications.Add(new NotificationItem
            {
                Id = "10",
                Title = "系统时间校准成功",
                Content = "系统已成功与局域网 NTP 时间服务器同步，校准偏差为 +0.023 秒。所有上位机节点与 PLC 模块已同步时钟。",
                Timestamp = "2024-10-20 04:00:10",
                Type = "系统",
                IsRead = false
            });

            _allNotifications.Add(new NotificationItem
            {
                Id = "11",
                Title = "网络延迟预警",
                Content = "局域网交换机主干端口出现突发数据丢包，平均网络延迟上升至 45ms。目前已自动切换至冗余备用端口，运行未受阻碍。",
                Timestamp = "2024-10-19 19:10:45",
                Type = "系统",
                IsRead = false
            });

            _allNotifications.Add(new NotificationItem
            {
                Id = "12",
                Title = "报表导出成功",
                Content = "2024年第3季度电机能效与测试合格率分析报表已成功导出，格式为 PDF/Excel，已保存至主服务器归档路径：D:\\Reports\\Q3\\。",
                Timestamp = "2024-10-18 17:30:00",
                Type = "系统",
                IsRead = false
            });
        }

        partial void OnSelectedFilterChanged(string value)
        {
            FilterNotifications();
        }

        private void FilterNotifications()
        {
            var filtered = _allNotifications.AsEnumerable();

            if (SelectedFilter != "全部")
            {
                filtered = filtered.Where(n => n.Type == SelectedFilter);
            }

            // 按时间倒序排序
            Notifications = new ObservableCollection<NotificationItem>(filtered.OrderByDescending(n => n.Timestamp));
        }

        private void UpdateCounts()
        {
            AllCount = _allNotifications.Count;
            AlarmCount = _allNotifications.Count(n => n.Type == "报警");
            MaintenanceCount = _allNotifications.Count(n => n.Type == "维护");
            SystemCount = _allNotifications.Count(n => n.Type == "系统");
            UnreadCount = _allNotifications.Count(n => !n.IsRead);
        }

        [RelayCommand]
        private void MarkAllAsRead()
        {
            foreach (var n in _allNotifications)
            {
                n.IsRead = true;
            }
            UpdateCounts();
            FilterNotifications();
        }

        [RelayCommand]
        private void ClearAll()
        {
            _allNotifications.Clear();
            UpdateCounts();
            FilterNotifications();
        }

        [RelayCommand]
        private void ToggleReadStatus(NotificationItem item)
        {
            if (item == null || item.IsRead) return;
            item.IsRead = true;
            UpdateCounts();
            FilterNotifications();
        }
    }
}
