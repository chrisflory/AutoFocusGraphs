using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AutoFocusGraphs {
    /// <summary>
    /// Summarizes focus position vs temperature drift across a digest report set.
    /// </summary>
    internal static class FocusDriftAnalyzer {
        private const double MinTempDeltaCelsius = 0.5;
        private const double LargeDriftStepsPerC = 50.0;

        internal sealed class Summary {
            public string Text { get; init; }
            public bool IsLargeDrift { get; init; }
            public double DeltaPosition { get; init; }
            public double DeltaTemperature { get; init; }
            public double? StepsPerCelsius { get; init; }
            public int SampleCount { get; init; }
        }

        public static Summary Summarize(IReadOnlyList<AutofocusReport> reports) {
            var points = SelectUsable(reports);
            if (points.Count < 3) {
                return null;
            }

            var first = points[0];
            var last = points[points.Count - 1];
            var deltaPos = last.Position - first.Position;
            var deltaT = last.Temperature - first.Temperature;
            double? stepsPerC = null;
            var isLarge = false;
            if (Math.Abs(deltaT) >= MinTempDeltaCelsius) {
                stepsPerC = deltaPos / deltaT;
                isLarge = Math.Abs(stepsPerC.Value) >= LargeDriftStepsPerC;
            }

            var parts = new List<string> {
                $"Focus drift: Δpos {deltaPos.ToString("+0;-0;0", CultureInfo.InvariantCulture)} steps",
                $"ΔT {deltaT.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture)}°C",
                $"n={points.Count}"
            };
            if (stepsPerC.HasValue) {
                parts.Add($"{stepsPerC.Value.ToString("0.0", CultureInfo.InvariantCulture)} steps/°C");
            }

            if (isLarge) {
                parts.Add("large drift");
            }

            return new Summary {
                Text = string.Join(" · ", parts),
                IsLargeDrift = isLarge,
                DeltaPosition = deltaPos,
                DeltaTemperature = deltaT,
                StepsPerCelsius = stepsPerC,
                SampleCount = points.Count
            };
        }

        public static IReadOnlyList<(double Position, double Temperature, AutofocusReport Report)> SelectUsable(
            IReadOnlyList<AutofocusReport> reports) {
            if (reports == null || reports.Count == 0) {
                return Array.Empty<(double, double, AutofocusReport)>();
            }

            return reports
                .Where(r => r != null && r.CalculatedPosition.HasValue && r.Temperature.HasValue)
                .OrderBy(r => r.CapturedUtc ?? DateTime.MinValue)
                .ThenBy(r => r.FormattedTimestamp, StringComparer.Ordinal)
                .ThenBy(r => r.FileName, StringComparer.OrdinalIgnoreCase)
                .Select(r => (r.CalculatedPosition.Value, r.Temperature.Value, r))
                .ToList();
        }
    }
}
