using System;

namespace MotorTestSystem.ViewModels;

public class MotorTestRecordModel
{
	public string Barcode { get; set; } = string.Empty;

	public DateTime TestTime { get; set; }

	public string FinalResult { get; set; } = "OK";

	public double? NoLoadCurrent { get; set; }

	public double? NoLoadSpeed { get; set; }

	public double? FwdNoise { get; set; }

	public double? RevNoise { get; set; }

	public double? LoadCurrent { get; set; }

	public double? LoadSpeed { get; set; }

	public string NoLoadCurrentText => NoLoadCurrent?.ToString("F2") ?? "NULL";

	public string NoLoadSpeedText => NoLoadSpeed?.ToString("F0") ?? "NULL";

	public string FwdNoiseText => FwdNoise?.ToString("F1") ?? "NULL";

	public string RevNoiseText => RevNoise?.ToString("F1") ?? "NULL";

	public string LoadCurrentText => LoadCurrent?.ToString("F2") ?? "NULL";

	public string LoadSpeedText => LoadSpeed?.ToString("F0") ?? "NULL";

	public bool IsNoLoadCurrentAbnormal => NoLoadCurrent > 1.5;

	public bool IsNoLoadSpeedAbnormal => NoLoadSpeed < 2900.0 || NoLoadSpeed > 3100.0;

	public bool IsFwdNoiseAbnormal => FwdNoise > 60.0;

	public bool IsRevNoiseAbnormal => RevNoise > 60.0;

	public bool IsLoadCurrentAbnormal => LoadCurrent > 4.5;

	public bool IsLoadSpeedAbnormal => LoadSpeed < 2900.0 || LoadSpeed > 3100.0;
}
