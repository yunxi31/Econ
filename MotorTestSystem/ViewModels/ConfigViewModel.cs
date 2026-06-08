using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MotorTestSystem.Models;

namespace MotorTestSystem.ViewModels
{
    public partial class ConfigViewModel : ViewModelBase
    {
        public ObservableCollection<StationConfig> Stations { get; } = new();

        public ConfigViewModel()
        {
            // Initialize config matching the PRD
            Stations.Add(new StationConfig { Id = "A1", Name = "空载测试机台 A1", PlcModel = "三菱 FX5U", IpAddress = "192.168.1.10", Port = 6000, Protocol = "MC Protocol (TCP)" });
            Stations.Add(new StationConfig { Id = "A2", Name = "空载测试机台 A2", PlcModel = "三菱 Q系列", IpAddress = "192.168.1.11", Port = 6000, Protocol = "MC Protocol (TCP)" });
            Stations.Add(new StationConfig { Id = "A3", Name = "噪音测试机台 A3", PlcModel = "西门子 S7-1200", IpAddress = "192.168.1.12", Port = 102, Protocol = "S7 Protocol (TCP)", StationId = 0 });
            Stations.Add(new StationConfig { Id = "A4", Name = "噪音测试机台 A4", PlcModel = "西门子 S7-1500", IpAddress = "192.168.1.13", Port = 102, Protocol = "S7 Protocol (TCP)", StationId = 0 });
            Stations.Add(new StationConfig { Id = "A5", Name = "负载测试机台 A5", PlcModel = "汇川 H5U", IpAddress = "192.168.1.14", Port = 502, Protocol = "ModbusTCP" });
            Stations.Add(new StationConfig { Id = "A6", Name = "负载测试机台 A6", PlcModel = "汇川 Easy521", IpAddress = "192.168.1.15", Port = 502, Protocol = "ModbusTCP" });
        }

        [RelayCommand]
        private async Task TestConnectionAsync(StationConfig config)
        {
            if (config == null) return;

            // Simulate testing connection
            await Task.Delay(1000); // Wait 1 second to simulate network latency

            // Randomize success for the demo (except Easy521 which is simulated as offline)
            bool isSuccess = config.Id != "A6"; 
            config.IsConnected = isSuccess;

            if (isSuccess)
            {
                MessageBox.Show($"{config.Name} ({config.IpAddress}:{config.Port}) 连接正常！", "连接测试成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"{config.Name} ({config.IpAddress}:{config.Port}) 无法建立连接，请检查IP设置与网线物理连接。", "连接测试失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
