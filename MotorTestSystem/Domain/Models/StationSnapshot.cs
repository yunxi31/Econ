namespace MotorTestSystem.Models
{
    public sealed class StationSnapshot
    {
        public string StationId { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public int Status { get; set; }
        public bool CompletionSignal { get; set; }
        public StageTestData? CompletedData { get; set; }

        /// <summary>
        /// PLC 数据序列号（用于断网重连后的序列号跳跃检测）。
        /// 由 PlcPollingService 从 PLC 读取，如果协议支持。
        /// </summary>
        public int? SequenceNumber { get; set; }
    }
}
