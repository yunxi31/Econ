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
    /// </summary>
    public sealed class BatchWriteService : IDisposable
    {
        private readonly IMotorTestRepository _repository;
        private readonly ChannelReader<StageTestData> _channelReader;
        private readonly CancellationTokenSource _cts;
        private readonly Task _runTask;

        public BatchWriteService(IMotorTestRepository repository, ChannelReader<StageTestData> channelReader)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _channelReader = channelReader ?? throw new ArgumentNullException(nameof(channelReader));
            _cts = new CancellationTokenSource();
            _runTask = Task.Run(() => ProcessQueueAsync(_cts.Token));
        }

        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            var batch = new List<StageTestData>();

            try
            {
                while (await _channelReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    // 1. Wait for at least one item, then read it
                    if (_channelReader.TryRead(out var firstItem))
                    {
                        batch.Add(firstItem);

                        // 2. Try to gather more items within 100ms or until limit of 50 items
                        var timeoutTask = Task.Delay(100, cancellationToken);
                        while (batch.Count < 50)
                        {
                            var waitTask = _channelReader.WaitToReadAsync(cancellationToken).AsTask();
                            var completedTask = await Task.WhenAny(waitTask, timeoutTask).ConfigureAwait(false);

                            if (completedTask == timeoutTask)
                            {
                                // Timeout reached, stop gathering
                                break;
                            }

                            // If waitTask completed, check if we can read
                            if (waitTask.Result)
                            {
                                while (batch.Count < 50 && _channelReader.TryRead(out var item))
                                {
                                    batch.Add(item);
                                }
                            }
                            else
                            {
                                // Channel completed/closed
                                break;
                            }
                        }

                        // 3. Perform bulk upsert
                        if (batch.Count > 0)
                        {
                            try
                            {
                                await _repository.BulkUpsertAsync(batch, cancellationToken).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Trace.WriteLine($"Error during bulk upsert: {ex.Message}");
                            }
                            finally
                            {
                                batch.Clear();
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
                System.Diagnostics.Trace.WriteLine($"BatchWriteService error: {ex.Message}");
            }

            // Flush remaining items upon cancellation/completion
            await FlushRemainingAsync().ConfigureAwait(false);
        }

        private async Task FlushRemainingAsync()
        {
            var remaining = new List<StageTestData>();
            while (_channelReader.TryRead(out var item))
            {
                remaining.Add(item);
            }

            if (remaining.Count > 0)
            {
                try
                {
                    // Use a new CancellationToken since the main one is already canceled
                    await _repository.BulkUpsertAsync(remaining, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"Error during final flush: {ex.Message}");
                }
            }
        }

        public async Task StopAsync()
        {
            _cts.Cancel();
            try
            {
                await _runTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error stopping BatchWriteService: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            try
            {
                // Wait for the task to complete synchronously or with a timeout to prevent deadlocks
                _runTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Error disposing BatchWriteService: {ex.Message}");
            }
            finally
            {
                _cts.Dispose();
            }
        }
    }
}
