using System;

namespace MotorTestSystem.Models
{
    public class MotorTestResult
    {
        public string Barcode { get; set; } = string.Empty;
        public DateTime TestTime { get; set; }
        public string FinalResult { get; set; } = "NG";

        // No-load stage
        public double? NoLoadCurrent { get; set; }
        public int? NoLoadSpeed { get; set; }
        public double? ShaftLength { get; set; }
        public double? KnurlDiameter { get; set; }
        public string? NoLoadResult { get; set; }

        // Noise stage
        public double? FwdNoise { get; set; }
        public double? RevNoise { get; set; }
        public double? NoiseDiff { get; set; }
        public string? NoiseResult { get; set; }

        // Load stage
        public double? LoadCurrent { get; set; }
        public int? LoadSpeed { get; set; }
        public string? LoadResult { get; set; }
    }
}
