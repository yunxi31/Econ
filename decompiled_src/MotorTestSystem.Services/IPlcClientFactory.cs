using MotorTestSystem.Models;

namespace MotorTestSystem.Services;

public interface IPlcClientFactory
{
	IPlcClient Create(StationConfig config);
}
