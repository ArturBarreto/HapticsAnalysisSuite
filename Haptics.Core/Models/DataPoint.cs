using System;

namespace Haptics.Core.Models
{
    public class DataPoint
    {
        public int Index { get; set; }
        public double ForceN { get; set; }
        public double VoltageV { get; set; }
        public double LinearMm { get; set; }
        public string? TimeRaw { get; set; } // not used in analysis; kept for completeness
    }
}
