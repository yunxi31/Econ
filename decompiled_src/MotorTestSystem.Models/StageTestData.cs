using System;

namespace MotorTestSystem.Models;

public sealed class StageTestData
{
	public string Barcode { get; set; } = string.Empty;

	public string StationId { get; set; } = string.Empty;

	public TestStage Stage { get; set; }

	public DateTime CollectedAt { get; set; } = DateTime.Now;

	public string Result { get; set; } = "NG";

	public double? NoLoadCurrent { get; set; }

	public int? NoLoadSpeed { get; set; }

	public double? ShaftLength { get; set; }

	public double? KnurlDiameter { get; set; }

	public double? FwdNoise { get; set; }

	public double? RevNoise { get; set; }

	public double? NoiseDiff { get; set; }

	public double? LoadCurrent { get; set; }

	public int? LoadSpeed { get; set; }
}
