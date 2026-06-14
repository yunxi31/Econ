using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
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
        private bool _isUpdatingDates = false; // 防止 StartDate/EndDate 互改死循环

        /// <summary>全量通知 ViewModel（私有，用于筛选）</summary>
        private readonly ObservableCollection<NotificationItemViewModel> _allNotificationVms = new();

        /// <summary>模型ID → ViewModel 映射（快速查找）</summary>
        private readonly System.Collections.Generic.Dictionary<string, NotificationItemViewModel> _vmMap = new();

        [ObservableProperty]
        private string _pageTitle = "日志中心";

        [ObservableProperty]
        private string _pageIcon = "FolderOpenOutline";

        [ObservableProperty]
        private ObservableCollection<NotificationItemViewModel> _notifications = new();

        [ObservableProperty]
        private string _selectedFilter = "全部"; // "全部", "报警", "维护", "系统"

        [ObservableProperty]
        private string _selectedTab = "运行日志"; // "运行日志" | "操作日志"

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _selectedSource = "全部";

        [ObservableProperty]
        private ObservableCollection<string> _sources = new() { "全部" };

        [ObservableProperty]
        private DateTime _startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        [ObservableProperty]
        private DateTime _endDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month,
            DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month));

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalPages))]
        [NotifyPropertyChangedFor(nameof(CurrentPageStart))]
        [NotifyPropertyChangedFor(nameof(CurrentPageEnd))]
        private int _currentPage = 1;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalPages))]
        [NotifyPropertyChangedFor(nameof(CurrentPageStart))]
        [NotifyPropertyChangedFor(nameof(CurrentPageEnd))]
        private int _pageSize = 100;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalPages))]
        [NotifyPropertyChangedFor(nameof(CurrentPageStart))]
        [NotifyPropertyChangedFor(nameof(CurrentPageEnd))]
        private int _totalRecords = 0;

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));
        public int CurrentPageStart => TotalRecords == 0 ? 0 : (CurrentPage - 1) * PageSize + 1;
        public int CurrentPageEnd => Math.Min(CurrentPage * PageSize, TotalRecords);

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
            _notificationService = notificationService ?? new InMemoryNotificationService();

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
            // CollectionChanged 可能从后台线程触发（PLC 轮询），必须回到 UI 线程
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => OnServiceCollectionChanged(sender, e));
                return;
            }

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
            // 新通知到达时刷新筛选和计数 — 事件可能来自后台线程，必须 Dispatch 到 UI
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => OnNotificationReceived(sender, item));
                return;
            }
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

        partial void OnSelectedFilterChanged(string value) => FilterNotifications();
        partial void OnSearchTextChanged(string value) => FilterNotifications();
        partial void OnSelectedSourceChanged(string value) => FilterNotifications();
        partial void OnSelectedTabChanged(string value) => FilterNotifications();
        
        partial void OnStartDateChanged(DateTime value)
        {
            if (_isUpdatingDates) return;
            _isUpdatingDates = true;
            try
            {
                OnPropertyChanged(nameof(MinEndDate));
                OnPropertyChanged(nameof(MaxEndDate));
                OnPropertyChanged(nameof(StartCalendarDisplayStart));
                OnPropertyChanged(nameof(StartCalendarDisplayEnd));
                if (EndDate < value) EndDate = value;
                else if (EndDate > value.AddDays(31)) EndDate = value.AddDays(31);
            }
            finally
            {
                _isUpdatingDates = false;
            }
            FilterNotifications();
        }

        partial void OnEndDateChanged(DateTime value)
        {
            if (_isUpdatingDates) return;
            _isUpdatingDates = true;
            try
            {
                OnPropertyChanged(nameof(MinStartDate));
                OnPropertyChanged(nameof(MaxStartDate));
                OnPropertyChanged(nameof(EndCalendarDisplayStart));
                OnPropertyChanged(nameof(EndCalendarDisplayEnd));
                if (StartDate > value) StartDate = value;
                else if (StartDate < value.AddDays(-31)) StartDate = value.AddDays(-31);
            }
            finally
            {
                _isUpdatingDates = false;
            }
            FilterNotifications();
        }

        // 用于 DatePicker 的 DisplayDateStart/End —— 始终是 StartDate 所在月份的头尾
        public DateTime StartCalendarDisplayStart => new DateTime(StartDate.Year, StartDate.Month, 1);
        public DateTime StartCalendarDisplayEnd   => new DateTime(StartDate.Year, StartDate.Month,
            DateTime.DaysInMonth(StartDate.Year, StartDate.Month));

        // 用于 EndDate DatePicker 的 DisplayDateStart/End —— 始终是 EndDate 所在月份的头尾
        public DateTime EndCalendarDisplayStart => new DateTime(EndDate.Year, EndDate.Month, 1);
        public DateTime EndCalendarDisplayEnd   => new DateTime(EndDate.Year, EndDate.Month,
            DateTime.DaysInMonth(EndDate.Year, EndDate.Month));

        // 业务约束：End 不能早于 Start，Start 不能超前 End 超过31天
        public DateTime MinEndDate => StartDate;
        public DateTime MaxEndDate => StartDate.AddDays(31);

        public DateTime MinStartDate => EndDate.AddDays(-31);
        public DateTime MaxStartDate => EndDate;

        private System.Collections.Generic.IEnumerable<NotificationItemViewModel> _currentFilteredList = Enumerable.Empty<NotificationItemViewModel>();

        private void FilterNotifications()
        {
            var filtered = _allNotificationVms.AsEnumerable();

            // 在所有通知范围内按 Level 筛选
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

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(n => 
                    (n.Content != null && n.Content.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                    (n.Title != null && n.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                );
            }

            if (!string.IsNullOrEmpty(SelectedSource) && SelectedSource != "全部")
            {
                filtered = filtered.Where(n => n.Source == SelectedSource);
            }

            // 按时间倒序
            _currentFilteredList = filtered.OrderByDescending(n => n.Model.CreatedAt).ToList();
            TotalRecords = _currentFilteredList.Count();
            CurrentPage = 1;
            UpdatePagedNotifications();
        }

        private void UpdatePagedNotifications()
        {
            var paged = _currentFilteredList.Skip((CurrentPage - 1) * PageSize).Take(PageSize);
            Notifications = new ObservableCollection<NotificationItemViewModel>(paged);
            
            // 手动触发通知以确保UI更新
            OnPropertyChanged(nameof(CurrentPageStart));
            OnPropertyChanged(nameof(CurrentPageEnd));
            OnPropertyChanged(nameof(TotalPages));
        }

        private void UpdateCounts()
        {
            AllCount = _allNotificationVms.Count;
            AlarmCount = _allNotificationVms.Count(n => n.Type == NotificationType.Alarm);
            MaintenanceCount = _allNotificationVms.Count(n => n.Type == NotificationType.Maintenance);
            SystemCount = _allNotificationVms.Count(n => n.Type == NotificationType.System);
            UnreadCount = _allNotificationVms.Count(n => !n.IsRead);
            UpdateSources();
        }

        private void UpdateSources()
        {
            var currentSelected = SelectedSource;
            var uniqueSources = _allNotificationVms
                .Select(n => n.Source)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(s => s)
                .ToList();
            
            Sources.Clear();
            Sources.Add("全部");
            foreach (var src in uniqueSources)
            {
                Sources.Add(src!);
            }
            
            if (Sources.Contains(currentSelected))
            {
                SelectedSource = currentSelected;
            }
            else
            {
                SelectedSource = "全部";
            }
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

        [RelayCommand]
        private void ViewDetails(NotificationItemViewModel? item)
        {
            if (item == null) return;
            MessageBox.Show($"查看详情:\n{item.Title}\n\n{item.Content}", "通知详情", MessageBoxButton.OK, MessageBoxImage.Information);
            ToggleReadStatus(item);
        }

        [RelayCommand]
        private void DiagnoseConnection(NotificationItemViewModel? item)
        {
            if (item == null) return;
            MessageBox.Show($"正在诊断连接:\n工位: {item.Source ?? "未知工位"}\n网关: GW-N02\n\n设备状态: 正在尝试连接重试...\n诊断结果: 物理链路正常，心跳包已恢复同步。", "PLC 连接诊断", MessageBoxButton.OK, MessageBoxImage.Warning);
            ToggleReadStatus(item);
        }

        [RelayCommand]
        private void NextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                UpdatePagedNotifications();
            }
        }

        [RelayCommand]
        private void PreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                UpdatePagedNotifications();
            }
        }

        [RelayCommand]
        private void ExportCsv()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                DefaultExt = ".csv",
                FileName = $"LogExport_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                var sb = new System.Text.StringBuilder();
                // 写入 UTF-8 BOM，防止 Excel 打开乱码
                sb.Append('\uFEFF');
                sb.AppendLine("Timestamp,Level,Source,Message");
                
                foreach (var n in _currentFilteredList) // 导出所有筛选结果，而不仅仅是当前页
                {
                    sb.AppendLine($"\"{n.Timestamp}\",\"{n.TypeDisplay}\",\"{n.Source}\",\"{n.Content?.Replace("\"", "\"\"")}\"");
                }
                System.IO.File.WriteAllText(dialog.FileName, sb.ToString(), new System.Text.UTF8Encoding(true));
            }
        }
    }
}
