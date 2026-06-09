using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    public sealed class BackendRuntime
    {
        public static BackendRuntime Shared { get; } = CreateDefault();

        public BackendRuntime(
            ObservableCollection<StationConfig> stationConfigs,
            IMotorTestRepository repository,
            IPlcClientFactory plcClientFactory)
        {
            StationConfigs = stationConfigs;
            Repository = repository;
            PollingService = new PlcPollingService(StationConfigs, Repository, plcClientFactory);
        }

        public ObservableCollection<StationConfig> StationConfigs { get; }
        public IMotorTestRepository Repository { get; }
        public PlcPollingService PollingService { get; }

        private static BackendRuntime CreateDefault()
        {
            var configs = new ObservableCollection<StationConfig>
            {
                new() { Id = "A1", Name = "A1", PlcModel = "FX5U", IpAddress = "192.168.10.11", Port = 502, Protocol = "ModbusTCP", IsConnected = true, Status = "在线" },
                new() { Id = "A2", Name = "A2", PlcModel = "S7-1200", IpAddress = "192.168.10.12", Port = 102, Protocol = "S7 Protocol (TCP)", IsConnected = true, Status = "在线" },
                new() { Id = "A3", Name = "A3", PlcModel = "AM600", IpAddress = "192.168.10.13", Port = 502, Protocol = "ModbusTCP", IsConnected = false, Status = "故障" },
                new() { Id = "A4", Name = "A4", PlcModel = "FX5U", IpAddress = "192.168.10.14", Port = 502, Protocol = "ModbusTCP", IsConnected = false, Status = "离线" },
                new() { Id = "A5", Name = "A5", PlcModel = "S7-1500", IpAddress = "192.168.10.15", Port = 102, Protocol = "S7 Protocol (TCP)", IsConnected = true, Status = "在线" },
                new() { Id = "A6", Name = "A6", PlcModel = "AM600", IpAddress = "192.168.10.16", Port = 502, Protocol = "ModbusTCP", IsConnected = true, Status = "在线" }
            };

            var repository = new InMemoryMotorTestRepository();
            SeedRepositoryAsync(repository).GetAwaiter().GetResult();
            return new BackendRuntime(configs, repository, new MockPlcClientFactory());
        }

        private static async Task SeedRepositoryAsync(IMotorTestRepository repository)
        {
            var now = DateTime.Now;
            // Define data for the last 8 hours: (hoursAgo, okCount, ngCount)
            var hourData = new[]
            {
                (7, 8, 1),
                (6, 11, 1),
                (5, 10, 0),
                (4, 14, 1),
                (3, 7, 0),
                (2, 9, 1),
                (1, 11, 1),
                (0, 8, 2)
            };

            int barcodeIndex = 0;
            foreach (var item in hourData)
            {
                int hoursAgo = item.Item1;
                int okCount = item.Item2;
                int ngCount = item.Item3;
                
                DateTime hourTime = now.AddHours(-hoursAgo);
                
                // Seed OK records
                for (int i = 0; i < okCount; i++)
                {
                    string barcode = $"DES-SR-150GEN{1992900399000 + barcodeIndex++}";
                    DateTime collectedAt = new DateTime(hourTime.Year, hourTime.Month, hourTime.Day, hourTime.Hour, (i * 5) % 60, 0);
                    
                    await repository.UpsertStageResultAsync(new StageTestData
                    {
                        Barcode = barcode,
                        StationId = "A1",
                        Stage = TestStage.NoLoad,
                        CollectedAt = collectedAt,
                        Result = "OK",
                        NoLoadCurrent = 1.82 + (i % 5) * 0.01,
                        NoLoadSpeed = 2050 + i,
                        ShaftLength = 32.4,
                        KnurlDiameter = 4.42
                    });
                    
                    await repository.UpsertStageResultAsync(new StageTestData
                    {
                        Barcode = barcode,
                        StationId = "A3",
                        Stage = TestStage.Noise,
                        CollectedAt = collectedAt.AddMinutes(1),
                        Result = "OK",
                        FwdNoise = 62.5,
                        RevNoise = 55.3,
                        NoiseDiff = 7.2
                    });

                    await repository.UpsertStageResultAsync(new StageTestData
                    {
                        Barcode = barcode,
                        StationId = "A5",
                        Stage = TestStage.Load,
                        CollectedAt = collectedAt.AddMinutes(2),
                        Result = "OK",
                        LoadCurrent = 2.35,
                        LoadSpeed = 1210 + i
                    });
                }
                
                // Seed NG records
                for (int i = 0; i < ngCount; i++)
                {
                    string barcode = $"DES-SR-150GEN{1992900399000 + barcodeIndex++}";
                    DateTime collectedAt = new DateTime(hourTime.Year, hourTime.Month, hourTime.Day, hourTime.Hour, (i * 15 + 2) % 60, 0);
                    
                    await repository.UpsertStageResultAsync(new StageTestData
                    {
                        Barcode = barcode,
                        StationId = "A1",
                        Stage = TestStage.NoLoad,
                        CollectedAt = collectedAt,
                        Result = "OK",
                        NoLoadCurrent = 1.82,
                        NoLoadSpeed = 2050,
                        ShaftLength = 32.4,
                        KnurlDiameter = 4.42
                    });
                    
                    // Noise failed (leads to NG final result)
                    await repository.UpsertStageResultAsync(new StageTestData
                    {
                        Barcode = barcode,
                        StationId = "A3",
                        Stage = TestStage.Noise,
                        CollectedAt = collectedAt.AddMinutes(1),
                        Result = "NG",
                        FwdNoise = 78.5,
                        RevNoise = 55.3,
                        NoiseDiff = 23.2
                    });

                    await repository.UpsertStageResultAsync(new StageTestData
                    {
                        Barcode = barcode,
                        StationId = "A5",
                        Stage = TestStage.Load,
                        CollectedAt = collectedAt.AddMinutes(2),
                        Result = "OK",
                        LoadCurrent = 2.35,
                        LoadSpeed = 1210
                    });
                }
            }
        }
    }
}
