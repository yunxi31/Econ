namespace MotorTestSystem.Models
{
    /// <summary>
    /// 故障原因排行项
    /// </summary>
    public sealed class FaultRankItem
    {
        /// <summary>排名</summary>
        public int Rank { get; set; }

        /// <summary>故障类别名称</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>发生次数</summary>
        public int Count { get; set; }
    }
}
