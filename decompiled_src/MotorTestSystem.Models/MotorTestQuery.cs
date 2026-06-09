using System;

namespace MotorTestSystem.Models;

public sealed class MotorTestQuery
{
	public string Barcode { get; set; } = string.Empty;

	public string ResultFilter { get; set; } = "全部";

	public DateTime StartTime { get; set; } = DateTime.Now.AddDays(-7.0);

	public DateTime EndTime { get; set; } = DateTime.Now;
}
