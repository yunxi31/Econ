namespace MotorTestSystem.Models
{
    /// <summary>
    /// 各测试阶段的不良数量统计
    /// </summary>
    public sealed class DefectSummary
    {
        /// <summary>空载阶段不良数</summary>
        public int NoLoadNgCount { get; set; }

        /// <summary>噪音阶段不良数</summary>
        public int NoiseNgCount { get; set; }

        /// <summary>负载阶段不良数</summary>
        public int LoadNgCount { get; set; }

        /// <summary>不良总数</summary>
        public int TotalNgCount => NoLoadNgCount + NoiseNgCount + LoadNgCount;

        /// <summary>空载不良占比 (%)</summary>
        public double NoLoadPercentage => TotalNgCount == 0 ? 0 : Math.Round(NoLoadNgCount * 100.0 / TotalNgCount, 1);

        /// <summary>噪音不良占比 (%)</summary>
        public double NoisePercentage => TotalNgCount == 0 ? 0 : Math.Round(NoiseNgCount * 100.0 / TotalNgCount, 1);

        /// <summary>负载不良占比 (%)</summary>
        public double LoadPercentage => TotalNgCount == 0 ? 0 : Math.Round(LoadNgCount * 100.0 / TotalNgCount, 1);
    }
}
