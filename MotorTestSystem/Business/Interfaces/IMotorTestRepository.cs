using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    public interface IMotorTestRepository
    {
        Task UpsertStageResultAsync(StageTestData data, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MotorTestResult>> QueryAsync(MotorTestQuery query, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MotorTestResult>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
        Task<ProductionSummary> GetSummaryAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取指定时间范围内各阶段（空载/噪音/负载）的不良数量统计
        /// </summary>
        Task<DefectSummary> GetDefectSummaryAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<FaultRankItem>> GetFaultRankingAsync(DateTime startTime, DateTime endTime, int topN = 5, CancellationToken cancellationToken = default);

        /// <summary>
        /// 批量更新或插入测试结果
        /// </summary>
        Task BulkUpsertAsync(IEnumerable<StageTestData> results, CancellationToken cancellationToken = default);

        /// <summary>
        /// 使用原生 ADO.NET 批量 INSERT OR REPLACE（性能评估用，9.2）。
        /// </summary>
        Task BulkUpsertWithRawSqlAsync(IEnumerable<StageTestData> results, CancellationToken cancellationToken = default);
    }
}
