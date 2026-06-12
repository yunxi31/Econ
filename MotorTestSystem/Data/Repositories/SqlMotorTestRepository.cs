using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;
using MotorTestSystem.Models.Entities;

namespace MotorTestSystem.Services
{
    /// <summary>
    /// 基于 SqlSugar + SQLite 的电机测试数据仓储实现
    /// </summary>
    public sealed class SqlMotorTestRepository : IMotorTestRepository
    {
        private readonly SqlSugarDbContext _ctx;

        public SqlMotorTestRepository(SqlSugarDbContext ctx)
        {
            _ctx = ctx;
        }

        public async Task UpsertStageResultAsync(StageTestData data, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(data);
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(data.Barcode))
            {
                throw new ArgumentException("Barcode is required before saving a stage result.", nameof(data));
            }

            string barcode = data.Barcode.Trim();

            var existing = await _ctx.Db.Queryable<MotorTestRecordEntity>()
                .FirstAsync(r => r.Barcode == barcode);

            if (existing == null)
            {
                existing = new MotorTestRecordEntity
                {
                    Barcode = barcode,
                    TestTime = data.CollectedAt,
                    FinalResult = "NG"
                };
                ApplyStage(existing, data);
                existing.FinalResult = CalculateFinalResult(existing);
                await _ctx.Db.Insertable(existing).ExecuteCommandAsync(cancellationToken);
            }
            else
            {
                ApplyStage(existing, data);
                existing.TestTime = data.CollectedAt;
                existing.FinalResult = CalculateFinalResult(existing);
                await _ctx.Db.Updateable(existing).ExecuteCommandAsync(cancellationToken);
            }
        }

        public async Task<IReadOnlyList<MotorTestResult>> QueryAsync(MotorTestQuery query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();

            var queryable = _ctx.Db.Queryable<MotorTestRecordEntity>();

            // 条码筛选
            if (!string.IsNullOrWhiteSpace(query.Barcode))
            {
                string keyword = query.Barcode.Trim();
                queryable = queryable.Where(r => r.Barcode.Contains(keyword));
            }

            // 结果筛选
            if (!IsAllFilter(query.ResultFilter))
            {
                queryable = queryable.Where(r => r.FinalResult == query.ResultFilter);
            }

            // 时间范围
            queryable = queryable.Where(r => r.TestTime >= query.StartTime && r.TestTime <= query.EndTime);

            // 排序
            queryable = queryable.OrderByDescending(r => r.TestTime);

            var entities = await queryable.ToListAsync(cancellationToken);
            return entities.Select(ToModel).ToList();
        }

        public async Task<IReadOnlyList<MotorTestResult>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entities = await _ctx.Db.Queryable<MotorTestRecordEntity>()
                .OrderByDescending(r => r.TestTime)
                .Take(Math.Max(0, count))
                .ToListAsync(cancellationToken);

