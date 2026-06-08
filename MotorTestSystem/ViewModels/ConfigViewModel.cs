using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using MotorTestSystem.Models;
using MotorTestSystem.Services;

namespace MotorTestSystem.ViewModels
{
    public partial class ConfigViewModel : ViewModelBase
    {
        private readonly BackendRuntime _runtime;

        public ObservableCollection<StationConfig> Stations => _runtime.StationConfigs;

        public ConfigViewModel()
            : this(BackendRuntime.Shared)
        {
        }

        public ConfigViewModel(BackendRuntime runtime)
        {
            _runtime = runtime;
        }

        [RelayCommand]
        private async Task TestConnectionAsync(StationConfig config)
        {
            if (config == null)
            {
                return;
            }

            bool isSuccess = await _runtime.PollingService.TestConnectionAsync(config);
            config.IsConnected = isSuccess;

            if (isSuccess)
            {
                MessageBox.Show($"{config.Name} ({config.IpAddress}:{config.Port}) 连接正常。", "连接测试成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"{config.Name} ({config.IpAddress}:{config.Port}) 无法建立连接，请检查 IP、端口、协议与网线连接。", "连接测试失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
