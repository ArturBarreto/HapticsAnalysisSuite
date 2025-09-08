using System;
using System.IO;
using System.Linq;
using Haptics.Core.Services;
using Xunit;

namespace Haptics.Tests
{
    public class MetricsTests
    {
        private static string SamplePath =>
            Path.Combine(AppContext.BaseDirectory, "data", "TaskData 1.csv");

        [Fact]
        public void CanLoadCsv()
        {
            var data = DataLoader.LoadCsv(SamplePath);
            Assert.NotEmpty(data);
            Assert.Equal(5, typeof(Haptics.Core.Models.DataPoint)
                .GetProperties().Length); // sanity
        }

        [Fact]
        public void MetricsMatchExpectedTolerances()
        {
            var data = DataLoader.LoadCsv(SamplePath);
            var metrics = HapticsAnalyzer.Compute(data);

            // Expected values measured on the provided dataset
            // (Fa, Fra, Frr, Tm) with reasonable tolerances
            Assert.InRange(metrics.Fa, 3.20, 3.35);     // ~3.267 N
            Assert.InRange(metrics.Fra, 1.90, 2.05);    // ~1.963 N
            Assert.InRange(metrics.Tm, 2.27, 2.30);     // ~2.286 mm
            Assert.InRange(metrics.Frr, -0.20, 0.05);   // ~-0.106 N near rest
            Assert.InRange(metrics.DeltaF, 1.20, 1.40); // ~1.304 N

            // Voltage threshold check
            Assert.InRange(metrics.HighVoltageMedian, 8.35, 8.50); // ~8.412 V
            Assert.InRange(metrics.LowVoltageMedian, 1.90, 2.10); // ~1.976 V
            Assert.InRange(metrics.ThresholdUsed, 5.05, 5.30); // mid-point
        }
    }
}
