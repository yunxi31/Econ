namespace MotorTestSystem.Models
{
    public sealed class ProductionSummary
    {
        public int TotalChecked { get; set; }
        public int OkCount { get; set; }
        public int NgCount { get; set; }
        public double PassRate { get; set; }
    }
}
