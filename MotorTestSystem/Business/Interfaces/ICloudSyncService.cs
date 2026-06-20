using System.Threading;
using System.Threading.Tasks;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 云端同步服务接口 — 将本地测试记录同步到 MES/云端。（P3 预留）
    /// </summary>
    public interface ICloudSyncService
    {
        /// <summary>启动同步循环</summary>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>停止同步循环</summary>
        Task StopAsync();

        /// <summary>当前同步队列积压数量</summary>
        int GetPendingSyncCount();

        /// <summary>最后一次同步时间</summary>
        System.DateTime? LastSyncTime { get; }
    }
}
