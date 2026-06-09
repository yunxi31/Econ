using System;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services;

public interface IPlcClient : IDisposable
{
	StationConfig Config { get; }

	Task<bool> ConnectAsync(CancellationToken cancellationToken = default(CancellationToken));

	Task<StationSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default(CancellationToken));

	Task ResetCompletionSignalAsync(CancellationToken cancellationToken = default(CancellationToken));
}
