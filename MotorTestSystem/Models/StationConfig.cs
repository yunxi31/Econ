using CommunityToolkit.Mvvm.ComponentModel;

namespace MotorTestSystem.Models
{
    public partial class StationConfig : ObservableObject
    {
        [ObservableProperty]
        private string _id = string.Empty; // A1 - A6

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _plcModel = string.Empty;

        [ObservableProperty]
        private string _ipAddress = "192.168.1.1";

        [ObservableProperty]
        private int _port = 502;

        [ObservableProperty]
        private string _protocol = "ModbusTCP";

        [ObservableProperty]
        private byte _stationId = 1;

        [ObservableProperty]
        private bool _isConnected = false;
    }
}
