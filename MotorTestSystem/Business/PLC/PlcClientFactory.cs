using System;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    public sealed class PlcClientFactory : IPlcClientFactory
    {
        private readonly bool _useSimulation;

        public PlcClientFactory(bool useSimulation = false)
        {
            _useSimulation = useSimulation;
        }

        public IPlcClient Create(StationConfig config)
        {
            if (_useSimulation)
            {
                return new MockPlcClient(config);
            }

            return config.Protocol switch
            {
                "ModbusTCP" => new ModbusTcpClient(config),
                "MelsecMC" or "MC Protocol (TCP)" or "MC Protocol / TCP" => new MelsecMcClient(config),
                "S7 Protocol (TCP)" or "S7 Protocol / TCP" or "S7" => new S7PlcClient(config),
                _ => new MockPlcClient(config) // Other unsupported protocols fall back to mock
            };
        }
    }
}
