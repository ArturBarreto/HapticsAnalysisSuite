using Haptics.Core.Models;
using System.Linq;

namespace Haptics.Core.Services
{
    public static class HapticsAnalyzer
    {
        public sealed class Options
        {
            /// <summary>How close to the starting position (mm) we consider "returned".</summary>
            public double ReturnWindowMm { get; set; } = 0.02;

            /// <summary>Number of samples over which to take the median for Frr near the end.</summary>
            public int FrrWindowSamples { get; set; } = 50;

            /// <summary>When computing voltage medians, take high from the bottom X% of travel and low from top X%.</summary>
            public double TravelPercentForVoltageMedians { get; set; } = 5.0;
        }

        /// <summary>
        /// Compute Fa, Fra, Frr, Tm (+ ΔF) from the data.
        /// Definitions (industry standard): Fa=actuation force, Fra=actuation return force,
        /// Tm=mechanical travel (total), Frr=return force near rest; ΔF=Fa-Fra (tactile effect).
        /// </summary>
        public static HapticMetrics Compute(IReadOnlyList<DataPoint> data, Options? options = null)
        {
            options ??= new Options();

            if (data == null || data.Count == 0)
                throw new ArgumentException("No data");

            // Split into press (increasing distance up to max) and release (after)
            int idxMax = data.Select((p, i) => (p.LinearMm, i))
                             .Max().i;

            var press = data.Take(idxMax + 1).ToList();
            var release = data.Skip(idxMax).ToList();

            // Determine voltage levels automatically (median near start vs near bottom)
            double travel = data.Max(p => p.LinearMm) - data.Min(p => p.LinearMm);
            double minMm = data.Min(p => p.LinearMm);
            double startBandTop = minMm + (options.TravelPercentForVoltageMedians / 100.0) * travel;
            double bottomBandBottom = minMm + (100.0 - options.TravelPercentForVoltageMedians) / 100.0 * travel;

            double highV = Median(data.Where(p => p.LinearMm <= startBandTop).Select(p => p.VoltageV));
            double lowV = Median(data.Where(p => p.LinearMm >= bottomBandBottom).Select(p => p.VoltageV));
            double threshold = (highV + lowV) / 2.0;

            // Find Fa: first crossing below threshold on press
            int faIdx = press.FindIndex(p => p.VoltageV < threshold);
            if (faIdx < 0) faIdx = press.Count - 1;
            var faPoint = press[faIdx];

            // Find Fra: first crossing above threshold on release
            int fraOffset = release.FindIndex(p => p.VoltageV > threshold);
            if (fraOffset < 0) fraOffset = release.Count - 1;
            var fraPoint = release[fraOffset];
            int fraIdxGlobal = idxMax + fraOffset;

            // Tm: total mechanical travel
            double tm = travel;

            // Frr: force after release, very near starting position (median to be robust)
            double startMm = data.First().LinearMm;
            var postReleaseNearStart = data
                .Skip(idxMax)
                .Where(p => Math.Abs(p.LinearMm - startMm) <= options.ReturnWindowMm)
                .Select(p => p.ForceN)
                .ToList();

            double frr = postReleaseNearStart.Count > 0
                ? Median(postReleaseNearStart)
                : Median(data.TakeLast(options.FrrWindowSamples).Select(p => p.ForceN));

            return new HapticMetrics
            {
                Fa = faPoint.ForceN,
                FaIndex = faPoint.Index,
                Fra = fraPoint.ForceN,
                FraIndex = fraIdxGlobal,
                Frr = frr,
                Tm = tm,
                HighVoltageMedian = highV,
                LowVoltageMedian = lowV,
                ThresholdUsed = threshold
            };
        }

        private static double Median(IEnumerable<double> seq)
        {
            var a = seq.Where(x => !double.IsNaN(x) && !double.IsInfinity(x)).OrderBy(x => x).ToArray();
            if (a.Length == 0) return double.NaN;
            int mid = a.Length / 2;
            return a.Length % 2 == 1 ? a[mid] : 0.5 * (a[mid - 1] + a[mid]);
        }
    }
}
