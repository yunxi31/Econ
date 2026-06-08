using System;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    public sealed class MockPlcClient : IPlcClient
    {
        private static readonly object CounterGate = new();
        private static long _barcodeCounter = 1992900399100;

        private readonly Random _random;
        private readonly TestStage _stage;
        private bool _isConnected;
        private int _pollCount;

        public MockPlcClient(StationConfig config)
        {
            Config = config;
            _stage = ResolveStage(config.Id);
            _random = new Random(config.Id.GetHashCode() ^ Environment.TickCount);
        }

        public StationConfig Config { get; }

        public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _isConnected = Config.Id != "A6" || _random.NextDouble() > 0.25;
            return Task.FromResult(_isConnected);
        }

        public Task<StationSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default)
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

            bool completed = _pollCount % _random.Next(2, 5) == 0;
            var data = completed ? CreateStageData() : null;

            return Task.FromResult(new StationSnapshot
            {
                StationId = Config.Id,
                IsOnline = true,
                Status = completed ? 1 : _random.Next(0, 2),
                CompletionSignal = completed,
                CompletedData = data
            });
        }

        public Task ResetCompletionSignalAsync(CancellationToken cancellationToken = default)
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

            return _stage switch
            {
                TestStage.NoLoad => CreateNoLoadData(barcode),
                TestStage.Noise => CreateNoiseData(barcode),
                TestStage.Load => CreateLoadData(barcode),
                _ => throw new InvalidOperationException($"Unsupported stage: {_stage}")
            };
        }

        private StageTestData CreateNoLoadData(string barcode)
        {
            double current = Math.Round(1.5 + _random.NextDouble() * 1.0, 3);
            int speed = _random.Next(1900, 2200);
            double shaftLength = Math.Round(32.0 + _random.NextDouble() * 0.9, 3);
            double knurlDiameter = Math.Round(4.2 + _random.NextDouble() * 0.5, 3);
            string result = current > 2.3 || knurlDiameter > 4.65 ? "NG" : "OK";

            return new StageTestData
            {
                Barcode = barcode,
                StationId = Config.Id,
                Stage = TestStage.NoLoad,
                CollectedAt = DateTime.Now,
                Result = result,
                NoLoadCurrent = current,
                NoLoadSpeed = speed,
                ShaftLength = shaftLength,
                KnurlDiameter = knurlDiameter
            };
        }

        private StageTestData CreateNoiseData(string barcode)
        {
            double fwdNoise = Math.Round(50.0 + _random.NextDouble() * 25.0, 2);
            double revNoise = Math.Round(50.0 + _random.NextDouble() * 20.0, 2);
            double noiseDiff = Math.Round(Math.Abs(fwdNoise - revNoise), 2);
            string result = noiseDiff > 10.0 || fwdNoise > 70.0 ? "NG" : "OK";

            return new StageTestData
            {
                Barcode = barcode,
                StationId = Config.Id,
                Stage = TestStage.Noise,
                CollectedAt = DateTime.Now,
                Result = result,
                FwdNoise = fwdNoise,
                RevNoise = revNoise,
                NoiseDiff = noiseDiff
            };
        }

        private StageTestData CreateLoadData(string barcode)
        {
            double current = Math.Round(2.0 + _random.NextDouble() * 1.5, 3);
            int speed = _random.Next(1100, 1300);
            string result = current > 3.2 ? "NG" : "OK";

            return new StageTestData
            {
                Barcode = barcode,
                StationId = Config.Id,
                Stage = TestStage.Load,
                CollectedAt = DateTime.Now,
                Result = result,
                LoadCurrent = current,
                LoadSpeed = speed
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

                int offset = _stage == TestStage.Noise ? 1 : 2;
                return $"DES-SR-150GEN{Math.Max(1992900399100L, _barcodeCounter - offset)}";
            }
        }

        private static TestStage ResolveStage(string stationId)
        {
            return stationId switch
            {
                "A1" or "A2" => TestStage.NoLoad,
                "A3" or "A4" => TestStage.Noise,
                "A5" or "A6" => TestStage.Load,
                _ => throw new ArgumentException($"Unknown station id: {stationId}", nameof(stationId))
            };
        }
    }
}
