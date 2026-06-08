using CommunityToolkit.Mvvm.ComponentModel;

namespace MotorTestSystem.Models
{
    public partial class StationState : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _plcModel = string.Empty;

        [ObservableProperty]
        private string _protocol = string.Empty;

        [ObservableProperty]
        private string _barcode = "等待扫码...";

        [ObservableProperty]
        private int _status = 0; // 0-待机, 1-运行, 2-故障

        [ObservableProperty]
        private bool _isOnline = true;

        [ObservableProperty]
        private string _result = "WAIT"; // OK, NG, WAIT

        [ObservableProperty]
        private bool _handshakeSignal = false;

        // Station specific measurements (A1/A2)
        [ObservableProperty]
        private double _noLoadCurrent;

        [ObservableProperty]
        private int _noLoadSpeed;

        [ObservableProperty]
        private double _shaftLength;

        [ObservableProperty]
        private double _knurlDiameter;

        // Station specific measurements (A3/A4)
        [ObservableProperty]
        private double _fwdNoise;

        [ObservableProperty]
        private double _revNoise;

        [ObservableProperty]
        private double _noiseDiff;

        // Station specific measurements (A5/A6)
        [ObservableProperty]
        private double _loadCurrent;

        [ObservableProperty]
        private int _loadSpeed;
    }
}
