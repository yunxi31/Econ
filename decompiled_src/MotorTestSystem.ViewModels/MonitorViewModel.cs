using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using MotorTestSystem.Models;
using MotorTestSystem.Services;

namespace MotorTestSystem.ViewModels;

public class MonitorViewModel : ViewModelBase
{
	private readonly BackendRuntime _runtime;

	private readonly Dictionary<string, StationState> _stationsById = new Dictionary<string, StationState>(StringComparer.OrdinalIgnoreCase);

	public ObservableCollection<StationState> NoLoadStations { get; } = new ObservableCollection<StationState>();

	public ObservableCollection<StationState> NoiseStations { get; } = new ObservableCollection<StationState>();

	public ObservableCollection<StationState> LoadStations { get; } = new ObservableCollection<StationState>();

	public ObservableCollection<string> SystemLogs { get; } = new ObservableCollection<string>();

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
		foreach (StationConfig stationConfig in _runtime.StationConfigs)
		{
			StationState stationState = new StationState
			{
				Id = stationConfig.Id,
				Name = stationConfig.Id,
				PlcModel = ResolveDisplayModel(stationConfig.Id),
				Protocol = stationConfig.Protocol,
				Status = ResolveInitialStatus(stationConfig.Id),
				IsOnline = true,
				Barcode = ResolveInitialBarcode(stationConfig.Id),
				Result = ResolveInitialResult(stationConfig.Id)
			};
			ApplyInitialMeasurements(stationState);
			_stationsById[stationConfig.Id] = stationState;
			string id = stationConfig.Id;
			if ((id == "A1" || id == "A2") ? true : false)
			{
				NoLoadStations.Add(stationState);
				continue;
			}
			id = stationConfig.Id;
			if ((id == "A3" || id == "A4") ? true : false)
			{
				NoiseStations.Add(stationState);
			}
			else
			{
				LoadStations.Add(stationState);
			}
		}
	}

	private static string ResolveDisplayModel(string stationId)
	{
		if (1 == 0)
		{
		}
		string result;
		switch (stationId)
		{
		case "A1":
		case "A2":
			result = "FX5U";
			break;
		case "A3":
		case "A4":
			result = "NV-X";
			break;
		case "A5":
		case "A6":
			result = "LD-Max";
			break;
		default:
			result = "PLC";
			break;
		}
		if (1 == 0)
		{
		}
		return result;
	}

	private static int ResolveInitialStatus(string stationId)
	{
		if (1 == 0)
		{
		}
		int result;
		switch (stationId)
		{
		case "A1":
		case "A3":
		case "A6":
			result = 1;
			break;
		case "A4":
			result = 2;
			break;
		default:
			result = 0;
			break;
		}
		if (1 == 0)
		{
		}
		return result;
	}

	private static string ResolveInitialBarcode(string stationId)
	{
		if (1 == 0)
		{
		}
		string result = stationId switch
		{
			"A1" => "SN-99483-XA1", 
			"A2" => "等待中", 
			"A3" => "SN-99482-XA1", 
			"A4" => "SN-99480-XA1", 
			"A5" => "SN-99481-XA1", 
			"A6" => "SN-99482-XA1", 
			_ => "SN-00000-XA1", 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private static string ResolveInitialResult(string stationId)
	{
		if (1 == 0)
		{
		}
		string result = stationId switch
		{
			"A4" => "NG", 
			"A5" => "OK", 
			"A2" => "WAIT", 
			_ => "RUN", 
		};
		if (1 == 0)
		{
		}
		return result;
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
		RunOnUiThread(delegate
		{
			ApplySnapshot(snapshot);
		});
	}

	private void OnLogReceived(object? sender, string message)
	{
		RunOnUiThread(delegate
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
		if (_stationsById.TryGetValue(snapshot.StationId, out StationState value))
		{
			value.IsOnline = snapshot.IsOnline;
			value.Status = snapshot.Status;
			value.HandshakeSignal = snapshot.CompletionSignal;
			if (snapshot.CompletedData != null)
			{
				StageTestData completedData = snapshot.CompletedData;
				value.Barcode = completedData.Barcode;
				value.Result = completedData.Result;
				value.NoLoadCurrent = completedData.NoLoadCurrent ?? value.NoLoadCurrent;
				value.NoLoadSpeed = completedData.NoLoadSpeed ?? value.NoLoadSpeed;
				value.ShaftLength = completedData.ShaftLength ?? value.ShaftLength;
				value.KnurlDiameter = completedData.KnurlDiameter ?? value.KnurlDiameter;
				value.FwdNoise = completedData.FwdNoise ?? value.FwdNoise;
				value.RevNoise = completedData.RevNoise ?? value.RevNoise;
				value.NoiseDiff = completedData.NoiseDiff ?? value.NoiseDiff;
				value.LoadCurrent = completedData.LoadCurrent ?? value.LoadCurrent;
				value.LoadSpeed = completedData.LoadSpeed ?? value.LoadSpeed;
			}
		}
	}

	private static void RunOnUiThread(Action action)
	{
		Application current = Application.Current;
		Dispatcher val = ((current != null) ? ((DispatcherObject)current).Dispatcher : null);
		if (val == null || val.CheckAccess())
		{
			action();
		}
		else
		{
			val.InvokeAsync(action);
		}
	}
}
