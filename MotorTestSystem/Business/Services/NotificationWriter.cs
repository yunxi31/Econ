using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 通知通道后台消费者 — 从 EventChannelService 的通知通道批量读取通知项，
    /// 然后批量写入 INotificationService。
    /// 实现通知写入的异步解耦，不阻塞 PLC 轮询线程。
    /// </summary>
    public sealed class NotificationWriter : IDisposable
    {
        private readonly ChannelReader<NotificationItem> _channelReader;
        private readonly INotificationService _notificationService;
        private readonly CancellationTokenSource _cts;
        private readonly Task _runTask;

        /// <summary>批量大小，默认 50 条</summary>
        public int BatchSize { get; set; } = 50;

        /// <summary>批量等待时间（毫秒），默认 100ms</summary>
        public int BatchTimeoutMs { get; set; } = 100;

        public NotificationWriter(
            ChannelReader<NotificationItem> channelReader,
            INotificationService notificationService)
        {
            _channelReader = channelReader ?? throw new ArgumentNullException(nameof(channelReader));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _cts = new CancellationTokenSource();
            _runTask = Task.Run(() => ConsumeAsync(_cts.Token));
        }

        private async Task ConsumeAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (await _channelReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var batch = new List<NotificationItem>();

                    if (_channelReader.TryRead(out var firstItem))
                    {
                        batch.Add(firstItem);

                        // 在超时时间内收集更多通知项
                        var timeoutTask = Task.Delay(BatchTimeoutMs, cancellationToken);
                        while (batch.Count < BatchSize)
                        {
                            var readTask = _channelReader.WaitToReadAsync(cancellationToken).AsTask();
                            var completed = await Task.WhenAny(readTask, timeoutTask).ConfigureAwait(false);

                            if (completed == timeoutTask)
                                break;

                            if (readTask.Result)
                            {
                                while (batch.Count < BatchSize && _channelReader.TryRead(out var item))
                                {
                                    batch.Add(item);
                                }
                            }
                            else
                            {
                                break;
                            }
                        }

                        // 批量写入通知
                        if (batch.Count > 0)
                        {
                            try
                            {
                                _notificationService.AddRange(batch);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Trace.WriteLine(
                                    $"NotificationWriter batch write error: {ex.Message}");
                                // 单条写入降级
                                foreach (var item in batch)
                                {
                                    try { _notificationService.Add(item); }
                                    catch { /* best-effort */ }
                                }
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"NotificationWriter fatal error: {ex.Message}");
            }
        }

        public async Task StopAsync()
        {
            _cts.Cancel();
            try { await _runTask.ConfigureAwait(false); }
            catch { /* ignore */ }
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _runTask.Wait(TimeSpan.FromSeconds(5)); }
            catch { /* ignore */ }
            _cts.Dispose();
        }
    }
}
