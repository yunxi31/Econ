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
                    Name = config.Name,
                    PlcModel = config.PlcModel,
                    Protocol = config.Protocol,
                    Status = 0,
                    IsOnline = false,
                    Barcode = "...",
                    Result = "WAIT"
                };

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
