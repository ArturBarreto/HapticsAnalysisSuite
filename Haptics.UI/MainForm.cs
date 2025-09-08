using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using Haptics.Core.Models;
using Haptics.Core.Services;
using ScottPlot.WinForms;

namespace Haptics.UI
{
    public class MainForm : Form
    {
        private Button btnLoad = new Button();
        private Button btnCalc = new Button();
        private Label lblFile = new Label();
        private TextBox txtMetrics = new TextBox();

        private FormsPlot plotTime = new FormsPlot();
        private FormsPlot plotFxD = new FormsPlot();

        private TabControl tabPlots = new TabControl();
        private TabPage tabTime = new TabPage("Time Series");
        private TabPage tabFxD = new TabPage("Force vs Distance");

        private DataPoint[]? _data;
        private HapticMetrics? _metrics;

        private SplitContainer mainSplit = new SplitContainer();

        // your preferred layout parameters
        private const double DesiredLeftRatio = 0.72; // 72% left for plots
        private const int DesiredPanel1Min = 300;     // min px left
        private const int DesiredPanel2Min = 250;     // min px right

        public MainForm()
        {
            Text = "Haptics Analysis";
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1100, 700);

            // --- RIGHT PANEL: controls + metrics ---
            btnLoad.Text = "Load CSV…";
            btnLoad.AutoSize = true;
            btnLoad.Click += (s, e) => LoadCsv();

            btnCalc.Text = "Calculate";
            btnCalc.AutoSize = true;
            btnCalc.Click += (s, e) => CalculateAndRender();

            lblFile.AutoSize = true;
            lblFile.MaximumSize = new Size(450, 0); // wrap long filenames
            lblFile.Font = new Font(lblFile.Font, FontStyle.Bold);

            txtMetrics.Multiline = true;
            txtMetrics.ReadOnly = true;
            txtMetrics.ScrollBars = ScrollBars.Vertical;
            txtMetrics.Font = new Font("Consolas", 10f);
            txtMetrics.Dock = DockStyle.Fill;

            var rightTopButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(6),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };
            rightTopButtons.Controls.AddRange(new Control[] { btnLoad, btnCalc });

