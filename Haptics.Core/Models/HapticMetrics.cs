namespace Haptics.Core.Models
{
    public class HapticMetrics
    {
        public double Fa { get; set; }      // Actuation force (downstroke) at electrical actuation
        public int FaIndex { get; set; }

        public double Fra { get; set; }     // Actuation return force (upstroke) at electrical de-actuation
        public int FraIndex { get; set; }

        public double Frr { get; set; }     // Return force near rest after release
        public double Tm { get; set; }      // Mechanical travel (total travel)
        public double DeltaF => Fa - Fra;   // Tactile effect
        public double HighVoltageMedian { get; set; }
        public double LowVoltageMedian { get; set; }
        public double ThresholdUsed { get; set; }
    }
}
