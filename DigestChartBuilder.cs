using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoFocusGraphs {
    /// <summary>
    /// Shared HFR trend + focus-drift chart packaging for digests.
    /// </summary>
    internal static class DigestChartBuilder {
        internal sealed class Result {
            public byte[] TrendPng { get; init; }
            public byte[] DriftPng { get; init; }
            public string DriftSummary { get; init; }
            public bool HasTrend => TrendPng != null && TrendPng.Length > 0;
            public bool HasDrift => DriftPng != null && DriftPng.Length > 0;
        }

        public static Result TryBuild(IReadOnlyList<AutofocusReport> reports, int maxRuns) {
            if (reports == null || reports.Count == 0) {
                return new Result();
            }

            var ordered = reports
                .OrderBy(r => r.FormattedTimestamp, StringComparer.Ordinal)
                .ThenBy(r => r.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            byte[] trend = null;
            try {
                trend = AutofocusGraphGenerator.CreateTrendPng(ordered, maxRuns);
            } catch {
                // text-only is fine
            }

            byte[] drift = null;
            try {
                drift = AutofocusGraphGenerator.CreateDriftPng(ordered, maxRuns);
            } catch {
                // optional
            }

            var summary = FocusDriftAnalyzer.Summarize(ordered);
            return new Result {
                TrendPng = trend,
                DriftPng = drift,
                DriftSummary = summary?.Text
            };
        }
    }
}
