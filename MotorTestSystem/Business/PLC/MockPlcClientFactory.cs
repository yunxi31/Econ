using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    public sealed class MockPlcClientFactory : IPlcClientFactory
    {
        public IPlcClient Create(StationConfig config)
        {
            return new MockPlcClient(config);
        }
    }
}
