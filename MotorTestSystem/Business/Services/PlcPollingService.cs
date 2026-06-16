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

        // ---- 序列号检测（用于断网重连后的数据完整性） ----
        private readonly ConcurrentDictionary<string, int> _lastSequenceNumbers = new();
        private readonly ConcurrentDictionary<string, long> _dataLossCounts = new();

        public event EventHandler<StationSnapshot>? SnapshotReceived;
        public event EventHandler<string>? LogReceived;

        /// <summary>
        /// 获取指定工位的累计数据丢失次数。
        /// </summary>
        public long GetDataLossCount(string stationId) =>
            _dataLossCounts.TryGetValue(stationId, out var count) ? count : 0;

        /// <summary>
        /// 是否有任何工位检测到数据丢失。
        /// </summary>
        public bool HasAnyDataLoss => _dataLossCounts.Values.Any(v => v > 0);

        /// <summary>所有工位的数据丢失次数合计</summary>
        public long TotalDataLossCount => _dataLossCounts.Values.Sum();

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

        public void Start()
        {
            if (_cancellationTokenSource != null)
                return;

            _cancellationTokenSource = new CancellationTokenSource();
            foreach (var client in _clients)
            {
                _pollingTasks.Add(PollStationAsync(client, _cancellationTokenSource.Token));
            }
        }

        public async Task StopAsync()
        {
            if (_cancellationTokenSource == null)
                return;

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
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                try
                {
                    Task.WaitAll(_pollingTasks.ToArray());
                }
                catch (AggregateException)
                {
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

                    // ---- 序列号跳跃检测（重连后检测数据是否连续） ----
                    if (snapshot.SequenceNumber.HasValue)
                    {
                        if (_lastSequenceNumbers.TryGetValue(stationId, out var lastSeq))
                        {
                            int gap = snapshot.SequenceNumber.Value - lastSeq;
                            if (gap > 1)
                            {
                                // 检测到序列号跳跃
                                _dataLossCounts.AddOrUpdate(stationId, gap - 1, (_, old) => old + gap - 1);
                                LogReceived?.Invoke(this,
                                    $"{stationId} sequence number gap detected: {lastSeq} → {snapshot.SequenceNumber} (gap={gap - 1}, total lost={_dataLossCounts[stationId]})");
                            }
                        }
                        _lastSequenceNumbers[stationId] = snapshot.SequenceNumber.Value;
                    }

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

        private static TimeSpan GetBackoffDelay(int consecutiveFailures)
        {
            if (consecutiveFailures <= 0) return MinRetryDelay;
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
