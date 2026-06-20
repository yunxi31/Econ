using System;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Infrastructure.Logging;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 云端同步服务 — 将本地测试记录同步到 MES/云端。（P3 预留 — 基础骨架）
    /// 当前按设计文档预留接口，实际 HTTP/MQTT 推送逻辑待后续实现。
    /// </summary>
    public sealed class CloudSyncService : ICloudSyncService, IDisposable
    {
        private static readonly IAppLogger _log = AppLogger.ForContext<CloudSyncService>();
        private readonly SqlSugarDbContext _dbContext;
        private CancellationTokenSource? _cts;
        private Task? _syncTask;

        public DateTime? LastSyncTime { get; private set; }

        /// <summary>需通过配置项控制，默认不启用</summary>
        public bool IsEnabled { get; set; }

        /// <summary>MES API 端点</summary>
        public string? Endpoint { get; set; }

        public CloudSyncService(SqlSugarDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public int GetPendingSyncCount()
        {
            try
            {
                return _dbContext.Db.Queryable<Models.SyncQueueEntity>()
                    .Where(e => e.SyncStatus == 0) // SyncStatus.Pending
                    .Count();
            }
            catch
            {
                return 0;
            }
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (!IsEnabled) return Task.CompletedTask;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _syncTask = Task.Run(() => SyncLoopAsync(_cts.Token), _cts.Token);
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            _cts?.Cancel();
            if (_syncTask != null)
            {
                try { await _syncTask; }
                catch (OperationCanceledException) { }
            }
        }

        /// <summary>
        /// 同步循环 — 每 5 秒扫描 SyncQueue 表中的 Pending 记录并尝试推送。
        /// （P3 预留：实际 HTTP POST 逻辑待后续实现）
        /// </summary>
        private async Task SyncLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(5000, cancellationToken);

                    if (!IsEnabled) continue;

                    var pendingRecords = await _dbContext.Db.Queryable<Models.SyncQueueEntity>()
                        .Where(e => e.SyncStatus == 0) // Pending
                        .OrderBy(e => e.CreatedAt)
                        .Take(50)
                        .ToListAsync(cancellationToken);

                    foreach (var record in pendingRecords)
                    {
                        try
                        {
                            // TODO: 实现 HTTP POST 到 MES API
                            // var response = await httpClient.PostAsJsonAsync(Endpoint, record);

                            record.SyncStatus = 1; // Synced
                            record.LastAttempt = DateTime.UtcNow;
                            record.RetryCount = 0;
                            await _dbContext.Db.Updateable(record)
                                .UpdateColumns(r => new { r.SyncStatus, r.LastAttempt, r.RetryCount })
                                .ExecuteCommandAsync(cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            record.RetryCount++;
                            record.LastAttempt = DateTime.UtcNow;
                            if (record.RetryCount >= 10)
                            {
                                record.SyncStatus = 2; // Failed permanently
                            }
                            await _dbContext.Db.Updateable(record)
                                .UpdateColumns(r => new { r.RetryCount, r.LastAttempt, r.SyncStatus })
                                .ExecuteCommandAsync(cancellationToken);

                            _log.Warning(ex, "CloudSync: 同步记录失败. RecordId={Id} RetryCount={Retry}",
                                record.Id, record.RetryCount);
                        }
                    }

                    LastSyncTime = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) { }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
