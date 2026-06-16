using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 后台批量写入服务，从 ChannelReader 读取测试数据并批量 Upsert 到数据库。
    /// 包含指数退避重试、死信队列持久化、通道水位监控和后台监控 Task。
    /// </summary>
    public sealed class BatchWriteService : IDisposable
    {
        private readonly IMotorTestRepository _repository;
        private readonly ChannelReader<StageTestData> _channelReader;
        private readonly CancellationTokenSource _cts;
        private readonly Task _runTask;
        private readonly IDeadLetterQueue? _deadLetterQueue;
        private readonly EventChannelService? _eventChannel;
        private readonly Task? _monitorTask;

        /// <summary>配置：单次写入超时时间（秒），默认 10</summary>
        public int FlushTimeoutSeconds { get; set; } = 10;

        /// <summary>配置：写入重试次数，默认 3</summary>
        public int MaxRetryCount { get; set; } = 3;

        /// <summary>监控：总丢弃计数（因通道满导致）</summary>
        private int _totalDroppedCount;

        /// <summary>获取丢弃计数</summary>
        public int GetTotalDroppedCount() => Interlocked.CompareExchange(ref _totalDroppedCount, 0, 0);

        /// <summary>最后一次水位 ≥80% 的告警时间（用于冷却）</summary>
        private DateTime _lastHighUtilizationWarning = DateTime.MinValue;

        public BatchWriteService(
            IMotorTestRepository repository,
            ChannelReader<StageTestData> channelReader,
            IDeadLetterQueue? deadLetterQueue = null,
            EventChannelService? eventChannel = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _channelReader = channelReader ?? throw new ArgumentNullException(nameof(channelReader));
            _deadLetterQueue = deadLetterQueue;
            _eventChannel = eventChannel;
            _cts = new CancellationTokenSource();
            _runTask = Task.Run(() => ProcessQueueAsync(_cts.Token));

            // 启动后台水位监控 Task（7.3）
            if (_eventChannel != null)
            {
                _monitorTask = Task.Run(() => MonitorUtilizationAsync(_cts.Token));
            }
        }

        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            var batch = new List<StageTestData>();

            try
            {
                while (await _channelReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (_channelReader.TryRead(out var firstItem))
                    {
                        batch.Add(firstItem);

                        var timeoutTask = Task.Delay(100, cancellationToken);
                        while (batch.Count < 50)
                        {
                            var waitTask = _channelReader.WaitToReadAsync(cancellationToken).AsTask();
                            var completedTask = await Task.WhenAny(waitTask, timeoutTask).ConfigureAwait(false);

                            if (completedTask == timeoutTask)
                                break;

                            if (waitTask.Result)
                            {
                                while (batch.Count < 50 && _channelReader.TryRead(out var item))
                                    batch.Add(item);
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (batch.Count > 0)
                        {
                            await TryBulkUpsertWithRetryAsync(batch, cancellationToken).ConfigureAwait(false);
                            batch.Clear();
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"BatchWriteService error: {ex.Message}");
            }

            await FlushRemainingAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// 带指数退避重试的批量写入（3.3）。
        /// 重试 3 次（1s, 2s, 4s 退避），全部失败后进入死信队列。
        /// </summary>
        private async Task TryBulkUpsertWithRetryAsync(List<StageTestData> batch, CancellationToken cancellationToken)
        {
            for (int attempt = 0; attempt <= MaxRetryCount; attempt++)
            {
                try
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(FlushTimeoutSeconds));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                    await _repository.BulkUpsertAsync(batch, linkedCts.Token).ConfigureAwait(false);
                    return; // 成功
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw; // 正常关闭
                }
                catch (Exception ex)
                {
                    if (attempt == MaxRetryCount)
                    {
                        System.Diagnostics.Trace.WriteLine($"Bulk upsert failed after {MaxRetryCount + 1} attempts: {ex.Message}");

                        if (_deadLetterQueue != null)
                        {
                            try
                            {
                                await _deadLetterQueue.EnqueueAsync(
                                    batch.AsReadOnly(), ex.Message, ex.GetType().FullName, cancellationToken
                                ).ConfigureAwait(false);
                            }
                            catch { /* best-effort */ }
                        }
                        return;
                    }

                    int delayMs = (int)Math.Pow(2, attempt) * 1000;
                    System.Diagnostics.Trace.WriteLine($"Bulk upsert attempt {attempt + 1} failed, retrying in {delayMs}ms: {ex.Message}");
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 后台水位监控 Task（7.3）：每秒查询通道占用率，≥80% 时每 5 秒记录警告日志。
        /// </summary>
        private async Task MonitorUtilizationAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);

                    if (_eventChannel == null) continue;

                    double utilization = _eventChannel.GetWriteChannelUtilization();
                    int droppedCount = GetTotalDroppedCount();

                    if (utilization >= 0.95)
                    {
                        System.Diagnostics.Trace.WriteLine(
                            $"[BATCH-WRITE] CRITICAL: Write channel utilization={utilization:P1}, dropped={droppedCount}");
                    }
                    else if (utilization >= 0.80)
                    {
                        // 每 5 秒记录一次，避免刷屏
                        if (DateTime.UtcNow - _lastHighUtilizationWarning > TimeSpan.FromSeconds(5))
                        {
                            _lastHighUtilizationWarning = DateTime.UtcNow;
                            System.Diagnostics.Trace.WriteLine(
                                $"[BATCH-WRITE] WARNING: Write channel utilization={utilization:P1}, dropped={droppedCount}");
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"BatchWriteService monitor error: {ex.Message}");
            }
        }

        private async Task FlushRemainingAsync()
        {
            var remaining = new List<StageTestData>();
            while (_channelReader.TryRead(out var item))
                remaining.Add(item);

            if (remaining.Count > 0)
            {
                try
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(FlushTimeoutSeconds));
                    await _repository.BulkUpsertAsync(remaining, timeoutCts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"Error during final flush: {ex.Message}");
                    if (_deadLetterQueue != null)
                    {
                        try { await _deadLetterQueue.EnqueueAsync(remaining.AsReadOnly(), $"Flush failure: {ex.Message}", ex.GetType().FullName).ConfigureAwait(false); }
                        catch { /* best-effort */ }
                    }
                }
            }
        }

        /// <summary>
        /// 获取当前队列深度。
        /// </summary>
        public int GetQueueDepth() => _eventChannel?.GetWriteChannelCount() ?? 0;

        public async Task StopAsync()
        {
            _cts.Cancel();
            try { await _runTask.ConfigureAwait(false); }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"Error stopping BatchWriteService: {ex.Message}"); }
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _runTask.Wait(TimeSpan.FromSeconds(FlushTimeoutSeconds + 2)); }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"Error disposing BatchWriteService: {ex.Message}"); }
            finally { _cts.Dispose(); }
        }
    }
}