            return entities.Select(ToModel).ToList();
        }

        public async Task<ProductionSummary> GetSummaryAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scoped = await _ctx.Db.Queryable<MotorTestRecordEntity>()
                .Where(r => r.TestTime >= startTime && r.TestTime <= endTime)
                .ToListAsync(cancellationToken);

            int ok = scoped.Count(r => r.FinalResult == "OK");
            int ng = scoped.Count(r => r.FinalResult == "NG");
            int total = scoped.Count;

            return new ProductionSummary
            {
                TotalChecked = total,
                OkCount = ok,
                NgCount = ng,
                PassRate = total == 0 ? 0 : Math.Round(ok * 100.0 / total, 2)
            };
        }

        public async Task<DefectSummary> GetDefectSummaryAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scoped = await _ctx.Db.Queryable<MotorTestRecordEntity>()
                .Where(r => r.TestTime >= startTime && r.TestTime <= endTime)
                .ToListAsync(cancellationToken);

            return new DefectSummary
            {
                NoLoadNgCount = scoped.Count(r => r.NoLoadResult == "NG"),
                NoiseNgCount = scoped.Count(r => r.NoiseResult == "NG"),
                LoadNgCount = scoped.Count(r => r.LoadResult == "NG")
            };
        }

        public async Task<IReadOnlyList<FaultRankItem>> GetFaultRankingAsync(DateTime startTime, DateTime endTime, int topN = 5, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scoped = await _ctx.Db.Queryable<MotorTestRecordEntity>()
                .Where(r => r.TestTime >= startTime && r.TestTime <= endTime)
                .ToListAsync(cancellationToken);

            var faultCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in scoped)
            {
                if (r.NoLoadResult == "NG")
                {
                    if (r.NoLoadCurrent.HasValue && r.NoLoadCurrent > 2.5)
                        IncrementFault(faultCounts, "电机起动电流超限");
                    else if (r.NoLoadSpeed.HasValue && r.NoLoadSpeed > 2200)
                        IncrementFault(faultCounts, "空载转速异常");
                    else
                        IncrementFault(faultCounts, "空载综合不合格");
                }

                if (r.NoiseResult == "NG")
                {
                    if (r.NoiseDiff.HasValue && r.NoiseDiff > 15)
                        IncrementFault(faultCounts, "空载噪声过大");
                    else if (r.FwdNoise.HasValue && r.FwdNoise > 70)
                        IncrementFault(faultCounts, "正转噪声超限");
                    else
                        IncrementFault(faultCounts, "噪音综合不合格");
                }

                if (r.LoadResult == "NG")
                {
                    if (r.LoadCurrent.HasValue && r.LoadCurrent > 3.0)
                        IncrementFault(faultCounts, "负载电流超限");
                    else if (r.LoadSpeed.HasValue && r.LoadSpeed < 1000)
                        IncrementFault(faultCounts, "负载转速偏低");
                    else
                        IncrementFault(faultCounts, "负载综合不合格");
                }
            }

            return faultCounts
                .OrderByDescending(kv => kv.Value)
                .Take(Math.Max(1, topN))
                .Select((kv, i) => new FaultRankItem
                {
                    Rank = i + 1,
                    Name = kv.Key,
                    Count = kv.Value
                })
                .ToList();
        }

        // ===== 内部辅助方法 =====

        private static void IncrementFault(Dictionary<string, int> dict, string key)
        {
            if (dict.ContainsKey(key))
                dict[key]++;
            else
                dict[key] = 1;
        }

        private static void ApplyStage(MotorTestRecordEntity record, StageTestData data)
        {
            string stageResult = NormalizeResult(data.Result);

            switch (data.Stage)
            {
                case TestStage.NoLoad:
                    record.NoLoadCurrent = Round(data.NoLoadCurrent, 3);
                    record.NoLoadSpeed = data.NoLoadSpeed;
                    record.ShaftLength = Round(data.ShaftLength, 3);
                    record.KnurlDiameter = Round(data.KnurlDiameter, 3);
                    record.NoLoadResult = stageResult;
                    break;
                case TestStage.Noise:
                    record.FwdNoise = Round(data.FwdNoise, 2);
                    record.RevNoise = Round(data.RevNoise, 2);
                    record.NoiseDiff = Round(data.NoiseDiff, 2);
                    record.NoiseResult = stageResult;
                    break;
                case TestStage.Load:
                    record.LoadCurrent = Round(data.LoadCurrent, 3);
                    record.LoadSpeed = data.LoadSpeed;
                    record.LoadResult = stageResult;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(data), $"Unsupported stage: {data.Stage}");
            }
        }

        private static string CalculateFinalResult(MotorTestRecordEntity record)
        {
            return record.NoLoadResult == "OK" &&
                   record.NoiseResult == "OK" &&
                   record.LoadResult == "OK"
                ? "OK"
                : "NG";
        }

        private static string NormalizeResult(string? result)
        {
            return string.Equals(result, "OK", StringComparison.OrdinalIgnoreCase) ? "OK" : "NG";
        }

        private static bool IsAllFilter(string? filter)
        {
            return string.IsNullOrWhiteSpace(filter) ||
                   filter == "全部" ||
                   string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase);
        }

        private static double? Round(double? value, int digits)
        {
            return value.HasValue ? Math.Round(value.Value, digits) : null;
        }

        /// <summary>
        /// 实体 → 领域模型转换
        /// </summary>
        private static MotorTestResult ToModel(MotorTestRecordEntity entity)
        {
            return new MotorTestResult
            {
                Barcode = entity.Barcode,
                TestTime = entity.TestTime,
                FinalResult = entity.FinalResult,
                NoLoadCurrent = entity.NoLoadCurrent,
                NoLoadSpeed = entity.NoLoadSpeed,
                ShaftLength = entity.ShaftLength,
                KnurlDiameter = entity.KnurlDiameter,
                NoLoadResult = entity.NoLoadResult,
                FwdNoise = entity.FwdNoise,
                RevNoise = entity.RevNoise,
                NoiseDiff = entity.NoiseDiff,
                NoiseResult = entity.NoiseResult,
                LoadCurrent = entity.LoadCurrent,
                LoadSpeed = entity.LoadSpeed,
                LoadResult = entity.LoadResult
            };
        }
    }
}
