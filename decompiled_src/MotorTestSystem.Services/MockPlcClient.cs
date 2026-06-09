using System;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services;

public sealed class MockPlcClient : IPlcClient, IDisposable
{
	private static readonly object CounterGate = new object();

	private static long _barcodeCounter = 1992900399100L;

	private readonly Random _random;

	private readonly TestStage _stage;

	private bool _isConnected;

	private int _pollCount;

	public StationConfig Config { get; }

	public MockPlcClient(StationConfig config)
	{
		Config = config;
		_stage = ResolveStage(config.Id);
		_random = new Random(config.Id.GetHashCode() ^ Environment.TickCount);
	}

	public Task<bool> ConnectAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		cancellationToken.ThrowIfCancellationRequested();
		_isConnected = Config.Id != "A6" || _random.NextDouble() > 0.25;
		return Task.FromResult(_isConnected);
	}

	public Task<StationSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		cancellationToken.ThrowIfCancellationRequested();
		_pollCount++;
		if (!_isConnected)
		{
			return Task.FromResult(new StationSnapshot
			{
				StationId = Config.Id,
				IsOnline = false,
				Status = 2,
				CompletionSignal = false
			});
		}
		bool flag = _pollCount % _random.Next(2, 5) == 0;
		StageTestData completedData = (flag ? CreateStageData() : null);
		return Task.FromResult(new StationSnapshot
		{
			StationId = Config.Id,
			IsOnline = true,
			Status = (flag ? 1 : _random.Next(0, 2)),
			CompletionSignal = flag,
			CompletedData = completedData
		});
	}

	public Task ResetCompletionSignalAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		cancellationToken.ThrowIfCancellationRequested();
		return Task.CompletedTask;
	}

	public void Dispose()
	{
	}

	private StageTestData CreateStageData()
	{
		string barcode = CreateBarcode();
		TestStage stage = _stage;
		if (1 == 0)
		{
		}
		StageTestData result = stage switch
		{
			TestStage.NoLoad => CreateNoLoadData(barcode), 
			TestStage.Noise => CreateNoiseData(barcode), 
			TestStage.Load => CreateLoadData(barcode), 
			_ => throw new InvalidOperationException($"Unsupported stage: {_stage}"), 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private StageTestData CreateNoLoadData(string barcode)
	{
		double num = Math.Round(1.5 + _random.NextDouble() * 1.0, 3);
		int value = _random.Next(1900, 2200);
		double value2 = Math.Round(32.0 + _random.NextDouble() * 0.9, 3);
		double num2 = Math.Round(4.2 + _random.NextDouble() * 0.5, 3);
		string result = ((num > 2.3 || num2 > 4.65) ? "NG" : "OK");
		return new StageTestData
		{
			Barcode = barcode,
			StationId = Config.Id,
			Stage = TestStage.NoLoad,
			CollectedAt = DateTime.Now,
			Result = result,
			NoLoadCurrent = num,
			NoLoadSpeed = value,
			ShaftLength = value2,
			KnurlDiameter = num2
		};
	}

	private StageTestData CreateNoiseData(string barcode)
	{
		double num = Math.Round(50.0 + _random.NextDouble() * 25.0, 2);
		double num2 = Math.Round(50.0 + _random.NextDouble() * 20.0, 2);
		double num3 = Math.Round(Math.Abs(num - num2), 2);
		string result = ((num3 > 10.0 || num > 70.0) ? "NG" : "OK");
		return new StageTestData
		{
			Barcode = barcode,
			StationId = Config.Id,
			Stage = TestStage.Noise,
			CollectedAt = DateTime.Now,
			Result = result,
			FwdNoise = num,
			RevNoise = num2,
			NoiseDiff = num3
		};
	}

	private StageTestData CreateLoadData(string barcode)
	{
		double num = Math.Round(2.0 + _random.NextDouble() * 1.5, 3);
		int value = _random.Next(1100, 1300);
		string result = ((num > 3.2) ? "NG" : "OK");
		return new StageTestData
		{
			Barcode = barcode,
			StationId = Config.Id,
			Stage = TestStage.Load,
			CollectedAt = DateTime.Now,
			Result = result,
			LoadCurrent = num,
			LoadSpeed = value
		};
	}

	private string CreateBarcode()
	{
		lock (CounterGate)
		{
			if (_stage == TestStage.NoLoad)
			{
				_barcodeCounter++;
				return $"DES-SR-150GEN{_barcodeCounter}";
			}
			int num = ((_stage == TestStage.Noise) ? 1 : 2);
			return $"DES-SR-150GEN{Math.Max(1992900399100L, _barcodeCounter - num)}";
		}
	}

	private static TestStage ResolveStage(string stationId)
	{
		if (1 == 0)
		{
		}
		TestStage result;
		switch (stationId)
		{
		case "A1":
		case "A2":
			result = TestStage.NoLoad;
			break;
		case "A3":
		case "A4":
			result = TestStage.Noise;
			break;
		case "A5":
		case "A6":
			result = TestStage.Load;
			break;
		default:
			throw new ArgumentException("Unknown station id: " + stationId, "stationId");
		}
		if (1 == 0)
		{
		}
		return result;
	}
}