            var rightInfo = new Panel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6, 0, 6, 6) };
            rightInfo.Controls.Add(lblFile);

            var rightMetrics = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
            rightMetrics.Controls.Add(txtMetrics);

            var rightPanel = new Panel { Dock = DockStyle.Fill };
            rightPanel.Controls.Add(rightMetrics);
            rightPanel.Controls.Add(rightInfo);
            rightPanel.Controls.Add(rightTopButtons);

            // --- LEFT PANEL: plots in tabs ---
            plotTime.Dock = DockStyle.Fill;
            plotFxD.Dock = DockStyle.Fill;

            tabTime.Controls.Add(plotTime);
            tabFxD.Controls.Add(plotFxD);

            tabPlots.Dock = DockStyle.Fill;
            tabPlots.TabPages.Add(tabTime);
            tabPlots.TabPages.Add(tabFxD);

            // --- MAIN SPLIT: left (plots) / right (controls+metrics) ---
            mainSplit.Dock = DockStyle.Fill;
            mainSplit.Orientation = Orientation.Vertical;

            // Do NOT set Panel1MinSize/Panel2MinSize yet (can throw). Set later safely.
            mainSplit.Panel1.Controls.Add(tabPlots);
            mainSplit.Panel2.Controls.Add(rightPanel);

            Controls.Add(mainSplit);

            // Configure split safely when layout exists
            this.Load += (s, e) => ConfigureSplitSafely(DesiredLeftRatio, DesiredPanel1Min, DesiredPanel2Min);

            // On resize, just keep the ratio inside legal bounds (don't change min sizes)
            this.SizeChanged += (s, e) => AdjustSplitterSafely(DesiredLeftRatio);

            // Try to auto-load the sample file if it's present in /data
            var defaultPath = Path.Combine(AppContext.BaseDirectory, "data", "TaskData 1.csv");
            if (File.Exists(defaultPath))
            {
                LoadCsv(defaultPath);
                CalculateAndRender();
            }
        }

        /// <summary>
        /// Set min sizes and SplitterDistance in a safe order to avoid exceptions:
        /// 1) compute a temporary legal SplitterDistance for the desired Panel2MinSize
        /// 2) set SplitterDistance to that temp value
        /// 3) set Panel2MinSize and Panel1MinSize
        /// 4) set final SplitterDistance to the desired ratio, clamped within legal bounds
        /// </summary>
        private void ConfigureSplitSafely(double leftRatio, int desiredP1Min, int desiredP2Min)
        {
            if (leftRatio <= 0) leftRatio = 0.5;
            if (leftRatio >= 1) leftRatio = 0.9;

            int w = mainSplit.ClientSize.Width;
            int sw = mainSplit.SplitterWidth;
            if (w <= 0) return;

            // Step 1: compute a temp legal distance that will be valid for the desired Panel2MinSize
            int tempMin1 = Math.Max(1, desiredP1Min);
            int tempMin2 = Math.Max(1, desiredP2Min);
            // ensure there is at least some space for both panels
            if (tempMin1 + sw + tempMin2 > w)
            {
                // shrink right min first, then left if needed
                tempMin2 = Math.Max(1, w - sw - tempMin1);
                if (tempMin1 + sw + tempMin2 > w)
                    tempMin1 = Math.Max(1, w - sw - tempMin2);
            }

            int minDistanceForDesiredP2 = tempMin1;
            int maxDistanceForDesiredP2 = w - sw - tempMin2;
            if (maxDistanceForDesiredP2 < minDistanceForDesiredP2)
                maxDistanceForDesiredP2 = minDistanceForDesiredP2;

            int tempDistance = Math.Min(
                Math.Max((int)(w * leftRatio), minDistanceForDesiredP2),
                maxDistanceForDesiredP2);

            // Step 2: set a legal SplitterDistance BEFORE changing min sizes
            if (mainSplit.SplitterDistance != tempDistance)
                mainSplit.SplitterDistance = tempDistance;

            // Step 3: now it is safe to set min sizes
            mainSplit.Panel1MinSize = tempMin1;
            mainSplit.Panel2MinSize = tempMin2;

            // Step 4: set the final desired ratio (clamped to new legal bounds)
            AdjustSplitterSafely(leftRatio);
        }

        /// <summary>
        /// Adjust SplitterDistance to a ratio, clamped using the current min sizes.
        /// Safe to call on resize. Does not change min sizes.
        /// </summary>
        private void AdjustSplitterSafely(double leftRatio)
        {
            if (leftRatio <= 0) leftRatio = 0.5;
            if (leftRatio >= 1) leftRatio = 0.9;

            int w = mainSplit.ClientSize.Width;
            int sw = mainSplit.SplitterWidth;
            if (w <= 0) return;

            int minDist = mainSplit.Panel1MinSize;
            int maxDist = w - sw - mainSplit.Panel2MinSize;
            if (maxDist < minDist) maxDist = minDist;

            int target = (int)(w * leftRatio);
            if (target < minDist) target = minDist;
            if (target > maxDist) target = maxDist;

            if (mainSplit.SplitterDistance != target)
                mainSplit.SplitterDistance = target;
        }

        private void LoadCsv(string? path = null)
        {
            if (path is null)
            {
                using var ofd = new OpenFileDialog
                {
                    Filter = "CSV files|*.csv|All files|*.*",
                    FileName = "TaskData 1.csv"
                };
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                path = ofd.FileName;
            }

            try
            {
                _data = DataLoader.LoadCsv(path).ToArray();
                lblFile.Text = $"Loaded: {Path.GetFileName(path)}   (#rows={_data.Length})";
                RenderTimePlot();
                RenderFxDPlot();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Load error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CalculateAndRender()
        {
            if (_data is null || _data.Length == 0)
            {
                MessageBox.Show(this, "No data loaded.", "Info");
                return;
            }

            _metrics = HapticsAnalyzer.Compute(_data);
            RenderTimePlot();
            RenderFxDPlot();

            txtMetrics.Text =
$@"Results
-------
Fa  (actuation force)           : {_metrics.Fa:F3} N   @ index {_metrics.FaIndex}
Fra (actuation return force)    : {_metrics.Fra:F3} N   @ index {_metrics.FraIndex}
ΔF  (tactile effect = Fa - Fra) : {_metrics.DeltaF:F3} N
Frr (return force near rest)    : {_metrics.Frr:F3} N
Tm  (mechanical travel total)   : {_metrics.Tm:F3} mm

Voltage levels (auto-detected)
High median: {_metrics.HighVoltageMedian:F3} V
Low  median: {_metrics.LowVoltageMedian:F3} V
Threshold  : {_metrics.ThresholdUsed:F3} V";
        }

        private void RenderTimePlot()
        {
            plotTime.Plot.Clear();

            if (_data is null) { plotTime.Refresh(); return; }

            double[] xs = _data.Select(p => (double)p.Index).ToArray();
            double[] force = _data.Select(p => p.ForceN).ToArray();
            double[] volt = _data.Select(p => p.VoltageV).ToArray();

            // Force (left Y axis)
            var forcePlt = plotTime.Plot.Add.Scatter(xs, force);
            forcePlt.LegendText = "Force (N)";
            forcePlt.LineWidth = 2;
            forcePlt.Color = ScottPlot.Colors.DarkBlue;

            plotTime.Plot.XLabel("Index");
            plotTime.Plot.YLabel("Force (N)");

            // Voltage (right Y axis)
            var rightAxis = plotTime.Plot.Axes.Right;
            var voltPlt = plotTime.Plot.Add.Scatter(xs, volt);
            voltPlt.Axes.YAxis = rightAxis;         // bind to right Y
            voltPlt.LegendText = "Voltage (V)";
            voltPlt.LineWidth = 2;
            voltPlt.Color = ScottPlot.Colors.DarkGreen;
            rightAxis.Label.Text = "Voltage (V)";

            plotTime.Plot.Title("Force & Voltage vs Index");

            if (_metrics != null)
            {
                // Fa marker line
                var vlFa = plotTime.Plot.Add.VerticalLine(_metrics.FaIndex);
                vlFa.LabelText = "Fa (actuation)";
                vlFa.LineWidth = 2;
                vlFa.Color = ScottPlot.Colors.Red;

                // Fra marker line
                var vlFra = plotTime.Plot.Add.VerticalLine(_metrics.FraIndex);
                vlFra.LabelText = "Fra (return)";
                vlFra.LineWidth = 2;
                vlFra.Color = ScottPlot.Colors.Orange;

                // Threshold line (on right axis)
                var th = plotTime.Plot.Add.HorizontalLine(_metrics.ThresholdUsed);
                th.Axes.YAxis = rightAxis;           // draw against right Y
                th.LabelText = "Threshold";
                th.LineWidth = 2;
                th.Color = ScottPlot.Colors.Purple;
            }

            // Show a complete legend
            plotTime.Plot.Legend.IsVisible = true;
            plotTime.Plot.Legend.Location = ScottPlot.Alignment.UpperRight;

            plotTime.Plot.Axes.AutoScale();
            plotTime.Refresh();
        }


        private void RenderFxDPlot()
        {
            plotFxD.Plot.Clear();

            if (_data is null) { plotFxD.Refresh(); return; }

            // X = travel (Linear mm)
            double[] x = _data.Select(p => p.LinearMm).ToArray();
            double[] f = _data.Select(p => p.ForceN).ToArray();
            double[] v = _data.Select(p => p.VoltageV).ToArray();

            // ----- FORCE curve (left Y) -----
            var forceCurve = plotFxD.Plot.Add.Scatter(x, f);
            forceCurve.LegendText = "Force (N)";
            forceCurve.LineWidth = 2;
            forceCurve.Color = ScottPlot.Colors.DarkBlue;

            // ----- VOLTAGE curve (right Y) -----
            var rightAxis = plotFxD.Plot.Axes.Right;
            var voltCurve = plotFxD.Plot.Add.Scatter(x, v);
            voltCurve.Axes.YAxis = rightAxis;          // bind to right axis
            voltCurve.LegendText = "Voltage (V)";
            voltCurve.LineWidth = 2;
            voltCurve.Color = ScottPlot.Colors.DarkGreen;
            rightAxis.Label.Text = "Voltage (V)";

            plotFxD.Plot.XLabel("Linear (mm)");
            plotFxD.Plot.YLabel("Force (N)");
            plotFxD.Plot.Title("Force vs Distance");
            plotFxD.Plot.Legend.IsVisible = true;

            // ----- Markers: Fa, Fra, Frr -----
            if (_metrics != null)
            {
                // Fa and Fra at their real coordinates
                var faPt = _data.FirstOrDefault(p => p.Index == _metrics.FaIndex);
                var frPt = _data.FirstOrDefault(p => p.Index == _metrics.FraIndex);

                if (faPt != null)
                {
                    var faMark = plotFxD.Plot.Add.Scatter(
                        new double[] { faPt.LinearMm },
                        new double[] { faPt.ForceN });
                    faMark.LineWidth = 0;
                    faMark.MarkerSize = 9;
                    faMark.LegendText = $"Fa ({_metrics.Fa:F2} N)";
                    faMark.Color = ScottPlot.Colors.Red;
                }

                if (frPt != null)
                {
                    var fraMark = plotFxD.Plot.Add.Scatter(
                        new double[] { frPt.LinearMm },
                        new double[] { frPt.ForceN });
                    fraMark.LineWidth = 0;
                    fraMark.MarkerSize = 9;
                    fraMark.LegendText = $"Fra ({_metrics.Fra:F2} N)";
                    fraMark.Color = ScottPlot.Colors.Orange;
                }

                // Frr: use a representative X near the start position after release
                double startMm = _data.First().LinearMm;
                int idxMax = _data
                    .Select((p, i) => new { p.LinearMm, i })
                    .OrderByDescending(t => t.LinearMm)
                    .First().i;

                // same window as analyzer (±0.02 mm around start after peak)
                const double window = 0.02;
                var nearStartAfterRelease = _data
                    .Skip(idxMax)
                    .Where(p => Math.Abs(p.LinearMm - startMm) <= window)
                    .ToArray();

                double xFrr = nearStartAfterRelease.Length > 0
                    ? nearStartAfterRelease.Select(p => p.LinearMm).OrderBy(z => z).Skip(nearStartAfterRelease.Length / 2).First()
                    : startMm;

                var frrMark = plotFxD.Plot.Add.Scatter(
                    new double[] { xFrr },
                    new double[] { _metrics.Frr });
                frrMark.LineWidth = 0;
                frrMark.MarkerSize = 9;
                frrMark.LegendText = $"Frr ({_metrics.Frr:F2} N)";
                frrMark.Color = ScottPlot.Colors.Purple;

                // Optional: also show the voltage threshold as a horizontal line on right axis
                var th = plotFxD.Plot.Add.HorizontalLine(_metrics.ThresholdUsed);
                th.Axes.YAxis = rightAxis;   // this line belongs to the voltage axis
                th.LabelText = "Threshold";
                th.Color = ScottPlot.Colors.Gray;
                th.LineWidth = 2;
            }

            plotFxD.Plot.Axes.AutoScale();
            plotFxD.Refresh();
        }
    }
}
