using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    public sealed class PlcPollingService : IDisposable
    {
        private readonly IMotorTestRepository _repository;
        private readonly IPlcClientFactory _clientFactory;
        private readonly TimeSpan _pollInterval;
        private readonly List<IPlcClient> _clients = new();
        private readonly List<Task> _pollingTasks = new();
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly ConcurrentDictionary<string, int> _consecutiveFailures = new();
        private readonly EventChannelService? _eventChannel;
        private static readonly TimeSpan MinRetryDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(60);

        public PlcPollingService(
            IEnumerable<StationConfig> stationConfigs,
            IMotorTestRepository repository,
            IPlcClientFactory clientFactory,
            TimeSpan? pollInterval = null,
            EventChannelService? eventChannel = null)
        {
            _repository = repository;
            _clientFactory = clientFactory;
            _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
            _eventChannel = eventChannel;
            _clients.AddRange(stationConfigs.Select(_clientFactory.Create));
        }

        public event EventHandler<StationSnapshot>? SnapshotReceived;
        public event EventHandler<string>? LogReceived;

        public void Start()
        {
            if (_cancellationTokenSource != null)
            {
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            foreach (var client in _clients)
            {
                _pollingTasks.Add(PollStationAsync(client, _cancellationTokenSource.Token));
            }
        }

        public async Task StopAsync()
        {
            if (_cancellationTokenSource == null)
            {
                return;
            }

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

        public async Task<bool> TestConnectionAsync(StationConfig config, CancellationToken cancellationToken = default)
        {
            using var client = _clientFactory.Create(config);
            return await client.ConnectAsync(cancellationToken);
        }

        public void Dispose()
        {
            // 同步等待轮询任务完成（避免竞态）
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                try
                {
                    Task.WaitAll(_pollingTasks.ToArray());
                }
                catch (AggregateException)
                {
                    // 忽略取消异常
                }
                _pollingTasks.Clear();
            }

            foreach (var client in _clients)
            {
                client.Dispose();
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        private async Task PollStationAsync(IPlcClient client, CancellationToken cancellationToken)
        {
            string stationId = client.Config.Id;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    bool connected = await client.ConnectAsync(cancellationToken);
                    if (!connected)
                    {
                        int failures = IncrementFailure(stationId);
                        Publish(new StationSnapshot
                        {
                            StationId = stationId,
                            IsOnline = false,
                            Status = 2,
                            CompletionSignal = false
                        });

                        await Task.Delay(GetBackoffDelay(failures), cancellationToken);
                        continue;
                    }

                    var snapshot = await client.ReadSnapshotAsync(cancellationToken);
                    if (snapshot.CompletionSignal && snapshot.CompletedData != null)
                    {
                        if (_eventChannel != null)
                        {
                            await _eventChannel.WriteWriter.WriteAsync(snapshot.CompletedData, cancellationToken);
                        }
                        else
                        {
                            await _repository.UpsertStageResultAsync(snapshot.CompletedData, cancellationToken);
                        }
                        await client.ResetCompletionSignalAsync(cancellationToken);
                        LogReceived?.Invoke(this, $"{stationId} queued {snapshot.CompletedData.Barcode} {snapshot.CompletedData.Stage} {snapshot.CompletedData.Result}");
                    }

                    // 连接 + 读取成功，重置失败计数
                    ResetFailure(stationId);

                    Publish(snapshot);
                    await Task.Delay(_pollInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    int failures = IncrementFailure(stationId);
                    Publish(new StationSnapshot
                    {
                        StationId = stationId,
                        IsOnline = false,
                        Status = 2,
                        CompletionSignal = false
                    });
                    LogReceived?.Invoke(this, $"{stationId} polling error: {ex.Message}");
                    await Task.Delay(GetBackoffDelay(failures), cancellationToken);
                }
            }
        }

        private int IncrementFailure(string stationId)
        {
            return _consecutiveFailures.AddOrUpdate(stationId, 1, (key, old) => old + 1);
        }

        private void ResetFailure(string stationId)
        {
            _consecutiveFailures.TryRemove(stationId, out _);
        }

        /// <summary>
        /// 指数退避: 1s → 2s → 4s → 8s → 16s → 32s → 60s (max)
        /// </summary>
        private static TimeSpan GetBackoffDelay(int consecutiveFailures)
        {
            if (consecutiveFailures <= 0) return MinRetryDelay;
            // 2^(n-1) 秒, 上限 60 秒
            double seconds = Math.Min(Math.Pow(2, consecutiveFailures - 1), MaxRetryDelay.TotalSeconds);
            return TimeSpan.FromSeconds(Math.Max(seconds, MinRetryDelay.TotalSeconds));
        }

        private void Publish(StationSnapshot snapshot)
        {
            SnapshotReceived?.Invoke(this, snapshot);
            _eventChannel?.SnapshotWriter.TryWrite(snapshot);
        }
    }
}
