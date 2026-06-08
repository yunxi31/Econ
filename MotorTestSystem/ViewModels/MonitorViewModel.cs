using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MotorTestSystem.Models;

namespace MotorTestSystem.ViewModels
{
    public partial class MonitorViewModel : ViewModelBase
    {
        public ObservableCollection<StationState> NoLoadStations { get; } = new();
        public ObservableCollection<StationState> NoiseStations { get; } = new();
        public ObservableCollection<StationState> LoadStations { get; } = new();

        private readonly DispatcherTimer _simulationTimer;
        private readonly Random _random = new();
        private int _barcodeCounter = 1000;

        public MonitorViewModel()
        {
            // A1 & A2 (No Load)
            NoLoadStations.Add(new StationState
            {
                Name = "机台 A1",
                PlcModel = "三菱 FX5U",
                Protocol = "MC Protocol (TCP)",
                Status = 1,
                IsOnline = true,
                Barcode = "M-FX5U-99081",
                NoLoadCurrent = 1.85,
                NoLoadSpeed = 2050,
                ShaftLength = 32.42,
                KnurlDiameter = 4.41,
                Result = "OK"
            });
            NoLoadStations.Add(new StationState
            {
                Name = "机台 A2",
                PlcModel = "三菱 Q系列",
                Protocol = "MC Protocol (TCP)",
                Status = 0,
                IsOnline = true,
                Barcode = "M-QSER-99082",
                NoLoadCurrent = 1.98,
                NoLoadSpeed = 2080,
                ShaftLength = 32.45,
                KnurlDiameter = 4.43,
                Result = "OK"
            });

            // A3 & A4 (Noise)
            NoiseStations.Add(new StationState
            {
                Name = "机台 A3",
                PlcModel = "西门子 S7-1200",
                Protocol = "S7 Protocol (TCP)",
                Status = 1,
                IsOnline = true,
                Barcode = "M-S120-99079",
                FwdNoise = 64.2,
                RevNoise = 58.1,
                NoiseDiff = 6.1,
                Result = "OK"
            });
            NoiseStations.Add(new StationState
            {
                Name = "机台 A4",
                PlcModel = "西门子 S7-1500",
                Protocol = "S7 Protocol (TCP)",
                Status = 2, // Faulted
                IsOnline = true,
                Barcode = "M-S150-ERR01",
                FwdNoise = 78.5,
                RevNoise = 62.4,
                NoiseDiff = 16.1,
                Result = "NG"
            });

            // A5 & A6 (Load)
            LoadStations.Add(new StationState
            {
                Name = "机台 A5",
                PlcModel = "汇川 H5U",
                Protocol = "ModbusTCP",
                Status = 1,
                IsOnline = true,
                Barcode = "M-HC5U-99075",
                LoadCurrent = 2.42,
                LoadSpeed = 1195,
                Result = "OK"
            });
            LoadStations.Add(new StationState
            {
                Name = "机台 A6",
                PlcModel = "汇川 Easy521",
                Protocol = "ModbusTCP",
                Status = 0,
                IsOnline = false, // Offline
                Barcode = "...",
                LoadCurrent = 0,
                LoadSpeed = 0,
                Result = "WAIT"
            });

            // Run live simulation timer
            _simulationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _simulationTimer.Tick += RunSimulation;
            _simulationTimer.Start();
        }

        private void RunSimulation(object? sender, EventArgs e)
        {
            _barcodeCounter++;
            string baseBarcode = $"DES-SR-150GEN-{_barcodeCounter}";

            // Simulate No Load Station A1 updates
            var a1 = NoLoadStations[0];
            if (a1.IsOnline)
            {
                a1.Status = _random.Next(0, 3);
                if (a1.Status == 1) // Running
                {
                    a1.Barcode = baseBarcode;
                    a1.NoLoadCurrent = Math.Round(1.5 + _random.NextDouble() * 1.0, 3);
                    a1.NoLoadSpeed = _random.Next(1900, 2200);
                    a1.ShaftLength = Math.Round(32.0 + _random.NextDouble() * 0.9, 3);
                    a1.KnurlDiameter = Math.Round(4.2 + _random.NextDouble() * 0.5, 3);
                    a1.Result = a1.NoLoadCurrent > 2.3 || a1.KnurlDiameter > 4.65 ? "NG" : "OK";
                    a1.HandshakeSignal = true;
                }
                else if (a1.Status == 0)
                {
                    a1.HandshakeSignal = false;
                }
            }

            // Simulate Noise Station A3 updates
            var a3 = NoiseStations[0];
            if (a3.IsOnline && _random.NextDouble() > 0.4)
            {
                a3.Status = 1;
                a3.Barcode = $"DES-SR-150GEN-{_barcodeCounter - 1}";
                a3.FwdNoise = Math.Round(50.0 + _random.NextDouble() * 25.0, 1);
                a3.RevNoise = Math.Round(50.0 + _random.NextDouble() * 20.0, 1);
                a3.NoiseDiff = Math.Round(Math.Abs(a3.FwdNoise - a3.RevNoise), 1);
                a3.Result = a3.NoiseDiff > 10.0 || a3.FwdNoise > 70.0 ? "NG" : "OK";
            }

            // Simulate Load Station A5 updates
            var a5 = LoadStations[0];
            if (a5.IsOnline && _random.NextDouble() > 0.5)
            {
                a5.Status = 1;
                a5.Barcode = $"DES-SR-150GEN-{_barcodeCounter - 2}";
                a5.LoadCurrent = Math.Round(2.0 + _random.NextDouble() * 1.5, 3);
                a5.LoadSpeed = _random.Next(1100, 1300);
                a5.Result = a5.LoadCurrent > 3.2 ? "NG" : "OK";
            }
        }
    }
}
