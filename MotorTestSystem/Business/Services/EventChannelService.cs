using System;
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

        /// <summary>
        /// 获取快照读取端（用于 UI 线程或 UI 刷新消费者）
        /// </summary>
        public ChannelReader<StationSnapshot> SnapshotReader => _snapshotChannel.Reader;

        /// <summary>
        /// 获取快照写入端（用于轮询服务）
        /// </summary>
        public ChannelWriter<StationSnapshot> SnapshotWriter => _snapshotChannel.Writer;

        /// <summary>
        /// 获取测试数据写入队列读取端（用于数据库异步批量写入服务）
        /// </summary>
        public ChannelReader<StageTestData> WriteReader => _writeChannel.Reader;

        /// <summary>
        /// 获取测试数据写入队列写入端（用于轮询服务当检测到完成信号时写入）
        /// </summary>
        public ChannelWriter<StageTestData> WriteWriter => _writeChannel.Writer;

        public EventChannelService()
        {
            // _snapshotChannel: Unbounded，用于快照事件（不能丢失，因为是实时状态反馈）
            _snapshotChannel = Channel.CreateUnbounded<StationSnapshot>(new UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = true
            });

            // _writeChannel: Bounded(500)，用于写缓冲（采用 DropOldest 策略以防止内存溢出，保证最新数据优先）
            _writeChannel = Channel.CreateBounded<StageTestData>(new BoundedChannelOptions(500)
            {
                SingleWriter = false,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.DropOldest
            });
        }

        public void Dispose()
        {
            _snapshotChannel.Writer.TryComplete();
            _writeChannel.Writer.TryComplete();
        }
    }
}
