using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorTestSystem.Models;
using MotorTestSystem.Services;

namespace MotorTestSystem.ViewModels
{
    /// <summary>
    /// 通知条目的 UI 包装（支持 ObservableObject 属性变更通知）
    /// </summary>
    public class NotificationItemViewModel : ObservableObject
    {
        private readonly NotificationItem _model;

        public NotificationItemViewModel(NotificationItem model)
        {
            _model = model;
        }

        /// <summary>底层模型引用</summary>
        public NotificationItem Model => _model;

        public string Id => _model.Id;
        public string Title => _model.Title;
        public string Content => _model.Content;
        public string Timestamp => _model.Timestamp;
        public string TypeDisplay => _model.TypeDisplay;
        public NotificationType Type => _model.Type;
        public NotificationSeverity Severity => _model.Severity;
        public string? Source => _model.Source;

        private bool _isRead;
        public bool IsRead
        {
            get => _isRead;
            set
            {
                if (SetProperty(ref _isRead, value))
                {
                    _model.IsRead = value;
                    OnPropertyChanged(nameof(BadgeText));
                }
            }
        }

        public string BadgeText => IsRead ? "已读" : "未读";

        /// <summary>从模型同步状态（外部修改后调用）</summary>
        public void SyncFromModel()
        {
            if (_isRead != _model.IsRead)
            {
                _isRead = _model.IsRead;
                OnPropertyChanged(nameof(IsRead));
                OnPropertyChanged(nameof(BadgeText));
            }
        }
    }

    public partial class NotificationCenterViewModel : ViewModelBase
    {
        private readonly INotificationService? _notificationService;

        /// <summary>全量通知 ViewModel（私有，用于筛选）</summary>
        private readonly ObservableCollection<NotificationItemViewModel> _allNotificationVms = new();

        /// <summary>模型ID → ViewModel 映射（快速查找）</summary>
        private readonly System.Collections.Generic.Dictionary<string, NotificationItemViewModel> _vmMap = new();

        [ObservableProperty]
        private ObservableCollection<NotificationItemViewModel> _notifications = new();

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

        /// <summary>
        /// 无参构造（兼容旧调用方式，使用内置 Mock 数据）
        /// </summary>
        public NotificationCenterViewModel() : this(null) { }

        /// <summary>
        /// 依赖注入构造（推荐）
        /// </summary>
        public NotificationCenterViewModel(INotificationService? notificationService)
        {
            _notificationService = notificationService;

            if (_notificationService != null)
            {
                // 从服务加载已有通知
                foreach (var item in _notificationService.Notifications)
                {
                    var vm = new NotificationItemViewModel(item);
                    _allNotificationVms.Add(vm);
                    _vmMap[item.Id] = vm;
                }

                // 监听服务层的实时通知
                _notificationService.Notifications.CollectionChanged += OnServiceCollectionChanged;
                _notificationService.NotificationReceived += OnNotificationReceived;
                _notificationService.UnreadCountChanged += OnUnreadCountChanged;
            }

            UpdateCounts();
            FilterNotifications();
        }

        // ========================================
        // 服务层事件处理
        // ========================================

        private void OnServiceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems != null)
                    {
                        foreach (NotificationItem item in e.NewItems)
                        {
                            if (!_vmMap.ContainsKey(item.Id))
                            {
                                var vm = new NotificationItemViewModel(item);
                                _allNotificationVms.Insert(0, vm); // 最新在前
                                _vmMap[item.Id] = vm;
                            }
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems != null)
                    {
                        foreach (NotificationItem item in e.OldItems)
                        {
                            if (_vmMap.Remove(item.Id, out var vm))
                            {
                                _allNotificationVms.Remove(vm);
                            }
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    _allNotificationVms.Clear();
                    _vmMap.Clear();
                    foreach (var item in _notificationService!.Notifications)
                    {
                        var vm = new NotificationItemViewModel(item);
                        _allNotificationVms.Add(vm);
                        _vmMap[item.Id] = vm;
                    }
                    break;
            }

            UpdateCounts();
            FilterNotifications();
        }

        private void OnNotificationReceived(object? sender, NotificationItem item)
        {
            // 新通知到达时刷新筛选和计数（UI 线程安全由 Dispatcher 保证）
            UpdateCounts();
            FilterNotifications();
        }

        private void OnUnreadCountChanged(object? sender, int newCount)
        {
            UnreadCount = newCount;
        }

        // ========================================
        // 筛选和计数
        // ========================================

        partial void OnSelectedFilterChanged(string value)
        {
            FilterNotifications();
        }

        private void FilterNotifications()
        {
            var filtered = _allNotificationVms.AsEnumerable();

            if (SelectedFilter != "全部")
            {
                string targetType = SelectedFilter switch
                {
                    "报警" => "报警",
                    "维护" => "维护",
                    "系统" => "系统",
                    _ => SelectedFilter
                };
                filtered = filtered.Where(n => n.TypeDisplay == targetType);
            }

            // 按时间倒序
            Notifications = new ObservableCollection<NotificationItemViewModel>(
                filtered.OrderByDescending(n => n.Timestamp));
        }

        private void UpdateCounts()
        {
            AllCount = _allNotificationVms.Count;
            AlarmCount = _allNotificationVms.Count(n => n.Type == NotificationType.Alarm);
            MaintenanceCount = _allNotificationVms.Count(n => n.Type == NotificationType.Maintenance);
            SystemCount = _allNotificationVms.Count(n => n.Type == NotificationType.System);
            UnreadCount = _allNotificationVms.Count(n => !n.IsRead);
        }

        // ========================================
        // 命令
        // ========================================

        [RelayCommand]
        private void MarkAllAsRead()
        {
            foreach (var n in _allNotificationVms)
            {
                n.IsRead = true;
            }

            _notificationService?.MarkAllAsRead();
            UpdateCounts();
            FilterNotifications();
        }

        [RelayCommand]
        private void ClearAll()
        {
            _allNotificationVms.Clear();
            _vmMap.Clear();

            _notificationService?.ClearAll();
            UpdateCounts();
            FilterNotifications();
        }

        [RelayCommand]
        private void ToggleReadStatus(NotificationItemViewModel? item)
        {
            if (item == null || item.IsRead) return;
            item.IsRead = true;

            _notificationService?.MarkAsRead(item.Id);
            UpdateCounts();
            FilterNotifications();
        }
    }
}
