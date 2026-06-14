using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorTestSystem.Models;
using MotorTestSystem.Services;

namespace MotorTestSystem.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly BackendRuntime _runtime;
        private readonly Dictionary<string, bool> _onlineStations = new(StringComparer.OrdinalIgnoreCase);
        private readonly DispatcherTimer _clockTimer;

        [ObservableProperty]
        private ViewModelBase _currentView;

        [ObservableProperty]
        private string _currentTime = DateTime.Now.ToString("HH:mm:ss");

        [ObservableProperty]
        private string _currentUser = "未登录";

        [ObservableProperty]
        private string _currentUserName = "未知";

        [ObservableProperty]
        private string _currentUserRole = "未知";

        [ObservableProperty]
        private int _onlineStationCount;

        [ObservableProperty]
        private int _totalStationCount;

        // ===== 导航可见性（基于 RBAC 权限） =====

        /// <summary>是否显示"生产监控"导航</summary>
        [ObservableProperty]
        private bool _isMonitorVisible = true;

        /// <summary>是否显示"数据追溯"导航</summary>
        [ObservableProperty]
        private bool _isHistoryVisible = true;

        /// <summary>是否显示"生产看板"导航</summary>
        [ObservableProperty]
        private bool _isDashboardVisible = true;

        /// <summary>是否显示"用户管理"导航</summary>
        [ObservableProperty]
        private bool _isUserVisible = true;

        /// <summary>是否显示"系统配置"导航</summary>
        [ObservableProperty]
        private bool _isConfigVisible = true;

        public DashboardViewModel DashboardVM { get; }
        public MonitorViewModel MonitorVM { get; }
        public HistoryViewModel HistoryVM { get; }
        public ConfigViewModel ConfigVM { get; }
        public UserViewModel UserVM { get; }
        public NotificationCenterViewModel NotificationVM { get; }
        public ObservableCollection<StationState> HeaderStations { get; } = new();

        public MainViewModel()
            : this(BackendRuntime.Shared)
        {
        }

        public MainViewModel(BackendRuntime runtime)
        {
            _runtime = runtime;
            TotalStationCount = _runtime.StationConfigs.Count;
            OnlineStationCount = 0;

            DashboardVM = new DashboardViewModel(_runtime.Repository, _runtime);
            MonitorVM = new MonitorViewModel(_runtime);
            HistoryVM = new HistoryViewModel(_runtime.Repository);
            ConfigVM = new ConfigViewModel(_runtime);
            UserVM = new UserViewModel(_runtime.UserService, _runtime.AuthService);
            NotificationVM = new NotificationCenterViewModel(_runtime.NotificationService);

            var allStations = MonitorVM.NoLoadStations
                .Concat(MonitorVM.NoiseStations)
                .Concat(MonitorVM.LoadStations)
                .OrderBy(s => s.Id);
            foreach (var station in allStations)
            {
                HeaderStations.Add(station);
            }

            NotificationVM.PageTitle = "通知中心";
            NotificationVM.PageIcon = "BellOutline";
            CurrentView = MonitorVM;

            _runtime.PollingService.SnapshotReceived += OnSnapshotReceived;

            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (_, _) => CurrentTime = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();
        }

        /// <summary>
        /// 由 App.xaml.cs 在登录成功后调用，设置当前认证用户并刷新权限
        /// </summary>
        public void SetAuthenticatedUser(AppUser? user)
        {
            if (user == null)
            {
                CurrentUser = "未登录";
                CurrentUserName = "未知";
                CurrentUserRole = "未知角色";
                return;
            }

            CurrentUser = $"{user.RoleDisplayName} ({user.Account})";
            CurrentUserName = user.Name;
            CurrentUserRole = user.RoleDisplayName;

            // 根据 RBAC 权限刷新导航可见性
            RefreshNavigationVisibility();
        }

        /// <summary>
        /// 根据当前用户的角色权限，刷新各导航项的可见性
        /// </summary>
        private void RefreshNavigationVisibility()
        {
            var auth = _runtime.AuthService;

            IsMonitorVisible = auth.HasPermission(AppPermission.Monitor);
            IsHistoryVisible = auth.HasPermission(AppPermission.History);
            IsDashboardVisible = auth.HasPermission(AppPermission.Dashboard);
            IsUserVisible = auth.HasPermission(AppPermission.UserManagement);
            IsConfigVisible = auth.HasPermission(AppPermission.SystemConfig);

            // 如果当前页面不可见了，自动切换到第一个可见页面
            if (CurrentView == MonitorVM && !IsMonitorVisible) NavigateToFirstVisible();
            else if (CurrentView == HistoryVM && !IsHistoryVisible) NavigateToFirstVisible();
            else if (CurrentView == DashboardVM && !IsDashboardVisible) NavigateToFirstVisible();
            else if (CurrentView == UserVM && !IsUserVisible) NavigateToFirstVisible();
            else if (CurrentView == ConfigVM && !IsConfigVisible) NavigateToFirstVisible();
        }

        private void NavigateToFirstVisible()
        {
            if (IsMonitorVisible) CurrentView = MonitorVM;
            else if (IsDashboardVisible) CurrentView = DashboardVM;
            else if (IsHistoryVisible) CurrentView = HistoryVM;
            else CurrentView = MonitorVM; // 兜底
        }

        public bool IsNotificationCenterActive => CurrentView == NotificationVM && NotificationVM?.PageTitle == "通知中心";

        partial void OnCurrentViewChanged(ViewModelBase value)
        {
            OnPropertyChanged(nameof(IsNotificationCenterActive));
        }

        private ViewModelBase? _previousViewBeforeNotification;

        [RelayCommand]
        private void Navigate(string destination)
        {
            if (destination == "LogCenter")
            {
                _previousViewBeforeNotification = null;
                NotificationVM.PageTitle = "日志中心";
                NotificationVM.PageIcon = "FolderOpenOutline";
                CurrentView = NotificationVM;
                OnPropertyChanged(nameof(IsNotificationCenterActive));
                return;
            }

            if (destination == "Notification")
            {
                if (CurrentView == NotificationVM && NotificationVM.PageTitle == "通知中心")
                {
                    // 已经在通知中心模式，toggle 回上一页
                    CurrentView = _previousViewBeforeNotification ?? MonitorVM;
                }
                else
                {
                    _previousViewBeforeNotification = CurrentView == NotificationVM ? null : CurrentView;
                    NotificationVM.PageTitle = "通知中心";
                    NotificationVM.PageIcon = "BellOutline";
                    // 先设为 null 再设回，强制触发 PropertyChanged（同一实例引用时不会自动触发）
                    CurrentView = null!;
                    CurrentView = NotificationVM;
                }
                OnPropertyChanged(nameof(IsNotificationCenterActive));
                return;
            }

            // RBAC 权限检查
            if (!IsNavigationAllowed(destination))
            {
                // 无权限时不切换，保持当前页面
                return;
            }

            _previousViewBeforeNotification = null;

            if (destination == "History")
            {
                HistoryVM.EnsureInitialized();
                CurrentView = HistoryVM;
                return;
            }

            CurrentView = destination switch
            {
                "Dashboard" => DashboardVM,
                "Monitor" => MonitorVM,
                "History" => HistoryVM,
                "Config" => ConfigVM,
                "User" => UserVM,
                "LogCenter" => NotificationVM,
                _ => DashboardVM
            };
        }

        /// <summary>
        /// 判断当前用户是否有权访问指定导航目标
        /// </summary>
        private bool IsNavigationAllowed(string destination) => destination switch
        {
            "Monitor" => IsMonitorVisible,
            "History" => IsHistoryVisible,
            "Dashboard" => IsDashboardVisible,
            "User" => IsUserVisible,
            "Config" => IsConfigVisible,
            "LogCenter" => true,
            _ => true
        };

        private void OnSnapshotReceived(object? sender, StationSnapshot snapshot)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                ApplyOnlineState(snapshot);
                return;
            }

            dispatcher.InvokeAsync(() => ApplyOnlineState(snapshot));
        }

        private void ApplyOnlineState(StationSnapshot snapshot)
        {
            _onlineStations[snapshot.StationId] = snapshot.IsOnline;
            OnlineStationCount = _onlineStations.Count(kvp => kvp.Value);
        }

        partial void OnCurrentUserChanged(string value)
        {
            // 保留兼容：如果外部直接设置 CurrentUser（非通过 SetAuthenticatedUser），
            // 仍然能解析角色名和用户名
            if (string.IsNullOrEmpty(value) || value == "未登录")
            {
                CurrentUserRole = "未知角色";
                CurrentUserName = "未知用户";
                return;
            }

            int index = value.IndexOf('(');
            if (index > 0)
            {
                CurrentUserRole = value.Substring(0, index).Trim();
                CurrentUserName = value.Substring(index + 1).Replace(")", "").Trim();
            }
            else
            {
                CurrentUserRole = value;
                CurrentUserName = value;
            }
        }
    }
}
