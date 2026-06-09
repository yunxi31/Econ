namespace MotorTestSystem.Models;

public sealed class StationSnapshot
{
	public string StationId { get; set; } = string.Empty;

	public bool IsOnline { get; set; }

	public int Status { get; set; }

	public bool CompletionSignal { get; set; }

	public StageTestData? CompletedData { get; set; }
}
