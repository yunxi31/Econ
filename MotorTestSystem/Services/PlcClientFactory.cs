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
                _ => new MockPlcClient(config) // S7 or other unsupported protocols fall back to mock
            };
        }
    }
}
