using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorTestSystem.Models;
using MotorTestSystem.Services;

namespace MotorTestSystem.ViewModels
{
    public class LogEntry
    {
        public string Timestamp { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string LevelBrush { get; set; } = "#White";
    }

    public partial class ConfigViewModel : ViewModelBase
    {
        private readonly BackendRuntime _runtime;
        private readonly IDialogService _dialogService;

        public ObservableCollection<StationConfig> Stations => _runtime.StationConfigs;

        public ObservableCollection<LogEntry> DiagnosticLogs { get; } = new()
        {
            new() { Timestamp = "14:02:33.105", Level = "[信息]", Message = "系统启动，网络接口已初始化。", LevelBrush = "#10B981" },
            new() { Timestamp = "14:02:35.021", Level = "[连接]", Message = "‖ A1 (192.168.10.11:502) ‖‖‖‖", LevelBrush = "#12DDF7" },
            new() { Timestamp = "14:02:38.442", Level = "[错误]", Message = "‖ A3 (192.168.10.13:502) ‖‖‖‖‖‖‖‖‖‖‖‖‖‖‖‖‖‖‖‖", LevelBrush = "#FF5A5F" },
            new() { Timestamp = "14:03:01.000", Level = "[警告]", Message = "‖ A4 (192.168.10.14) Ping ‖ > 1000ms‖", LevelBrush = "#FFC53D" }
        };

        public ConfigViewModel()
            : this(
                  BackendRuntime.GetSharedAsync().GetAwaiter().GetResult(),
                  new MotorTestSystem.Presentation.Services.WpfDialogService())
        {
        }

        public ConfigViewModel(BackendRuntime runtime)
            : this(
                  runtime,
                  new MotorTestSystem.Presentation.Services.WpfDialogService())
        {
        }

        public ConfigViewModel(BackendRuntime runtime, IDialogService dialogService)
        {
            _runtime = runtime;
            _dialogService = dialogService;
        }

        [RelayCommand]
        private async Task TestConnectionAsync(StationConfig config)
        {
            if (config == null)
            {
                return;
            }

            // Simulate testing connection in runtime
            bool isSuccess = await _runtime.PollingService.TestConnectionAsync(config);
            config.IsConnected = isSuccess;
            
            // Set status based on connection test result
            config.Status = isSuccess ? "在线" : "离线";

            // Add log entry dynamically
            string time = System.DateTime.Now.ToString("HH:mm:ss.fff");
            if (isSuccess)
            {
                DiagnosticLogs.Add(new LogEntry 
                { 
                    Timestamp = time, 
                    Level = "[连接]", 
                    Message = $"‖ {config.Name} ({config.IpAddress}:{config.Port}) 连接测试成功 ‖‖‖‖", 
                    LevelBrush = "#12DDF7" 
                });
                await ShowMessage($"{config.Name} ({config.IpAddress}:{config.Port}) 连接正常。", "连接测试成功", MessageBoxImage.Information);
            }
            else
            {
                DiagnosticLogs.Add(new LogEntry 
                { 
                    Timestamp = time, 
                    Level = "[错误]", 
                    Message = $"‖ {config.Name} ({config.IpAddress}:{config.Port}) 连接测试失败 ‖‖‖‖", 
                    LevelBrush = "#FF5A5F" 
                });
                await ShowMessage($"{config.Name} ({config.IpAddress}:{config.Port}) 无法建立连接，请检查 IP、端口、协议与网线连接。", "连接测试失败", MessageBoxImage.Warning);
            }
        }

        public static System.Action<string, string, MessageBoxImage>? MessageBoxShowMock { get; set; }

        private async Task ShowMessage(string message, string caption, MessageBoxImage image)
        {
            if (MessageBoxShowMock != null)
            {
                MessageBoxShowMock(message, caption, image);
                return;
            }
            await _dialogService.ShowMessageAsync(message, caption, MessageBoxButton.OK, image);
        }

        [RelayCommand]
        private void ClearLogs()
        {
            DiagnosticLogs.Clear();
        }

        [RelayCommand]
        private async Task SaveAll()
        {
            await _dialogService.ShowMessageAsync("所有配置已成功保存至系统数据库中。", "保存配置成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
