using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services
{
    public sealed class InMemoryMotorTestRepository : IMotorTestRepository
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, MotorTestResult> _records = new(StringComparer.OrdinalIgnoreCase);

        public Task UpsertStageResultAsync(StageTestData data, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(data);
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(data.Barcode))
            {
                throw new ArgumentException("Barcode is required before saving a stage result.", nameof(data));
            }

            lock (_gate)
            {
                string barcode = data.Barcode.Trim();
                if (!_records.TryGetValue(barcode, out var record))
                {
                    record = new MotorTestResult { Barcode = barcode };
                    _records[barcode] = record;
                }

                ApplyStage(record, data);
                record.TestTime = data.CollectedAt;
                record.FinalResult = CalculateFinalResult(record);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MotorTestResult>> QueryAsync(MotorTestQuery query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<MotorTestResult> results;
            lock (_gate)
            {
                results = _records.Values
                    .Where(r => string.IsNullOrWhiteSpace(query.Barcode) ||
                        r.Barcode.Contains(query.Barcode.Trim(), StringComparison.OrdinalIgnoreCase))
                    .Where(r => IsAllFilter(query.ResultFilter) || r.FinalResult == query.ResultFilter)
                    .Where(r => r.TestTime >= query.StartTime && r.TestTime <= query.EndTime)
                    .OrderByDescending(r => r.TestTime)
                    .Select(Clone)
                    .ToList();
            }

            return Task.FromResult(results);
        }

        public Task<IReadOnlyList<MotorTestResult>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<MotorTestResult> results;
            lock (_gate)
            {
                results = _records.Values
                    .OrderByDescending(r => r.TestTime)
                    .Take(Math.Max(0, count))
                    .Select(Clone)
                    .ToList();
            }

            return Task.FromResult(results);
        }

        public Task<ProductionSummary> GetSummaryAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ProductionSummary summary;
            lock (_gate)
            {
                var scoped = _records.Values
                    .Where(r => r.TestTime >= startTime && r.TestTime <= endTime)
                    .ToList();

                int ok = scoped.Count(r => r.FinalResult == "OK");
                int ng = scoped.Count(r => r.FinalResult == "NG");
                int total = scoped.Count;

                summary = new ProductionSummary
                {
                    TotalChecked = total,
                    OkCount = ok,
                    NgCount = ng,
                    PassRate = total == 0 ? 0 : Math.Round(ok * 100.0 / total, 2)
                };
            }

            return Task.FromResult(summary);
        }

        private static void ApplyStage(MotorTestResult record, StageTestData data)
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

        private static string CalculateFinalResult(MotorTestResult record)
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
                   filter == "鍏ㄩ儴" ||
                   string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase);
        }

        private static double? Round(double? value, int digits)
        {
            return value.HasValue ? Math.Round(value.Value, digits) : null;
        }

        private static MotorTestResult Clone(MotorTestResult source)
        {
            return new MotorTestResult
            {
                Barcode = source.Barcode,
                TestTime = source.TestTime,
                FinalResult = source.FinalResult,
                NoLoadCurrent = source.NoLoadCurrent,
                NoLoadSpeed = source.NoLoadSpeed,
                ShaftLength = source.ShaftLength,
                KnurlDiameter = source.KnurlDiameter,
                NoLoadResult = source.NoLoadResult,
                FwdNoise = source.FwdNoise,
                RevNoise = source.RevNoise,
                NoiseDiff = source.NoiseDiff,
                NoiseResult = source.NoiseResult,
                LoadCurrent = source.LoadCurrent,
                LoadSpeed = source.LoadSpeed,
                LoadResult = source.LoadResult
            };
        }
    }
}
