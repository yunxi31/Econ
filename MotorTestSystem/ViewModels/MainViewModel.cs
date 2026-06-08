using System;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MotorTestSystem.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ViewModelBase _currentView;

        [ObservableProperty]
        private string _currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        [ObservableProperty]
        private string _currentUser = "管理员 (Admin)";

        [ObservableProperty]
        private int _onlineStationCount = 5;

        [ObservableProperty]
        private int _totalStationCount = 6;

        public DashboardViewModel DashboardVM { get; } = new();
        public MonitorViewModel MonitorVM { get; } = new();
        public HistoryViewModel HistoryVM { get; } = new();
        public ConfigViewModel ConfigVM { get; } = new();

        private readonly DispatcherTimer _clockTimer;

        public MainViewModel()
        {
            // Set default view
            _currentView = DashboardVM;

            // Timer for current clock in header
            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (s, e) => CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
                _ => DashboardVM
            };
        }
    }
}
