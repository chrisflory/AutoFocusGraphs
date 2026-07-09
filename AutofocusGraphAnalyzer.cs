using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AutoFocusGraphs {
    /// <summary>
    /// Rule-based autofocus V-curve observations for graph overlay (not diagnoses).
    /// </summary>
    internal static class AutofocusGraphAnalyzer {
        private const int MaxHints = 3;
        private const string WingShapeCluster = "wing-shape";

        internal enum HintTier {
            Fact,
            Pattern,
            Suggestion
        }

        internal sealed class Options {
            public double MinR2 { get; init; } = 0.90;
            public double MaxFinalHfr { get; init; } = 3.0;
            public double? PreviousFocusFallback { get; init; }
            public bool ConservativeHints { get; init; } = true;
            public bool ShowHyperbolicFitOnGraph { get; init; } = true;
            public bool ShowParabolicFitOnGraph { get; init; } = true;
            public bool LogFiredRules { get; init; } = true;
        }

        internal sealed class Result {
            public IReadOnlyList<string> Hints { get; init; } = Array.Empty<string>();
            public IReadOnlyList<string> RuleIds { get; init; } = Array.Empty<string>();
        }

        private sealed class Hint {
            public string RuleId { get; init; }
            public string Cluster { get; init; }
            public int Priority { get; init; }
            public HintTier Tier { get; init; }
            public string Text { get; init; }
        }

        public static Result Analyze(AutofocusReport report, Options options = null) {
            options ??= new Options();
            if (report?.MeasurePoints == null || report.MeasurePoints.Count < 3) {
                return new Result();
            }

            var hints = new List<Hint>();
            AddSettingHints(report, hints);
            AddShapeHints(report, hints);
            AddFitStatisticsHints(report, options, hints);
            AddOutlierHints(report, hints);
            AddStepSizeHints(report, hints);
            AddFitQualityHints(report, options, hints);
            AddHfrHints(report, options, hints);
            AddFocusShiftHints(report, options, hints);
            AddCombinedShapeSettingHints(report, hints);

            var selected = SelectHints(hints, options, out var suppressed);
            LogResults(report, selected, suppressed, options);

            return new Result {
                Hints = selected.Select(h => h.Text).ToList(),
                RuleIds = selected.Select(h => h.RuleId).ToList()
            };
        }

        public static IReadOnlyList<string> GetHints(AutofocusReport report, Options options = null) =>
            Analyze(report, options).Hints;

        private static List<Hint> SelectHints(List<Hint> hints, Options options, out List<Hint> suppressed) {
            suppressed = new List<Hint>();

            var wingShape = hints
                .Where(h => h.Cluster == WingShapeCluster)
                .OrderBy(h => h.Priority)
                .ThenBy(h => h.RuleId, StringComparer.Ordinal)
                .ToList();
            if (wingShape.Count > 1) {
                suppressed.AddRange(wingShape.Skip(1));
                foreach (var drop in wingShape.Skip(1)) {
                    hints.Remove(drop);
                }
            }

            var eligible = hints
                .Where(h => options.ConservativeHints
                    ? h.Tier != HintTier.Suggestion
                    : true)
                .OrderBy(h => h.Priority)
                .ThenBy(h => h.RuleId, StringComparer.Ordinal)
                .ToList();

            if (options.ConservativeHints) {
                suppressed.AddRange(hints.Where(h => h.Tier == HintTier.Suggestion));
            }

            var selected = eligible.Take(MaxHints).ToList();
            var selectedSet = new HashSet<Hint>(selected);
            suppressed.AddRange(eligible.Where(h => !selectedSet.Contains(h)));
            return selected;
        }

        private static void LogResults(
            AutofocusReport report,
            IReadOnlyList<Hint> selected,
            IReadOnlyList<Hint> suppressed,
            Options options) {
            if (!options.LogFiredRules) {
                return;
            }

            var fileLabel = string.IsNullOrWhiteSpace(report.FileName) ? "report" : report.FileName;
            if (selected.Count > 0) {
                var ruleList = string.Join(", ", selected.Select(h => h.RuleId));
                var hintList = string.Join(" | ", selected.Select(h => h.Text));
                Logger.Info($"AutoFocusGraphs: graph analysis [{fileLabel}] rules: {ruleList}");
                Logger.Info($"AutoFocusGraphs: graph analysis [{fileLabel}] hints: {hintList}");
            }

            if (suppressed.Count > 0) {
                var suppressedRules = string.Join(", ", suppressed.Select(h => h.RuleId).Distinct());
                Logger.Info($"AutoFocusGraphs: graph analysis [{fileLabel}] suppressed: {suppressedRules}");
            }
        }

        private static void AddFitStatisticsHints(AutofocusReport report, Options options, List<Hint> hints) {
            var fitLabel = report.PrimaryFitLabel(
                options.ShowParabolicFitOnGraph,
                options.ShowHyperbolicFitOnGraph);
            if (string.Equals(fitLabel, "Parabolic", StringComparison.Ordinal)) {
                return;
            }

            if (report.HyperbolicMinimumStdError is { } sigma && sigma > 0) {
                hints.Add(new Hint {
                    RuleId = "fit-sigma-focus",
                    Priority = 16,
                    Tier = HintTier.Fact,
                    Text = $"σ(focus) ± {sigma.ToString("0.#", CultureInfo.InvariantCulture)} steps"
                });
            }

            if (report.HyperbolicLeaveOneOutStdError is { } loo && loo > 0) {
                hints.Add(new Hint {
                    RuleId = "fit-loo-stability",
                    Priority = 17,
                    Tier = HintTier.Fact,
                    Text = $"LOO focus stability ± {loo.ToString("0.#", CultureInfo.InvariantCulture)} steps"
                });

                if (report.HyperbolicMinimumStdError is { } sigma2 && loo > sigma2 * 1.25) {
                    hints.Add(new Hint {
                        RuleId = "fit-loo-exceeds-sigma",
                        Priority = 19,
                        Tier = HintTier.Pattern,
                        Text = "LOO uncertainty exceeds fit σ — curve sensitive to individual points"
                    });
                }
            }
        }

        private static void AddOutlierHints(AutofocusReport report, List<Hint> hints) {
            var outliers = report.GetOutlierPointIndices();
            if (outliers.Count == 0) {
                return;
            }

            if (outliers.Count == 1) {
                var point = report.MeasurePoints[outliers[0]];
                var fromJson = point.IsOutlier == true;
                hints.Add(new Hint {
                    RuleId = fromJson ? "outlier-point-json" : "outlier-point-residual",
                    Priority = 13,
                    Tier = HintTier.Pattern,
                    Text = fromJson
                        ? $"Report marks pos {(int)point.Position} (HFR {point.Value.ToString("0.00", CultureInfo.InvariantCulture)}) as outlier"
                        : $"Pos {(int)point.Position} (HFR {point.Value.ToString("0.00", CultureInfo.InvariantCulture)}) high vs wing trend"
                });
                return;
            }

            hints.Add(new Hint {
                RuleId = "outlier-points-multiple",
                Priority = 13,
                Tier = HintTier.Pattern,
                Text = $"{outliers.Count} measure points flagged as outliers"
            });
        }

        private static void AddSettingHints(AutofocusReport report, List<Hint> hints) {
            if (!IsOvershootModel(report)) {
                return;
            }

            var backlashIn = ParseBacklashSteps(report.BacklashIn);
            var backlashOut = ParseBacklashSteps(report.BacklashOut);
            var inActive = backlashIn is > 0;
            var outActive = backlashOut is > 0;

            if (inActive && outActive) {
                hints.Add(new Hint {
                    RuleId = "overshoot-both-directions",
                    Priority = 10,
                    Tier = HintTier.Fact,
                    Text = "Overshoot enabled on IN and OUT (NINA allows one direction only)"
                });
            }
        }

        private static void AddShapeHints(AutofocusReport report, List<Hint> hints) {
            if (TryDetectPostMinimumPlateau(report, out var plateauSide, out var lastDy, out var lastDx)) {
                hints.Add(new Hint {
                    RuleId = "post-minimum-plateau",
                    Cluster = WingShapeCluster,
                    Priority = 12,
                    Tier = HintTier.Pattern,
                    Text = $"{plateauSide} wing: HFR rise slows on last step (+{lastDy.ToString("0.0", CultureInfo.InvariantCulture)} over {lastDx.ToString("0", CultureInfo.InvariantCulture)} pos)"
                });
            }

            if (TryDetectApproachPlateau(report, out var approachSide, out var approachFlatSteps)) {
                hints.Add(new Hint {
                    RuleId = "outer-wing-plateau",
                    Cluster = WingShapeCluster,
                    Priority = 11,
                    Tier = HintTier.Pattern,
                    Text =
                        $"{approachSide} wing: nearly flat for {approachFlatSteps} step(s) at scan edge before steep slope — backlash or overshoot on approach"
                });
            }

            if (TryDetectZigzag(report, out var zigzagChanges)) {
                hints.Add(new Hint {
                    RuleId = "zigzag-wing",
                    Priority = 16,
                    Tier = HintTier.Pattern,
                    Text =
                        $"HFR reverses direction {zigzagChanges} time(s) along scan — step size vs seeing, guiding, or backlash"
                });
            }

            if (TryDetectOuterMeasurementCliff(report, out var cliffSide)) {
                hints.Add(new Hint {
                    RuleId = "outer-measurement-cliff",
                    Priority = 17,
                    Tier = HintTier.Pattern,
                    Text =
                        $"{cliffSide} scan edge: HFR drops sharply on outermost point — stars may be too defocused to measure"
                });
            }

            if (TryGetWingAsymmetry(report, out var asymmetry)) {
                var pct = (int)Math.Round(asymmetry * 100);
                if (pct >= 35) {
                    hints.Add(new Hint {
                        RuleId = "asymmetric-v-curve",
                        Cluster = WingShapeCluster,
                        Priority = 25,
                        Tier = HintTier.Pattern,
                        Text = $"Wing HFR averages differ ~{pct}% left vs right"
                    });
                }
            }
        }

        private static void AddCombinedShapeSettingHints(AutofocusReport report, List<Hint> hints) {
            if (!IsOvershootModel(report)) {
                return;
            }

            var hasShape = hints.Any(h =>
                h.Cluster == WingShapeCluster &&
                (h.RuleId == "post-minimum-plateau" || h.RuleId == "asymmetric-v-curve"));
            if (!hasShape) {
                return;
            }

            var backlashIn = ParseBacklashSteps(report.BacklashIn) ?? 0;
            var backlashOut = ParseBacklashSteps(report.BacklashOut) ?? 0;
            if (backlashIn <= 0 && backlashOut <= 0) {
                return;
            }

            string compText;
            if (backlashIn > 0 && backlashOut > 0) {
                compText = $"IN {backlashIn} / OUT {backlashOut}";
            } else if (backlashOut > 0) {
                compText = $"OUT {backlashOut}";
            } else {
                compText = $"IN {backlashIn}";
            }

            hints.Add(new Hint {
                RuleId = "shape-with-overshoot-settings",
                Priority = 14,
                Tier = HintTier.Suggestion,
                Text = $"Wing shape + overshoot {compText} — comp may be worth reviewing"
            });
        }

        private static void AddStepSizeHints(AutofocusReport report, List<Hint> hints) {
            var points = report.MeasurePoints;
            var count = points.Count;
            var step = report.StepSize ?? EstimateStepSize(points);
            if (step <= 0) {
                step = EstimateStepSize(points);
            }

            if (count < 7) {
                hints.Add(new Hint {
                    RuleId = "few-samples",
                    Priority = 20,
                    Tier = HintTier.Fact,
                    Text = $"{count} measure points in scan"
                });
            }

            if (step > 0 && TryGetCurveSpanSteps(report, step, out var spanSteps) && spanSteps <= step * 2 && count <= 9) {
                hints.Add(new Hint {
                    RuleId = "step-coarse",
                    Priority = 30,
                    Tier = HintTier.Pattern,
                    Text = $"Step {step}, curve spans ~{spanSteps} steps"
                });
            }

            if (IsMinimumNearEdge(report, out var minIdx)) {
                hints.Add(new Hint {
                    RuleId = "minimum-near-edge",
                    Priority = 40,
                    Tier = HintTier.Pattern,
                    Text = $"Best focus near scan edge (point {minIdx + 1}/{count})"
                });
            } else if (TryGetSteepVBothWings(report, out var steepHint)) {
                hints.Add(steepHint);
            } else if (TryDetectFlatCurve(report)) {
                hints.Add(new Hint {
                    RuleId = "flat-curve",
                    Priority = 33,
                    Tier = HintTier.Pattern,
                    Text = "HFR barely changes across scan — poor seeing or step size too small to resolve focus"
                });
            } else if (TryGetShallowWing(report, out var shallowSide, out var shallowRatio)) {
                hints.Add(new Hint {
                    RuleId = "shallow-wing",
                    Priority = 32,
                    Tier = HintTier.Pattern,
                    Text =
                        $"{shallowSide} wing ~{shallowRatio.ToString("0.0", CultureInfo.InvariantCulture)}× min HFR at scan edge — wider scan or larger step may help"
                });
            }
        }

        private static void AddFitQualityHints(AutofocusReport report, Options options, List<Hint> hints) {
            var fitLabel = report.PrimaryFitLabel(
                options.ShowParabolicFitOnGraph,
                options.ShowHyperbolicFitOnGraph);
            var isParabolic = string.Equals(fitLabel, "Parabolic", StringComparison.Ordinal);
            var primaryR2 = report.PrimaryFitR2(
                options.ShowParabolicFitOnGraph,
                options.ShowHyperbolicFitOnGraph);

            if (!primaryR2.HasValue) {
                hints.Add(new Hint {
                    RuleId = "r2-missing",
                    Priority = 15,
                    Tier = HintTier.Fact,
                    Text = $"{fitLabel} R² not in report"
                });
            } else if (primaryR2.Value < options.MinR2) {
                hints.Add(new Hint {
                    RuleId = "r2-low",
                    Priority = 18,
                    Tier = HintTier.Fact,
                    Text = $"{fitLabel} R² {report.FormatR2(primaryR2)} below gate ({options.MinR2:0.00})"
                });
            }

            if (isParabolic && report.UsesHyperbolicRunFit() && report.R2Hyperbolic.HasValue) {
                hints.Add(new Hint {
                    RuleId = "fit-mode-mismatch",
                    Priority = 47,
                    Tier = HintTier.Pattern,
                    Text = $"Graph shows parabolic; run also has hyperbolic R² {report.FormatR2(report.R2Hyperbolic)}"
                });
            } else if (!isParabolic && report.UsesParabolicRunFit() && report.R2Parabolic.HasValue) {
                hints.Add(new Hint {
                    RuleId = "fit-mode-mismatch",
                    Priority = 47,
                    Tier = HintTier.Pattern,
                    Text = $"Graph shows hyperbolic; run also has parabolic R² {report.FormatR2(report.R2Parabolic)}"
                });
            }

            if (report.R2Left.HasValue && report.R2Right.HasValue) {
                var gap = Math.Abs(report.R2Left.Value - report.R2Right.Value);
                var weaker = Math.Min(report.R2Left.Value, report.R2Right.Value);
                var stronger = Math.Max(report.R2Left.Value, report.R2Right.Value);
                if (gap >= 0.12 && weaker < 0.88) {
                    var side = report.R2Left.Value < report.R2Right.Value ? "Left" : "Right";
                    hints.Add(new Hint {
                        RuleId = "weak-trend-side",
                        Cluster = WingShapeCluster,
                        Priority = 28,
                        Tier = HintTier.Pattern,
                        Text = $"{side} trend R² {weaker.ToString("0.00", CultureInfo.InvariantCulture)} vs {stronger.ToString("0.00", CultureInfo.InvariantCulture)} other side"
                    });
                }
            }

            var fitDelta = isParabolic ? report.ParabolicFitDeltaSteps() : report.HyperbolicFitDeltaSteps();
            var threshold = Math.Max(report.StepSize ?? 2, 2);
            if (fitDelta.HasValue && Math.Abs(fitDelta.Value) >= threshold) {
                var sign = fitDelta.Value > 0 ? "+" : string.Empty;
                hints.Add(new Hint {
                    RuleId = "fit-min-mismatch",
                    Priority = 32,
                    Tier = HintTier.Pattern,
                    Text = $"{fitLabel} minimum {sign}{fitDelta.Value} steps from calculated focus"
                });
            }

            if (TryGetAverageRelativeError(report, out var relError) && relError >= 0.18) {
                var pct = (int)Math.Round(relError * 100);
                hints.Add(new Hint {
                    RuleId = "noisy-points",
                    Priority = 38,
                    Tier = HintTier.Pattern,
                    Text = $"High scatter between points (avg rel. error ~{pct}%)"
                });
            }
        }

        private static void AddHfrHints(AutofocusReport report, Options options, List<Hint> hints) {
            var minHfr = report.MeasurePoints.Min(p => p.Value);
            var primaryR2 = report.PrimaryFitR2(
                options.ShowParabolicFitOnGraph,
                options.ShowHyperbolicFitOnGraph);
            if (report.FinalHfr > options.MaxFinalHfr) {
                var r2Ok = primaryR2.HasValue && primaryR2.Value >= options.MinR2;
                hints.Add(new Hint {
                    RuleId = r2Ok ? "hfr-high-good-fit" : "hfr-high",
                    Priority = r2Ok ? 45 : 22,
                    Tier = HintTier.Fact,
                    Text = r2Ok
                        ? $"Final HFR {report.FormatFinalHfr()} above gate ({options.MaxFinalHfr:0.00}); R² passed"
                        : $"Final HFR {report.FormatFinalHfr()} above gate ({options.MaxFinalHfr:0.00})"
                });
            } else if (minHfr > 0 && report.FinalHfr > minHfr * 1.08) {
                hints.Add(new Hint {
                    RuleId = "final-above-curve-min",
                    Priority = 50,
                    Tier = HintTier.Pattern,
                    Text = $"Final HFR {report.FormatFinalHfr()} above curve min {minHfr.ToString("0.00", CultureInfo.InvariantCulture)}"
                });
            }
        }

        private static void AddFocusShiftHints(AutofocusReport report, Options options, List<Hint> hints) {
            var delta = report.FocusDeltaFromPrevious(options.PreviousFocusFallback);
            var step = Math.Max(report.StepSize ?? 50, 1);
            if (delta.HasValue && Math.Abs(delta.Value) >= step * 3) {
                var sign = delta.Value > 0 ? "+" : string.Empty;
                hints.Add(new Hint {
                    RuleId = "large-focus-delta",
                    Priority = 48,
                    Tier = HintTier.Fact,
                    Text = $"Focus moved {sign}{delta.Value} steps since last AF"
                });
            }
        }

        private static bool IsOvershootModel(AutofocusReport report) {
            return !string.IsNullOrWhiteSpace(report.BacklashMethod) &&
                   report.BacklashMethod.IndexOf("OVERSHOOT", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int? ParseBacklashSteps(object value) {
            if (value == null) {
                return null;
            }

            switch (value) {
                case int i:
                    return i;
                case long l:
                    return (int)l;
                case double d:
                    return (int)Math.Round(d);
                case float f:
                    return (int)Math.Round(f);
            }

            var text = value.ToString()?.Trim();
            if (string.IsNullOrEmpty(text) || string.Equals(text, "N/A", StringComparison.OrdinalIgnoreCase)) {
                return null;
            }

            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
        }

        private static int EstimateStepSize(IReadOnlyList<AutofocusReport.MeasurePoint> points) {
            if (points.Count < 2) {
                return 0;
            }

            var deltas = new List<int>();
            for (var i = 1; i < points.Count; i++) {
                var delta = (int)Math.Round(Math.Abs(points[i].Position - points[i - 1].Position));
                if (delta > 0) {
                    deltas.Add(delta);
                }
            }

            return deltas.Count == 0 ? 0 : (int)Math.Round(deltas.Average());
        }

        private const double ShallowWingRatioThreshold = 1.85;
        private const double SteepWingEdgeRatioThreshold = 2.2;
        private const double SteepWingEdgeRatioMax = 9.0;
        private const double SteepWingOuterDropMinFactor = 0.35;
        private const double SteepWingNearMinFactor = 1.08;
        private const int SteepWingMinReportedStep = 160;

        private static bool TryGetSteepVBothWings(AutofocusReport report, out Hint hint) {
            hint = null;
            var points = report.MeasurePoints;
            var count = points.Count;
            if (count < 5 || count > 9) {
                return false;
            }

            var step = report.StepSize ?? EstimateStepSize(points);
            if (step < SteepWingMinReportedStep) {
                return false;
            }

            var minIdx = 0;
            for (var i = 1; i < count; i++) {
                if (points[i].Value < points[minIdx].Value) {
                    minIdx = i;
                }
            }

            if (minIdx < 2 || minIdx > count - 3) {
                return false;
            }

            var minHfr = points[minIdx].Value;
            if (minHfr <= 0.01) {
                return false;
            }

            var leftEdge = points[0].Value;
            var leftInner = points[1].Value;
            var rightEdge = points[count - 1].Value;
            var rightInner = points[count - 2].Value;
            var leftEdgeRatio = leftEdge / minHfr;
            var rightEdgeRatio = rightEdge / minHfr;
            if (leftEdgeRatio < SteepWingEdgeRatioThreshold ||
                rightEdgeRatio < SteepWingEdgeRatioThreshold ||
                leftEdgeRatio > SteepWingEdgeRatioMax ||
                rightEdgeRatio > SteepWingEdgeRatioMax) {
                return false;
            }

            var leftDrop = leftEdge - leftInner;
            var rightDrop = rightEdge - rightInner;
            var minDrop = Math.Min(leftDrop, rightDrop);
            if (leftDrop <= 0 || rightDrop <= 0 || minDrop < minHfr * SteepWingOuterDropMinFactor) {
                return false;
            }

            var nearMinThreshold = minHfr * SteepWingNearMinFactor;
            var nearMinCount = points.Count(p => p.Value <= nearMinThreshold);
            if (nearMinCount > 2) {
                return false;
            }

            var wingMultiple = (int)Math.Round((leftEdgeRatio + rightEdgeRatio) * 0.5);
            var stepLabel = step > 0 ? step.ToString(CultureInfo.InvariantCulture) : "n";

            hint = new Hint {
                RuleId = "steep-v-both-wings",
                Cluster = WingShapeCluster,
                Priority = 29,
                Tier = HintTier.Pattern,
                Text =
                    $"Coarse V (step {stepLabel}): wings ~{wingMultiple}× min HFR with steep outer steps; only {nearMinCount} point(s) near bottom — typical when step size is large vs offset"
            };
            return true;
        }

        private static bool TryGetShallowWing(AutofocusReport report, out string side, out double ratio) {
            side = null;
            ratio = 0;
            var points = report.MeasurePoints;
            if (points.Count < 5) {
                return false;
            }

            var minIdx = 0;
            for (var i = 1; i < points.Count; i++) {
                if (points[i].Value < points[minIdx].Value) {
                    minIdx = i;
                }
            }

            var minHfr = points[minIdx].Value;
            if (minHfr <= 0.01) {
                return false;
            }

            var leftRatio = points[0].Value / minHfr;
            var rightRatio = points[points.Count - 1].Value / minHfr;
            if (leftRatio >= ShallowWingRatioThreshold && rightRatio >= ShallowWingRatioThreshold) {
                return false;
            }

            if (leftRatio <= rightRatio) {
                side = "Left";
                ratio = leftRatio;
            } else {
                side = "Right";
                ratio = rightRatio;
            }

            return ratio < ShallowWingRatioThreshold;
        }

        private static bool TryGetCurveSpanSteps(AutofocusReport report, int stepSize, out int spanSteps) {
            spanSteps = 0;
            if (report.MeasurePoints.Count < 2 || stepSize <= 0) {
                return false;
            }

            var minPos = report.MeasurePoints.Min(p => p.Position);
            var maxPos = report.MeasurePoints.Max(p => p.Position);
            spanSteps = (int)Math.Round((maxPos - minPos) / stepSize);
            return spanSteps > 0;
        }

        private static bool IsMinimumNearEdge(AutofocusReport report, out int minIdx) {
            minIdx = 0;
            var points = report.MeasurePoints;
            if (points.Count < 4) {
                return false;
            }

            for (var i = 1; i < points.Count; i++) {
                if (points[i].Value < points[minIdx].Value) {
                    minIdx = i;
                }
            }

            return minIdx <= 1 || minIdx >= points.Count - 2;
        }

        private static bool TryDetectZigzag(AutofocusReport report, out int signChanges) {
            signChanges = CountMeaningfulSignChanges(report.MeasurePoints);
            return report.MeasurePoints.Count >= 7 && signChanges >= 3;
        }

        private static int CountMeaningfulSignChanges(IReadOnlyList<AutofocusReport.MeasurePoint> points) {
            if (points.Count < 4) {
                return 0;
            }

            var values = points.Select(p => p.Value).ToList();
            var range = values.Max() - values.Min();
            if (range < 0.2) {
                return 0;
            }

            var noise = Math.Max(0.06, range * 0.04);
            int? lastSign = null;
            var changes = 0;
            for (var i = 1; i < points.Count; i++) {
                var delta = points[i].Value - points[i - 1].Value;
                if (Math.Abs(delta) < noise) {
                    continue;
                }

                var sign = delta > 0 ? 1 : -1;
                if (lastSign.HasValue && sign != lastSign.Value) {
                    changes++;
                }

                lastSign = sign;
            }

            return changes;
        }

        private static bool TryDetectOuterMeasurementCliff(AutofocusReport report, out string side) {
            side = null;
            var points = report.MeasurePoints;
            if (points.Count < 5) {
                return false;
            }

            var minHfr = points.Min(p => p.Value);
            if (minHfr <= 0.01) {
                return false;
            }

            if (points[1].Value > minHfr * 1.8 &&
                points[0].Value < points[1].Value * 0.70) {
                side = "Left";
                return true;
            }

            var last = points.Count - 1;
            if (points[last - 1].Value > minHfr * 1.8 &&
                points[last].Value < points[last - 1].Value * 0.70) {
                side = "Right";
                return true;
            }

            return false;
        }

        private static bool TryDetectApproachPlateau(AutofocusReport report, out string side, out int flatSteps) {
            side = null;
            flatSteps = 0;
            var points = report.MeasurePoints;
            if (points.Count < 6) {
                return false;
            }

            var minIdx = 0;
            for (var i = 1; i < points.Count; i++) {
                if (points[i].Value < points[minIdx].Value) {
                    minIdx = i;
                }
            }

            var minHfr = points[minIdx].Value;
            if (minHfr <= 0.01) {
                return false;
            }

            if (minIdx >= 3 &&
                TryCheckOuterApproachPlateau(points, 0, minIdx - 1, minHfr, out flatSteps)) {
                side = "Left";
                return true;
            }

            if (points.Count - minIdx >= 4 &&
                TryCheckOuterApproachPlateau(points, minIdx + 1, points.Count - 1, minHfr, out flatSteps)) {
                side = "Right";
                return true;
            }

            return false;
        }

        private static bool TryCheckOuterApproachPlateau(
            IReadOnlyList<AutofocusReport.MeasurePoint> points,
            int startIdx,
            int endIdx,
            double minHfr,
            out int flatSteps) {
            flatSteps = 0;
            if (endIdx - startIdx < 3) {
                return false;
            }

            var outerIdx = points[startIdx].Position < points[endIdx].Position ? startIdx : endIdx;
            var innerIdx = outerIdx == startIdx ? endIdx : startIdx;
            var outward = outerIdx < innerIdx;

            var earlySlopes = new List<double>();
            var lateSlopes = new List<double>();
            if (outward) {
                for (var i = outerIdx + 1; i <= Math.Min(outerIdx + 2, innerIdx); i++) {
                    var dx = points[i].Position - points[i - 1].Position;
                    if (dx > 0) {
                        earlySlopes.Add(Math.Abs(points[i].Value - points[i - 1].Value) / dx);
                    }
                }

                for (var i = outerIdx + 3; i <= innerIdx; i++) {
                    var dx = points[i].Position - points[i - 1].Position;
                    if (dx > 0) {
                        lateSlopes.Add(Math.Abs(points[i].Value - points[i - 1].Value) / dx);
                    }
                }

                flatSteps = Math.Min(2, innerIdx - outerIdx);
            } else {
                for (var i = outerIdx; i >= Math.Max(outerIdx - 1, innerIdx + 1); i--) {
                    var dx = points[i].Position - points[i - 1].Position;
                    if (dx > 0) {
                        earlySlopes.Add(Math.Abs(points[i].Value - points[i - 1].Value) / dx);
                    }
                }

                for (var i = outerIdx - 2; i > innerIdx; i--) {
                    var dx = points[i].Position - points[i - 1].Position;
                    if (dx > 0) {
                        lateSlopes.Add(Math.Abs(points[i].Value - points[i - 1].Value) / dx);
                    }
                }

                flatSteps = Math.Min(2, outerIdx - innerIdx);
            }

            if (earlySlopes.Count < 2 || lateSlopes.Count < 1) {
                return false;
            }

            var earlyAvg = earlySlopes.Average();
            var lateAvg = lateSlopes.Average();
            if (lateAvg <= 0.0001 || earlyAvg > lateAvg * 0.30) {
                return false;
            }

            var outerHfr = points[outerIdx].Value;
            if (outerHfr < minHfr * 1.8) {
                return false;
            }

            var flatSpanHfr = Math.Abs(points[Math.Min(outerIdx + flatSteps, innerIdx)].Value - outerHfr);
            return flatSpanHfr < outerHfr * 0.10;
        }

        private static bool TryDetectFlatCurve(AutofocusReport report) {
            var points = report.MeasurePoints;
            if (points.Count < 7) {
                return false;
            }

            var minHfr = points.Min(p => p.Value);
            var maxHfr = points.Max(p => p.Value);
            if (minHfr <= 0.5) {
                return false;
            }

            return (maxHfr - minHfr) / minHfr < 0.22;
        }

        private static bool TryDetectPostMinimumPlateau(
            AutofocusReport report,
            out string side,
            out double lastDy,
            out double lastDx) {
            side = null;
            lastDy = 0;
            lastDx = 0;
            var points = report.MeasurePoints;
            if (points.Count < 5) {
                return false;
            }

            var minIdx = 0;
            for (var i = 1; i < points.Count; i++) {
                if (points[i].Value < points[minIdx].Value) {
                    minIdx = i;
                }
            }

            if (TryCheckWingPlateau(points, minIdx + 1, points.Count - 1, out lastDy, out lastDx)) {
                side = "Right";
                return true;
            }

            if (TryCheckWingPlateau(points, 0, minIdx - 1, out lastDy, out lastDx)) {
                side = "Left";
                return true;
            }

            return false;
        }

        private static bool TryCheckWingPlateau(
            IReadOnlyList<AutofocusReport.MeasurePoint> points,
            int startIdx,
            int endIdx,
            out double lastDy,
            out double lastDx) {
            lastDy = 0;
            lastDx = 0;
            if (endIdx - startIdx < 2) {
                return false;
            }

            var slopes = new List<double>();
            for (var i = startIdx + 1; i <= endIdx; i++) {
                var dx = points[i].Position - points[i - 1].Position;
                if (dx <= 0) {
                    continue;
                }

                slopes.Add(Math.Abs(points[i].Value - points[i - 1].Value) / dx);
            }

            if (slopes.Count < 2) {
                return false;
            }

            var lastSlope = slopes[^1];
            var priorSlopes = slopes.Take(slopes.Count - 1).ToList();
            var avgPrior = priorSlopes.Average();
            if (avgPrior <= 0.0002 || lastSlope >= avgPrior * 0.25) {
                return false;
            }

            lastDx = points[endIdx].Position - points[endIdx - 1].Position;
            lastDy = Math.Abs(points[endIdx].Value - points[endIdx - 1].Value);
            var expectedLastDy = avgPrior * lastDx;
            return lastDy < expectedLastDy * 0.35;
        }

        private static bool TryGetWingAsymmetry(AutofocusReport report, out double asymmetry) {
            asymmetry = 0;
            if (!report.CalculatedPosition.HasValue) {
                return false;
            }

            var focus = report.CalculatedPosition.Value;
            var left = report.MeasurePoints.Where(p => p.Position < focus - 0.5).ToList();
            var right = report.MeasurePoints.Where(p => p.Position > focus + 0.5).ToList();
            if (left.Count < 2 || right.Count < 2) {
                return false;
            }

            var leftAvg = left.Average(p => p.Value);
            var rightAvg = right.Average(p => p.Value);
            var baseline = Math.Max(leftAvg, rightAvg);
            if (baseline <= 0) {
                return false;
            }

            asymmetry = Math.Abs(leftAvg - rightAvg) / baseline;
            return true;
        }

        private static bool TryGetAverageRelativeError(AutofocusReport report, out double relativeError) {
            relativeError = 0;
            var usable = report.MeasurePoints.Where(p => p.Value > 0.01).ToList();
            if (usable.Count < 3) {
                return false;
            }

            relativeError = usable.Average(p => p.Error / p.Value);
            return true;
        }
    }
}
