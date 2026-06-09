using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MotorTestSystem.Models;

namespace MotorTestSystem.Services;

public sealed class InMemoryMotorTestRepository : IMotorTestRepository
{
	private readonly object _gate = new object();

	private readonly Dictionary<string, MotorTestResult> _records = new Dictionary<string, MotorTestResult>(StringComparer.OrdinalIgnoreCase);

	public Task UpsertStageResultAsync(StageTestData data, CancellationToken cancellationToken = default(CancellationToken))
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		cancellationToken.ThrowIfCancellationRequested();
		if (string.IsNullOrWhiteSpace(data.Barcode))
		{
			throw new ArgumentException("Barcode is required before saving a stage result.", "data");
		}
		lock (_gate)
		{
			string text = data.Barcode.Trim();
			if (!_records.TryGetValue(text, out MotorTestResult value))
			{
				value = new MotorTestResult
				{
					Barcode = text
				};
				_records[text] = value;
			}
			ApplyStage(value, data);
			value.TestTime = data.CollectedAt;
			value.FinalResult = CalculateFinalResult(value);
		}
		return Task.CompletedTask;
	}

	public Task<IReadOnlyList<MotorTestResult>> QueryAsync(MotorTestQuery query, CancellationToken cancellationToken = default(CancellationToken))
	{
		ArgumentNullException.ThrowIfNull(query, "query");
		cancellationToken.ThrowIfCancellationRequested();
		IReadOnlyList<MotorTestResult> result;
		lock (_gate)
		{
			result = (from r in _records.Values
				where string.IsNullOrWhiteSpace(query.Barcode) || r.Barcode.Contains(query.Barcode.Trim(), StringComparison.OrdinalIgnoreCase)
				where IsAllFilter(query.ResultFilter) || r.FinalResult == query.ResultFilter
				where r.TestTime >= query.StartTime && r.TestTime <= query.EndTime
				orderby r.TestTime descending
				select r).Select(Clone).ToList();
		}
		return Task.FromResult(result);
	}

	public Task<IReadOnlyList<MotorTestResult>> GetRecentAsync(int count, CancellationToken cancellationToken = default(CancellationToken))
	{
		cancellationToken.ThrowIfCancellationRequested();
		IReadOnlyList<MotorTestResult> result;
		lock (_gate)
		{
			result = _records.Values.OrderByDescending((MotorTestResult r) => r.TestTime).Take(Math.Max(0, count)).Select(Clone)
				.ToList();
		}
		return Task.FromResult(result);
	}

	public Task<ProductionSummary> GetSummaryAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default(CancellationToken))
	{
		cancellationToken.ThrowIfCancellationRequested();
		ProductionSummary result;
		lock (_gate)
		{
			List<MotorTestResult> list = _records.Values.Where((MotorTestResult r) => r.TestTime >= startTime && r.TestTime <= endTime).ToList();
			int num = list.Count((MotorTestResult r) => r.FinalResult == "OK");
			int ngCount = list.Count((MotorTestResult r) => r.FinalResult == "NG");
			int count = list.Count;
			result = new ProductionSummary
			{
				TotalChecked = count,
				OkCount = num,
				NgCount = ngCount,
				PassRate = ((count == 0) ? 0.0 : Math.Round((double)num * 100.0 / (double)count, 2))
			};
		}
		return Task.FromResult(result);
	}

	private static void ApplyStage(MotorTestResult record, StageTestData data)
	{
		string text = NormalizeResult(data.Result);
		switch (data.Stage)
		{
		case TestStage.NoLoad:
			record.NoLoadCurrent = Round(data.NoLoadCurrent, 3);
			record.NoLoadSpeed = data.NoLoadSpeed;
			record.ShaftLength = Round(data.ShaftLength, 3);
			record.KnurlDiameter = Round(data.KnurlDiameter, 3);
			record.NoLoadResult = text;
			break;
		case TestStage.Noise:
			record.FwdNoise = Round(data.FwdNoise, 2);
			record.RevNoise = Round(data.RevNoise, 2);
			record.NoiseDiff = Round(data.NoiseDiff, 2);
			record.NoiseResult = text;
			break;
		case TestStage.Load:
			record.LoadCurrent = Round(data.LoadCurrent, 3);
			record.LoadSpeed = data.LoadSpeed;
			record.LoadResult = text;
			break;
		default:
			throw new ArgumentOutOfRangeException("data", $"Unsupported stage: {data.Stage}");
		}
	}

	private static string CalculateFinalResult(MotorTestResult record)
	{
		return (record.NoLoadResult == "OK" && record.NoiseResult == "OK" && record.LoadResult == "OK") ? "OK" : "NG";
	}

	private static string NormalizeResult(string? result)
	{
		return string.Equals(result, "OK", StringComparison.OrdinalIgnoreCase) ? "OK" : "NG";
	}

	private static bool IsAllFilter(string? filter)
	{
		return string.IsNullOrWhiteSpace(filter) || filter == "全部" || filter == "鍏ㄩ儴" || string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase);
	}

	private static double? Round(double? value, int digits)
	{
		return value.HasValue ? new double?(Math.Round(value.Value, digits)) : ((double?)null);
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
