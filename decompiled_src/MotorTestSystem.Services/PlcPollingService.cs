using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services;

public sealed class PlcPollingService : IDisposable
{
	private readonly IMotorTestRepository _repository;

	private readonly IPlcClientFactory _clientFactory;

	private readonly TimeSpan _pollInterval;

	private readonly List<IPlcClient> _clients = new List<IPlcClient>();

	private readonly List<Task> _pollingTasks = new List<Task>();

	private CancellationTokenSource? _cancellationTokenSource;

	public event EventHandler<StationSnapshot>? SnapshotReceived;

	public event EventHandler<string>? LogReceived;

	public PlcPollingService(IEnumerable<StationConfig> stationConfigs, IMotorTestRepository repository, IPlcClientFactory clientFactory, TimeSpan? pollInterval = null)
	{
		_repository = repository;
		_clientFactory = clientFactory;
		_pollInterval = pollInterval ?? TimeSpan.FromSeconds(1.0);
		_clients.AddRange(stationConfigs.Select(_clientFactory.Create));
	}

	public void Start()
	{
		if (_cancellationTokenSource != null)
		{
			return;
		}
		_cancellationTokenSource = new CancellationTokenSource();
		foreach (IPlcClient client in _clients)
		{
			_pollingTasks.Add(Task.Run(() => PollStationAsync(client, _cancellationTokenSource.Token)));
		}
	}

	public async Task StopAsync()
	{
		if (_cancellationTokenSource != null)
		{
			_cancellationTokenSource.Cancel();
			try
			{
				await Task.WhenAll(_pollingTasks);
			}
			catch (OperationCanceledException)
			{
			}
			_pollingTasks.Clear();
			_cancellationTokenSource.Dispose();
			_cancellationTokenSource = null;
		}
	}

	public async Task<bool> TestConnectionAsync(StationConfig config, CancellationToken cancellationToken = default(CancellationToken))
	{
		using IPlcClient client = _clientFactory.Create(config);
		return await client.ConnectAsync(cancellationToken);
	}

	public void Dispose()
	{
		_cancellationTokenSource?.Cancel();
		foreach (IPlcClient client in _clients)
		{
			client.Dispose();
		}
		_cancellationTokenSource?.Dispose();
	}

	private async Task PollStationAsync(IPlcClient client, CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				if (!(await client.ConnectAsync(cancellationToken)))
				{
					Publish(new StationSnapshot
					{
						StationId = client.Config.Id,
						IsOnline = false,
						Status = 2,
						CompletionSignal = false
					});
					await Task.Delay(_pollInterval, cancellationToken);
					continue;
				}
				StationSnapshot snapshot = await client.ReadSnapshotAsync(cancellationToken);
				if (snapshot.CompletionSignal && snapshot.CompletedData != null)
				{
					await _repository.UpsertStageResultAsync(snapshot.CompletedData, cancellationToken);
					await client.ResetCompletionSignalAsync(cancellationToken);
					this.LogReceived?.Invoke(this, $"{snapshot.StationId} saved {snapshot.CompletedData.Barcode} {snapshot.CompletedData.Stage} {snapshot.CompletedData.Result}");
				}
				Publish(snapshot);
				await Task.Delay(_pollInterval, cancellationToken);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex2)
			{
				Publish(new StationSnapshot
				{
					StationId = client.Config.Id,
					IsOnline = false,
					Status = 2,
					CompletionSignal = false
				});
				this.LogReceived?.Invoke(this, client.Config.Id + " polling error: " + ex2.Message);
				await Task.Delay(_pollInterval, cancellationToken);
			}
		}
	}

	private void Publish(StationSnapshot snapshot)
	{
		this.SnapshotReceived?.Invoke(this, snapshot);
	}
}
