using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using MotorTestSystem.Models;
using MotorTestSystem.Services;

namespace MotorTestSystem.ViewModels
{
    public partial class MonitorViewModel : ViewModelBase
    {
        private readonly BackendRuntime _runtime;
        private readonly Dictionary<string, StationState> _stationsById = new(StringComparer.OrdinalIgnoreCase);

        public ObservableCollection<StationState> NoLoadStations { get; } = new();
        public ObservableCollection<StationState> NoiseStations { get; } = new();
        public ObservableCollection<StationState> LoadStations { get; } = new();
        public ObservableCollection<string> SystemLogs { get; } = new();

        public MonitorViewModel()
            : this(BackendRuntime.Shared)
        {
        }

        public MonitorViewModel(BackendRuntime runtime)
        {
            _runtime = runtime;
            BuildStationStates();

            _runtime.PollingService.SnapshotReceived += OnSnapshotReceived;
            _runtime.PollingService.LogReceived += OnLogReceived;
            _runtime.PollingService.Start();
        }

        private void BuildStationStates()
        {
            foreach (var config in _runtime.StationConfigs)
            {
                var state = new StationState
                {
                    Id = config.Id,
                    Name = config.Id,
                    PlcModel = ResolveDisplayModel(config.Id),
                    Protocol = config.Protocol,
                    Status = ResolveInitialStatus(config.Id),
                    IsOnline = true,
                    Barcode = ResolveInitialBarcode(config.Id),
                    Result = ResolveInitialResult(config.Id)
                };

                ApplyInitialMeasurements(state);

                _stationsById[config.Id] = state;

                if (config.Id is "A1" or "A2")
                {
                    NoLoadStations.Add(state);
                }
                else if (config.Id is "A3" or "A4")
                {
                    NoiseStations.Add(state);
                }
                else
                {
                    LoadStations.Add(state);
                }
            }
        }

        private static string ResolveDisplayModel(string stationId)
        {
            return stationId switch
            {
                "A1" or "A2" => "FX5U",
                "A3" or "A4" => "NV-X",
                "A5" or "A6" => "LD-Max",
                _ => "PLC"
            };
        }

        private static int ResolveInitialStatus(string stationId)
        {
            return stationId switch
            {
                "A1" or "A3" or "A6" => 1,
                "A4" => 2,
                _ => 0
            };
        }

        private static string ResolveInitialBarcode(string stationId)
        {
            return stationId switch
            {
                "A1" => "SN-99483-XA1",
                "A2" => "等待中",
                "A3" => "SN-99482-XA1",
                "A4" => "SN-99480-XA1",
                "A5" => "SN-99481-XA1",
                "A6" => "SN-99482-XA1",
                _ => "SN-00000-XA1"
            };
        }

        private static string ResolveInitialResult(string stationId)
        {
            return stationId switch
            {
                "A4" => "NG",
                "A5" => "OK",
                "A2" => "WAIT",
                _ => "RUN"
            };
        }

        private static void ApplyInitialMeasurements(StationState state)
        {
            switch (state.Id)
            {
                case "A1":
                    state.NoLoadCurrent = 1.2;
                    state.NoLoadSpeed = 1450;
                    break;
                case "A3":
                    state.FwdNoise = 62.4;
                    state.RevNoise = 0.8;
                    state.NoiseDiff = 28.5;
                    break;
                case "A4":
                    state.FwdNoise = 78.9;
                    state.NoiseDiff = 65.0;
                    break;
                case "A5":
                    state.LoadCurrent = 15.2;
                    state.LoadSpeed = 42;
                    break;
                case "A6":
                    state.LoadCurrent = 10.1;
                    state.LoadSpeed = 38;
                    break;
            }
        }

        private void OnSnapshotReceived(object? sender, StationSnapshot snapshot)
        {
            RunOnUiThread(() => ApplySnapshot(snapshot));
        }

        private void OnLogReceived(object? sender, string message)
        {
            RunOnUiThread(() =>
            {
                SystemLogs.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}");
                while (SystemLogs.Count > 10)
                {
                    SystemLogs.RemoveAt(SystemLogs.Count - 1);
                }
            });
        }

        private void ApplySnapshot(StationSnapshot snapshot)
        {
            if (!_stationsById.TryGetValue(snapshot.StationId, out var state))
            {
                return;
            }

            state.IsOnline = snapshot.IsOnline;
            state.Status = snapshot.Status;
            state.HandshakeSignal = snapshot.CompletionSignal;

            if (snapshot.CompletedData == null)
            {
                return;
            }

            var data = snapshot.CompletedData;
            state.Barcode = data.Barcode;
            state.Result = data.Result;

            state.NoLoadCurrent = data.NoLoadCurrent ?? state.NoLoadCurrent;
            state.NoLoadSpeed = data.NoLoadSpeed ?? state.NoLoadSpeed;
            state.ShaftLength = data.ShaftLength ?? state.ShaftLength;
            state.KnurlDiameter = data.KnurlDiameter ?? state.KnurlDiameter;

            state.FwdNoise = data.FwdNoise ?? state.FwdNoise;
            state.RevNoise = data.RevNoise ?? state.RevNoise;
            state.NoiseDiff = data.NoiseDiff ?? state.NoiseDiff;

            state.LoadCurrent = data.LoadCurrent ?? state.LoadCurrent;
            state.LoadSpeed = data.LoadSpeed ?? state.LoadSpeed;
        }

        private static void RunOnUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.InvokeAsync(action);
        }
    }
}
