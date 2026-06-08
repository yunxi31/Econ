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
                new() { Id = "A1", Name = "空载测试机台 A1", PlcModel = "三菱 FX5U", IpAddress = "192.168.1.10", Port = 6000, Protocol = "MC Protocol (TCP)" },
                new() { Id = "A2", Name = "空载测试机台 A2", PlcModel = "三菱 Q系列", IpAddress = "192.168.1.11", Port = 6000, Protocol = "MC Protocol (TCP)" },
                new() { Id = "A3", Name = "噪音测试机台 A3", PlcModel = "西门子 S7-1200", IpAddress = "192.168.1.12", Port = 102, Protocol = "S7 Protocol (TCP)", StationId = 0 },
                new() { Id = "A4", Name = "噪音测试机台 A4", PlcModel = "西门子 S7-1500", IpAddress = "192.168.1.13", Port = 102, Protocol = "S7 Protocol (TCP)", StationId = 0 },
                new() { Id = "A5", Name = "负载测试机台 A5", PlcModel = "汇川 H5U", IpAddress = "192.168.1.14", Port = 502, Protocol = "ModbusTCP" },
                new() { Id = "A6", Name = "负载测试机台 A6", PlcModel = "汇川 Easy521", IpAddress = "192.168.1.15", Port = 502, Protocol = "ModbusTCP" }
            };

            var repository = new InMemoryMotorTestRepository();
            SeedRepositoryAsync(repository).GetAwaiter().GetResult();
            return new BackendRuntime(configs, repository, new MockPlcClientFactory());
        }

        private static async Task SeedRepositoryAsync(IMotorTestRepository repository)
        {
            var now = DateTime.Now.AddMinutes(-30);
            for (int i = 0; i < 12; i++)
            {
                string barcode = $"DES-SR-150GEN{1992900399000 + i}";
                string noiseResult = i % 5 == 0 ? "NG" : "OK";
                string loadResult = i % 7 == 0 ? "NG" : "OK";

                await repository.UpsertStageResultAsync(new StageTestData
                {
                    Barcode = barcode,
                    StationId = "A1",
                    Stage = TestStage.NoLoad,
                    CollectedAt = now.AddMinutes(i),
                    Result = "OK",
                    NoLoadCurrent = 1.82 + i * 0.01,
                    NoLoadSpeed = 2050 + i,
                    ShaftLength = 32.4,
                    KnurlDiameter = 4.42
                });

                await repository.UpsertStageResultAsync(new StageTestData
                {
                    Barcode = barcode,
                    StationId = "A3",
                    Stage = TestStage.Noise,
                    CollectedAt = now.AddMinutes(i + 1),
                    Result = noiseResult,
                    FwdNoise = noiseResult == "OK" ? 62.5 : 75.5,
                    RevNoise = 55.3,
                    NoiseDiff = noiseResult == "OK" ? 7.2 : 20.2
                });

                if (i % 4 != 0)
                {
                    await repository.UpsertStageResultAsync(new StageTestData
                    {
                        Barcode = barcode,
                        StationId = "A5",
                        Stage = TestStage.Load,
                        CollectedAt = now.AddMinutes(i + 2),
                        Result = loadResult,
                        LoadCurrent = loadResult == "OK" ? 2.35 : 3.42,
                        LoadSpeed = 1210 + i
                    });
                }
            }
        }
    }
}
