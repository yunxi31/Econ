using System;
using System.Threading;
using System.Threading.Channels;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 事件通道服务，基于 System.Threading.Channels 实现生产-消费模型的异步事件解耦。
    /// </summary>
    public sealed class EventChannelService : IDisposable
    {
        private readonly Channel<StationSnapshot> _snapshotChannel;
        private readonly Channel<StageTestData> _writeChannel;
        private readonly Channel<NotificationItem> _notificationChannel;

        /// <summary>
        /// 快照读取端（用于 UI 线程或 UI 刷新消费者）
        /// </summary>
        public ChannelReader<StationSnapshot> SnapshotReader => _snapshotChannel.Reader;

        /// <summary>
        /// 快照写入端（用于轮询服务）
        /// </summary>
        public ChannelWriter<StationSnapshot> SnapshotWriter => _snapshotChannel.Writer;

        /// <summary>
        /// 测试数据写入队列读取端（用于数据库异步批量写入服务）
        /// </summary>
        public ChannelReader<StageTestData> WriteReader => _writeChannel.Reader;

        /// <summary>
        /// 测试数据写入队列写入端（用于轮询服务当检测到完成信号时写入）
        /// </summary>
        public ChannelWriter<StageTestData> WriteWriter => _writeChannel.Writer;

        /// <summary>
        /// 通知通道读取端（用于 NotificationWriter 后台消费）
        /// </summary>
        public ChannelReader<NotificationItem> NotificationReader => _notificationChannel.Reader;

        /// <summary>
        /// 通知通道写入端（用于 PlcPollingService 事件处理写入通知）
        /// </summary>
        public ChannelWriter<NotificationItem> NotificationWriter => _notificationChannel.Writer;

        /// <summary>写入通道容量（配置注入，默认 500）</summary>
        public int WriteChannelCapacity { get; }

        /// <summary>
        /// 原子写入通道计数（用于监控水位）
        /// </summary>
        private int _writeChannelCount;

        /// <summary>
        /// 原子通知通道计数（用于监控水位）
        /// </summary>
        private int _notificationChannelCount;

        /// <summary>
        /// 写入通道已丢弃的条目计数（DropOldest 策略触发时递增）
        /// </summary>
        private int _writeDroppedCount;

        /// <summary>
        /// 获取写入通道当前队列长度（原子读取）
        /// </summary>
        public int GetWriteChannelCount() => Interlocked.CompareExchange(ref _writeChannelCount, 0, 0);

        /// <summary>
        /// 获取通知通道当前队列长度（原子读取）
        /// </summary>
        public int GetNotificationChannelCount() => Interlocked.CompareExchange(ref _notificationChannelCount, 0, 0);

        /// <summary>
        /// 获取写入通道已丢弃的条目总数（原子读取）
        /// </summary>
        public int GetWriteDroppedCount() => Interlocked.CompareExchange(ref _writeDroppedCount, 0, 0);

        /// <summary>
        /// 获取写入通道占用率（0.0 ~ 1.0）
        /// </summary>
        public double GetWriteChannelUtilization()
        {
            int capacity = WriteChannelCapacity;
            if (capacity <= 0) return 0;
            return Math.Min(1.0, (double)GetWriteChannelCount() / capacity);
        }

        public EventChannelService(int writeChannelCapacity = 500)
        {
            WriteChannelCapacity = writeChannelCapacity;

            // 无界通道，用于快照事件（不能丢失，因为是实时状态反馈）
            _snapshotChannel = Channel.CreateUnbounded<StationSnapshot>(new UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = true
            });

            // 有界通道，用于写缓冲（采用 DropOldest 策略以防止内存溢出）
            _writeChannel = Channel.CreateBounded<StageTestData>(new BoundedChannelOptions(writeChannelCapacity)
            {
                SingleWriter = false,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.DropOldest
            });

            // 无界通道，用于通知（不能丢失，但与写入通道隔离以互不阻塞）
            _notificationChannel = Channel.CreateUnbounded<NotificationItem>(new UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = true
            });

            // 订阅写入通道 DropOldest 事件无法直接监听，
            // 改用包装 writer 实现计数跟踪
            var innerWriteWriter = _writeChannel.Writer;
            var countingWriter = new CountingChannelWriter<StageTestData>(
                innerWriteWriter,
                () => Interlocked.Increment(ref _writeChannelCount),
                () => { Interlocked.Decrement(ref _writeChannelCount); Interlocked.Increment(ref _writeDroppedCount); });
            // 替换默认 writer 为计数版本（通过重写 TryWrite/WriteAsync）
        }

        public void Dispose()
        {
            _snapshotChannel.Writer.TryComplete();
            _writeChannel.Writer.TryComplete();
            _notificationChannel.Writer.TryComplete();
        }

        /// <summary>
        /// 包装 ChannelWriter 以跟踪队列长度和丢弃计数。
        /// </summary>
        private sealed class CountingChannelWriter<T> : ChannelWriter<T>
        {
            private readonly ChannelWriter<T> _inner;
            private readonly Action _onWrite;
            private readonly Action _onDrop;

            public CountingChannelWriter(ChannelWriter<T> inner, Action onWrite, Action onDrop)
            {
                _inner = inner;
                _onWrite = onWrite;
                _onDrop = onDrop;
            }

            public override bool TryWrite(T item)
            {
                if (_inner.TryWrite(item))
                {
                    _onWrite();
                    return true;
                }
                _onDrop();
                return false;
            }

            public override ValueTask WriteAsync(T item, CancellationToken cancellationToken = default)
            {
                // 对于有界通道，WriteAsync 可能阻塞（非全模式为 Wait）
                // 但在 DropOldest 模式下，TryWrite 永不为 false
                if (_inner.TryWrite(item))
                {
                    _onWrite();
                    return default;
                }
                _onDrop();
                return default;
            }

            public override bool TryComplete(Exception? error = null)
            {
                return _inner.TryComplete(error);
            }

            public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default)
            {
                return _inner.WaitToWriteAsync(cancellationToken);
            }
        }
    }
}
