using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services;

public interface IMotorTestRepository
{
	Task UpsertStageResultAsync(StageTestData data, CancellationToken cancellationToken = default(CancellationToken));

	Task<IReadOnlyList<MotorTestResult>> QueryAsync(MotorTestQuery query, CancellationToken cancellationToken = default(CancellationToken));

	Task<IReadOnlyList<MotorTestResult>> GetRecentAsync(int count, CancellationToken cancellationToken = default(CancellationToken));

	Task<ProductionSummary> GetSummaryAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default(CancellationToken));
}
