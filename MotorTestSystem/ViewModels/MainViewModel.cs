using System;
using System.Collections.Generic;
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
        private string _currentTime = DateTime.UtcNow.ToString("HH:mm:ss");

        [ObservableProperty]
        private string _currentUser = "管理员";

        [ObservableProperty]
        private string _currentUserName = "admin";

        [ObservableProperty]
        private string _currentUserRole = "管理员";

        [ObservableProperty]
        private int _onlineStationCount;

        [ObservableProperty]
        private int _totalStationCount;

        public DashboardViewModel DashboardVM { get; }
        public MonitorViewModel MonitorVM { get; }
        public HistoryViewModel HistoryVM { get; }
        public ConfigViewModel ConfigVM { get; }
        public UserViewModel UserVM { get; }

        public MainViewModel()
            : this(BackendRuntime.Shared)
        {
        }

        public MainViewModel(BackendRuntime runtime)
        {
            _runtime = runtime;
            TotalStationCount = _runtime.StationConfigs.Count;
            OnlineStationCount = 0;

            DashboardVM = new DashboardViewModel(_runtime.Repository);
            MonitorVM = new MonitorViewModel(_runtime);
            HistoryVM = new HistoryViewModel(_runtime.Repository);
            ConfigVM = new ConfigViewModel(_runtime);
            UserVM = new UserViewModel();

            _currentView = MonitorVM;

            _runtime.PollingService.SnapshotReceived += OnSnapshotReceived;

            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (_, _) => CurrentTime = DateTime.UtcNow.ToString("HH:mm:ss");
            _clockTimer.Start();
        }

        [RelayCommand]
        private void Navigate(string destination)
        {
            CurrentView = destination switch
            {
                "Dashboard" => DashboardVM,
                "Monitor" => MonitorVM,
                "History" => HistoryVM,
                "Config" => ConfigVM,
                "User" => UserVM,
                _ => DashboardVM
            };
        }

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
            if (string.IsNullOrEmpty(value))
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
