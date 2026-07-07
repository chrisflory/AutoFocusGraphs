using ScottPlot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AutofocusGraphs {
    /// <summary>
    /// Builds a dark-mode autofocus V-curve PNG (replaces the matplotlib Discord bot graph).
    /// </summary>
    internal static class AutofocusGraphGenerator {
        public static byte[] CreatePng(
            AutofocusReport report,
            bool showHyperbolicFit = true,
            bool showParabolicFit = true,
            bool showTrendLines = true,
            bool showFocusPositionLine = true,
            bool showFilterInTitle = true,
            bool labelTrendSegments = true,
            bool minimalMode = false,
            bool showMeasurePointLabels = true,
            bool showGraphContextStrip = true,
            bool showPreviousFocusMarker = true,
            bool showTrendR2InLegend = true,
            bool showInitialFocusMarker = true,
            bool showMeasurePointErrorBars = false,
            bool showFitDisagreementWarning = true,
            bool showGraphAnalysisHints = true,
            bool conservativeGraphHints = true,
            double hintMinR2 = 0.90,
            double hintMaxFinalHfr = 3.0,
            int pixelWidth = 1200,
            int pixelHeight = 720) {
            var positions = report.MeasurePoints.Select(p => p.Position).ToArray();
            var values = report.MeasurePoints.Select(p => p.Value).ToArray();
            var errors = report.MeasurePoints.Select(p => p.Error).ToArray();

            // Split left/right at the measure point nearest the calculated focus (not merely nearest HFR).
            var focusX = report.CalculatedPosition ?? positions[IndexNearestValue(values, report.FinalHfr)];
            var splitIdx = IndexNearestValue(positions, focusX);

            var plot = new Plot();
            plot.FigureBackground.Color = Color.FromHex("#36393F");
            plot.DataBackground.Color = Color.FromHex("#36393F");
            var previousFocusFallback = ReportStore.Instance.GetPreviousCalculatedFocusPosition(report.FileName);

            if (!minimalMode) {
                var curve = plot.Add.Scatter(positions, values);
                curve.Color = Color.FromHex("#36A2EB");
                curve.LineWidth = 4;
                curve.MarkerSize = 0;
                curve.LegendText = "Focus Curve";
            }

            var points = plot.Add.Scatter(positions, values);
            points.Color = Color.FromHex("#FF6384");
            points.LineWidth = 0;
            points.MarkerSize = minimalMode ? 12 : 10;
            points.LegendText = "Measure Points";

            if (showGraphAnalysisHints) {
                foreach (var idx in report.GetOutlierPointIndices()) {
                    if (idx < 0 || idx >= positions.Length) {
                        continue;
                    }

                    var outlierMark = plot.Add.Text("×", positions[idx], values[idx]);
                    outlierMark.LabelFontColor = Color.FromHex("#FF4444");
                    outlierMark.LabelFontSize = 20;
                    outlierMark.LabelBold = true;
                    outlierMark.LabelAlignment = Alignment.MiddleCenter;
                }
            }

            if (showMeasurePointErrorBars && errors.Any(e => e > 0)) {
                var errorBars = plot.Add.ErrorBar(positions, values, errors);
                errorBars.Color = Color.FromHex("#FF6384");
                errorBars.LineWidth = 1.5f;
                errorBars.LegendText = "HFR uncertainty";
            }

            // HFR value above each measure point (same style as the final-HFR label).
            if (showMeasurePointLabels) {
                for (var i = 0; i < positions.Length; i++) {
                    var pointLabel = plot.Add.Text(
                        values[i].ToString("0.00", CultureInfo.InvariantCulture),
                        positions[i],
                        values[i]);
                    pointLabel.LabelFontColor = Color.FromHex("#FFB3C1");
                    pointLabel.LabelFontSize = 12;
                    pointLabel.LabelBold = true;
                    // Below the point: sits inside the V and avoids the yellow final-HFR label.
                    pointLabel.LabelAlignment = Alignment.UpperCenter;
                    pointLabel.OffsetY = 10;
                }
            }

            // Left/right linear trends (NINA-style), meeting the focus position on X.
            // Different colors so the two sides are obvious even when they nearly meet.
            if (!minimalMode && showTrendLines) {
                if (splitIdx >= 1) {
                    AddTrend(
                        plot,
                        positions,
                        values,
                        0,
                        splitIdx,
                        focusX,
                        extendToFocusOnRight: true,
                        color: Color.FromHex("#F39C12"),
                        legendText: FormatTrendLegend(
                            labelTrendSegments ? "Left Trend" : "Trend",
                            showTrendR2InLegend ? report.R2Left : null));
                }
                if (splitIdx < positions.Length - 1) {
                    AddTrend(
                        plot,
                        positions,
                        values,
                        splitIdx,
                        positions.Length - 1,
                        focusX,
                        extendToFocusOnRight: false,
                        color: Color.FromHex("#E67E22"),
                        legendText: FormatTrendLegend(
                            labelTrendSegments
                                ? "Right Trend"
                                : (splitIdx >= 1 ? string.Empty : "Trend"),
                            showTrendR2InLegend ? report.R2Right : null));
                }
            }

            if (!minimalMode && showHyperbolicFit && report.HasHyperbolicFit) {
                TryAddHyperbolicFit(plot, report, positions, values);
            }
            if (!minimalMode && showParabolicFit && report.HasParabolicFit) {
                TryAddParabolicFit(plot, report, positions, values);
            }

            if (!minimalMode && showPreviousFocusMarker) {
                var prevPos = report.ResolvePreviousFocusPosition(previousFocusFallback);
                if (prevPos.HasValue) {
                    var calcPos = report.CalculatedPosition;
                    var showPreviousLine = !calcPos.HasValue || Math.Abs(prevPos.Value - calcPos.Value) >= 0.5;
                    if (showPreviousLine) {
                        var prevLine = plot.Add.VerticalLine(prevPos.Value);
                        prevLine.Color = Color.FromHex("#95A5A6");
                        prevLine.LineWidth = 1.5f;
                        prevLine.LinePattern = LinePattern.Dotted;
                        prevLine.LegendText = "Last AF";
                    }
                }
            }

            // Final HFR sits on the focus-position line when NINA reports a calculated position.
            var finalX = focusX;
            var finalY = report.FinalHfr;
            var final = plot.Add.Scatter(
                new[] { finalX },
                new[] { finalY });
            final.Color = Colors.Yellow;
            final.MarkerSize = 16;
            final.MarkerShape = MarkerShape.FilledCircle;
            final.LineWidth = 0;
            final.LegendText = "Final HFR";

            var hfrLabel = plot.Add.Text($"HFR {report.FormatFinalHfr()}", finalX, finalY);
            hfrLabel.LabelFontColor = Colors.Yellow;
            hfrLabel.LabelFontSize = 16;
            hfrLabel.LabelBold = true;
            hfrLabel.LabelAlignment = Alignment.LowerCenter;
            hfrLabel.OffsetY = -14;

            plot.Axes.AutoScale();
            ExpandAxisLimitsForPoint(
                plot,
                report.InitialFocusPosition,
                report.InitialFocusValue);
            ExpandAxisLimitsForPoint(
                plot,
                report.ResolvePreviousFocusPosition(previousFocusFallback),
                report.PreviousFocusValue);

            plot.Axes.Bottom.Label.Text = "Focuser Position";
            plot.Axes.Left.Label.Text = "HFR";
            var title = "N.I.N.A. Autofocus Run";
            if (showFilterInTitle && !string.IsNullOrWhiteSpace(report.Filter) && report.Filter != "N/A") {
                title += $" — {report.Filter}";
            }
            plot.Axes.Title.Label.Text = title;

            StyleAxis(plot.Axes.Bottom);
            StyleAxis(plot.Axes.Left);
            plot.Axes.Title.Label.ForeColor = Colors.White;
            plot.Axes.Title.Label.FontSize = 18;

            plot.ShowLegend(Edge.Bottom);
            plot.Legend.Orientation = Orientation.Horizontal;
            plot.Legend.FontColor = Colors.White;
            plot.Legend.FontSize = 14;
            plot.Legend.BackgroundColor = Color.FromHex("#36393F");
            plot.Legend.OutlineColor = Color.FromHex("#555555");
            plot.Legend.ShadowColor = Colors.Transparent;

            plot.Grid.MajorLineColor = Color.FromHex("#666666");
            plot.Grid.MinorLineColor = Color.FromHex("#444444");

            if (showGraphContextStrip || showFitDisagreementWarning || showPreviousFocusMarker) {
                plot.Axes.AutoScale();
                AddGraphAnnotations(
                    plot,
                    report,
                    previousFocusFallback,
                    showGraphContextStrip,
                    showFitDisagreementWarning,
                    showPreviousFocusMarker);
            }

            if (showGraphAnalysisHints) {
                plot.Axes.AutoScale();
                AddAnalysisHints(
                    plot,
                    report,
                    previousFocusFallback,
                    showHyperbolicFit,
                    showParabolicFit,
                    conservativeGraphHints,
                    hintMinR2,
                    hintMaxFinalHfr);
            }

            if (!minimalMode) {
                AddFocusAndStartMarkers(
                    plot,
                    report,
                    focusX,
                    showFocusPositionLine,
                    showInitialFocusMarker);
            }

            return plot.GetImageBytes(pixelWidth, pixelHeight, ImageFormat.Png);
        }

        private static void AddFocusAndStartMarkers(
            Plot plot,
            AutofocusReport report,
            double focusX,
            bool showFocusLine,
            bool showStartMarker) {
            var hasFocusLine = showFocusLine && report.CalculatedPosition.HasValue;
            var hasStartMarker = showStartMarker
                && report.InitialFocusPosition.HasValue
                && report.InitialFocusValue.HasValue;
            if (!hasFocusLine && !hasStartMarker) {
                return;
            }

            var startX = hasStartMarker ? report.InitialFocusPosition.Value : focusX;
            var delta = Math.Abs(startX - focusX);
            var startLegend = delta < 0.5 ? "Start (≈ focus)" : "Start pos";
            const string startColor = "#00D4FF";

            if (hasFocusLine) {
                var focusLine = plot.Add.VerticalLine(focusX);
                focusLine.Color = Color.FromHex("#FFFFFF");
                focusLine.LineWidth = 2;
                focusLine.LinePattern = LinePattern.Dashed;
                focusLine.LegendText = "Focus Pos";
            }

            if (!hasStartMarker) {
                return;
            }

            var startLine = plot.Add.VerticalLine(startX);
            startLine.Color = Color.FromHex(startColor);
            startLine.LineWidth = 3;
            startLine.LinePattern = delta < 0.5 ? LinePattern.Solid : LinePattern.Dotted;
            startLine.LegendText = startLegend;

            var startY = ResolveInitialFocusY(report);
            var start = plot.Add.Scatter(new[] { startX }, new[] { startY });
            start.Color = Color.FromHex(startColor);
            start.MarkerSize = 18;
            start.MarkerShape = MarkerShape.FilledDiamond;
            start.LineWidth = 0;
            start.MarkerLineWidth = 2;
            start.MarkerLineColor = Colors.White;
            start.LegendText = "Start";

            if (delta >= 0.5) {
                var limits = plot.Axes.GetLimits();
                var labelY = limits.Top - (limits.Top - limits.Bottom) * 0.04;
                var startLabel = plot.Add.Text(
                    startX.ToString("0", CultureInfo.InvariantCulture),
                    startX,
                    labelY);
                startLabel.LabelFontColor = Color.FromHex(startColor);
                startLabel.LabelFontSize = 11;
                startLabel.LabelBold = true;
                startLabel.LabelAlignment = Alignment.LowerCenter;
            }
        }

        private static double ResolveInitialFocusY(AutofocusReport report) {
            var y = report.InitialFocusValue.Value;
            var x = report.InitialFocusPosition.Value;
            if (report.MeasurePoints == null || report.MeasurePoints.Count == 0) {
                return y;
            }

            var values = report.MeasurePoints.Select(p => p.Value).ToList();
            var min = values.Min();
            var max = values.Max();
            if (y >= min * 0.85 && y <= max * 1.35) {
                return y;
            }

            var nearest = report.MeasurePoints
                .OrderBy(p => Math.Abs(p.Position - x))
                .First();
            return nearest.Value;
        }

        private static void ExpandAxisLimitsForPoint(Plot plot, double? x, double? y) {
            if (!x.HasValue || !y.HasValue) {
                return;
            }

            var limits = plot.Axes.GetLimits();
            var left = Math.Min(limits.Left, x.Value);
            var right = Math.Max(limits.Right, x.Value);
            var bottom = Math.Min(limits.Bottom, y.Value);
            var top = Math.Max(limits.Top, y.Value);
            if (Math.Abs(left - limits.Left) < 0.01 &&
                Math.Abs(right - limits.Right) < 0.01 &&
                Math.Abs(bottom - limits.Bottom) < 0.01 &&
                Math.Abs(top - limits.Top) < 0.01) {
                return;
            }

            plot.Axes.SetLimits(left, right, bottom, top);
        }

        private static string FormatTrendLegend(string label, double? r2) {
            if (string.IsNullOrEmpty(label)) {
                return string.Empty;
            }
            if (!r2.HasValue) {
                return label;
            }
            return $"{label} (R² {r2.Value.ToString("0.00", CultureInfo.InvariantCulture)})";
        }

        private static void AddGraphAnnotations(
            Plot plot,
            AutofocusReport report,
            double? previousFocusFallback,
            bool showContextStrip,
            bool showFitDisagreement,
            bool showPreviousFocusMarker) {
            var lines = new List<string>();
            if (showContextStrip) {
                var context = BuildContextStripLine(report);
                if (!string.IsNullOrEmpty(context)) {
                    lines.Add(context);
                }
            }

            if (showPreviousFocusMarker) {
                var delta = report.FocusDeltaFromPrevious(previousFocusFallback);
                if (delta.HasValue) {
                    if (delta.Value == 0) {
                        lines.Add("Δ focus 0 steps");
                    } else {
                        var sign = delta.Value > 0 ? "+" : string.Empty;
                        lines.Add($"Δ focus {sign}{delta.Value} steps");
                    }
                }
            }

            if (showFitDisagreement) {
                var fitDelta = report.HyperbolicFitDeltaSteps();
                var threshold = Math.Max(report.StepSize ?? 2, 2);
                if (fitDelta.HasValue && Math.Abs(fitDelta.Value) >= threshold) {
                    var sign = fitDelta.Value > 0 ? "+" : string.Empty;
                    lines.Add($"Fit Δ {sign}{fitDelta.Value} steps");
                }
            }

            if (lines.Count == 0) {
                return;
            }

            var limits = plot.Axes.GetLimits();
            var annotation = plot.Add.Text(string.Join("\n", lines), limits.Right, limits.Top);
            annotation.LabelFontColor = Color.FromHex("#DCDDDE");
            annotation.LabelFontSize = 13;
            annotation.LabelBold = false;
            annotation.LabelAlignment = Alignment.UpperRight;
            annotation.OffsetX = -12;
            annotation.OffsetY = 12;
        }

        private static void AddAnalysisHints(
            Plot plot,
            AutofocusReport report,
            double? previousFocusFallback,
            bool showHyperbolicFit,
            bool showParabolicFit,
            bool conservativeGraphHints,
            double hintMinR2,
            double hintMaxFinalHfr) {
            var analysis = AutofocusGraphAnalyzer.Analyze(
                report,
                new AutofocusGraphAnalyzer.Options {
                    MinR2 = hintMinR2,
                    MaxFinalHfr = hintMaxFinalHfr,
                    PreviousFocusFallback = previousFocusFallback,
                    ConservativeHints = conservativeGraphHints,
                    ShowHyperbolicFitOnGraph = showHyperbolicFit,
                    ShowParabolicFitOnGraph = showParabolicFit,
                    LogFiredRules = true
                });
            var hints = analysis.Hints;
            if (hints.Count == 0) {
                return;
            }

            var lines = new List<string> { "Observations only — not a diagnosis." };
            lines.AddRange(hints.Select(h => "• " + h));
            var limits = plot.Axes.GetLimits();
            var annotation = plot.Add.Text(string.Join("\n", lines), limits.Left, limits.Bottom);
            annotation.LabelFontColor = Color.FromHex("#FAA61A");
            annotation.LabelFontSize = 12;
            annotation.LabelBold = false;
            annotation.LabelAlignment = Alignment.LowerLeft;
            annotation.OffsetX = 14;
            annotation.OffsetY = -14;
        }

        private static string BuildContextStripLine(AutofocusReport report) {
            var parts = new List<string>();
            if (report.Temperature.HasValue) {
                parts.Add($"T {report.FormatTemperature()}°C");
            }
            if (report.StepSize.HasValue) {
                parts.Add($"Step {report.StepSize.Value}");
            }
            var duration = report.FormatGraphDuration();
            if (!string.IsNullOrWhiteSpace(duration) && duration != "N/A") {
                parts.Add(duration);
            }
            return parts.Count == 0 ? string.Empty : string.Join(" · ", parts);
        }

        /// <summary>
        /// Lightweight trend of final HFR across recent runs (integer X ticks, HFR labels).
        /// </summary>
        public static byte[] CreateTrendPng(IReadOnlyList<AutofocusReport> reports, int maxRuns = 20) {
            if (reports == null || reports.Count == 0) {
                throw new InvalidOperationException("No reports available for trend chart.");
            }

            // Stable order matching the digest list; chart only the most recent N runs.
            var ordered = reports
                .OrderBy(r => r.FormattedTimestamp, StringComparer.Ordinal)
                .ThenBy(r => r.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (maxRuns > 0 && ordered.Count > maxRuns) {
                ordered = ordered.Skip(ordered.Count - maxRuns).ToList();
            }

            var count = ordered.Count;
            var xs = new double[count];
            var hfr = new double[count];
            for (var i = 0; i < count; i++) {
                xs[i] = i + 1;
                hfr[i] = ordered[i].FinalHfr;
            }

            var plot = new Plot();
            plot.FigureBackground.Color = Color.FromHex("#36393F");
            plot.DataBackground.Color = Color.FromHex("#36393F");

            var hfrSeries = plot.Add.Scatter(xs, hfr);
            hfrSeries.Color = Color.FromHex("#36A2EB");
            hfrSeries.LineWidth = 3;
            hfrSeries.MarkerSize = 12;
            hfrSeries.LegendText = "Final HFR";

            for (var i = 0; i < count; i++) {
                var label = plot.Add.Text(ordered[i].FormatFinalHfr(), xs[i], hfr[i]);
                label.LabelFontColor = Colors.White;
                label.LabelFontSize = 14;
                label.LabelBold = true;
                label.LabelAlignment = Alignment.LowerCenter;
                label.OffsetY = -12;
            }

            // Integer run numbers only — avoid ScottPlot's dense fractional ticks.
            var tickPositions = xs.ToArray();
            var tickLabels = xs.Select(x => ((int)x).ToString()).ToArray();
            plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(tickPositions, tickLabels);
            plot.Axes.SetLimitsX(0.5, count + 0.5);

            var minHfr = hfr.Min();
            var maxHfr = hfr.Max();
            var pad = Math.Max(0.2, (maxHfr - minHfr) * 0.2);
            plot.Axes.SetLimitsY(Math.Max(0, minHfr - pad), maxHfr + pad + 0.3);

            plot.Axes.Bottom.Label.Text = "Run # (oldest → newest)";
            plot.Axes.Left.Label.Text = "Final HFR";
            plot.Axes.Title.Label.Text = "Autofocus trend";
            StyleAxis(plot.Axes.Bottom);
            StyleAxis(plot.Axes.Left);
            plot.Axes.Title.Label.ForeColor = Colors.White;
            plot.Axes.Title.Label.FontSize = 18;
            plot.ShowLegend(Edge.Bottom);
            plot.Legend.Orientation = Orientation.Horizontal;
            plot.Legend.FontColor = Colors.White;
            plot.Legend.BackgroundColor = Color.FromHex("#36393F");
            plot.Legend.OutlineColor = Color.FromHex("#555555");
            plot.Legend.ShadowColor = Colors.Transparent;
            plot.Grid.MajorLineColor = Color.FromHex("#555555");

            return plot.GetImageBytes(1200, 640, ImageFormat.Png);
        }

        /// <summary>
        /// Hyperbolic model: y = a * sqrt((x - h)^2 + r^2) + k, with h fixed at the reported minimum.
        /// </summary>
        private static void TryAddHyperbolicFit(Plot plot, AutofocusReport report, double[] positions, double[] values) {
            if (positions.Length < 3) {
                return;
            }

            var h = report.HyperbolicMinimumPosition
                ?? report.CalculatedPosition
                ?? positions[Array.IndexOf(values, values.Min())];

            var minPos = positions.Min();
            var maxPos = positions.Max();
            var span = Math.Max(1.0, maxPos - minPos);

            double bestA = 0, bestR = span * 0.2, bestK = report.FinalHfr;
            var bestError = double.MaxValue;

            // Grid-search r, then solve a,k with linear least squares for fixed h,r.
            for (var step = 1; step <= 40; step++) {
                var r = span * (0.05 + step * 0.05);
                if (!TryFitHyperbolicLinear(positions, values, h, r, out var a, out var k, out var error)) {
                    continue;
                }
                if (error < bestError) {
                    bestError = error;
                    bestA = a;
                    bestR = r;
                    bestK = k;
                }
            }

            if (bestError == double.MaxValue || bestA <= 0) {
                return;
            }

            var sampleCount = 80;
            var xs = new double[sampleCount];
            var ys = new double[sampleCount];
            for (var i = 0; i < sampleCount; i++) {
                var x = minPos + (maxPos - minPos) * i / (sampleCount - 1);
                xs[i] = x;
                ys[i] = bestA * Math.Sqrt((x - h) * (x - h) + bestR * bestR) + bestK;
            }

            var series = plot.Add.Scatter(xs, ys);
            series.Color = Color.FromHex("#2ECC71");
            series.LineWidth = 2.5f;
            series.MarkerSize = 0;
            series.LegendText = report.R2Hyperbolic.HasValue
                ? $"Hyperbolic (R² {report.FormatR2(report.R2Hyperbolic)})"
                : "Hyperbolic";
        }

        private static bool TryFitHyperbolicLinear(
            double[] positions,
            double[] values,
            double h,
            double r,
            out double a,
            out double k,
            out double error) {
            a = 0;
            k = 0;
            error = double.MaxValue;

            var n = positions.Length;
            double sumS = 0, sumY = 0, sumSS = 0, sumSY = 0;
            var s = new double[n];
            for (var i = 0; i < n; i++) {
                s[i] = Math.Sqrt((positions[i] - h) * (positions[i] - h) + r * r);
                sumS += s[i];
                sumY += values[i];
                sumSS += s[i] * s[i];
                sumSY += s[i] * values[i];
            }

            var denom = n * sumSS - sumS * sumS;
            if (Math.Abs(denom) < 1e-9) {
                return false;
            }

            a = (n * sumSY - sumS * sumY) / denom;
            k = (sumY - a * sumS) / n;
            if (a <= 0) {
                return false;
            }

            error = 0;
            for (var i = 0; i < n; i++) {
                var pred = a * s[i] + k;
                var d = values[i] - pred;
                error += d * d;
            }
            return true;
        }

        /// <summary>
        /// Parabolic (quadratic) model: y = a*x^2 + b*x + c via least squares.
        /// Matches NINA's "Parabolic" curve-fitting option (JSON field name is Quadratic).
        /// </summary>
        private static void TryAddParabolicFit(Plot plot, AutofocusReport report, double[] positions, double[] values) {
            if (positions.Length < 3) {
                return;
            }

            // Normal equations for [a, b, c]
            double s0 = positions.Length;
            double s1 = 0, s2 = 0, s3 = 0, s4 = 0;
            double t0 = 0, t1 = 0, t2 = 0;
            for (var i = 0; i < positions.Length; i++) {
                var x = positions[i];
                var y = values[i];
                var x2 = x * x;
                s1 += x;
                s2 += x2;
                s3 += x2 * x;
                s4 += x2 * x2;
                t0 += y;
                t1 += x * y;
                t2 += x2 * y;
            }

            // Solve 3x3 system
            if (!Solve3x3(
                    s4, s3, s2, t2,
                    s3, s2, s1, t1,
                    s2, s1, s0, t0,
                    out var a, out var b, out var c)) {
                return;
            }

            // Require a upward-opening parabola (autofocus V-curve).
            if (a <= 0) {
                return;
            }

            var minPos = positions.Min();
            var maxPos = positions.Max();
            var sampleCount = 80;
            var xs = new double[sampleCount];
            var ys = new double[sampleCount];
            for (var i = 0; i < sampleCount; i++) {
                var x = minPos + (maxPos - minPos) * i / (sampleCount - 1);
                xs[i] = x;
                ys[i] = a * x * x + b * x + c;
            }

            var series = plot.Add.Scatter(xs, ys);
            series.Color = Color.FromHex("#9B59B6");
            series.LineWidth = 2.5f;
            series.LinePattern = LinePattern.Dotted;
            series.MarkerSize = 0;
            series.LegendText = report.R2Parabolic.HasValue
                ? $"Parabolic (R² {report.FormatR2(report.R2Parabolic)})"
                : "Parabolic";
        }

        private static bool Solve3x3(
            double a11, double a12, double a13, double b1,
            double a21, double a22, double a23, double b2,
            double a31, double a32, double a33, double b3,
            out double x1, out double x2, out double x3) {
            x1 = x2 = x3 = 0;
            var det =
                a11 * (a22 * a33 - a23 * a32) -
                a12 * (a21 * a33 - a23 * a31) +
                a13 * (a21 * a32 - a22 * a31);
            if (Math.Abs(det) < 1e-12) {
                return false;
            }

            x1 = (
                b1 * (a22 * a33 - a23 * a32) -
                a12 * (b2 * a33 - a23 * b3) +
                a13 * (b2 * a32 - a22 * b3)) / det;
            x2 = (
                a11 * (b2 * a33 - a23 * b3) -
                b1 * (a21 * a33 - a23 * a31) +
                a13 * (a21 * b3 - b2 * a31)) / det;
            x3 = (
                a11 * (a22 * b3 - b2 * a32) -
                a12 * (a21 * b3 - b2 * a31) +
                b1 * (a21 * a32 - a22 * a31)) / det;
            return true;
        }

        private static int IndexNearestValue(double[] values, double target) {
            var bestIdx = 0;
            var bestDelta = Math.Abs(values[0] - target);
            for (var i = 1; i < values.Length; i++) {
                var delta = Math.Abs(values[i] - target);
                if (delta < bestDelta) {
                    bestDelta = delta;
                    bestIdx = i;
                }
            }
            return bestIdx;
        }

        private static void AddTrend(
            Plot plot,
            double[] positions,
            double[] values,
            int start,
            int end,
            double focusX,
            bool extendToFocusOnRight,
            Color color,
            string legendText) {
            var count = end - start + 1;
            if (count < 2) {
                return;
            }

            var xs = new double[count];
            var ys = new double[count];
            Array.Copy(positions, start, xs, 0, count);
            Array.Copy(values, start, ys, 0, count);

            double sumX = 0, sumY = 0, sumXy = 0, sumXx = 0;
            for (var i = 0; i < count; i++) {
                sumX += xs[i];
                sumY += ys[i];
                sumXy += xs[i] * ys[i];
                sumXx += xs[i] * xs[i];
            }
            var denom = count * sumXx - sumX * sumX;
            if (Math.Abs(denom) < 1e-9) {
                return;
            }
            var slope = (count * sumXy - sumX * sumY) / denom;
            var intercept = (sumY - slope * sumX) / count;

            // Anchor each side on the focus-position X so trends meet the white line.
            double x0, x1;
            if (extendToFocusOnRight) {
                x0 = xs[0];
                x1 = focusX;
            } else {
                x0 = focusX;
                x1 = xs[count - 1];
            }

            var lineXs = new[] { x0, x1 };
            var lineYs = new[] { intercept + slope * x0, intercept + slope * x1 };
            var trend = plot.Add.Scatter(lineXs, lineYs);
            trend.Color = color;
            trend.LineWidth = 2.5f;
            trend.LinePattern = LinePattern.Dashed;
            trend.MarkerSize = 0;
            trend.LegendText = legendText ?? string.Empty;
        }

        private static void StyleAxis(ScottPlot.IYAxis axis) {
            axis.Label.ForeColor = Colors.White;
            axis.TickLabelStyle.ForeColor = Colors.White;
            axis.FrameLineStyle.Color = Colors.White;
            axis.MajorTickStyle.Color = Colors.White;
            axis.MinorTickStyle.Color = Colors.Gray;
        }

        private static void StyleAxis(ScottPlot.IXAxis axis) {
            axis.Label.ForeColor = Colors.White;
            axis.TickLabelStyle.ForeColor = Colors.White;
            axis.FrameLineStyle.Color = Colors.White;
            axis.MajorTickStyle.Color = Colors.White;
            axis.MinorTickStyle.Color = Colors.Gray;
        }
    }
}
