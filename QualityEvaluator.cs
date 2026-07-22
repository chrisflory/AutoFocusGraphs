using System;

namespace AutoFocusGraphs {
    internal static class QualityEvaluator {
        public static QualityResult Evaluate(
            AutofocusReport report,
            bool isFailure,
            string failureReason,
            bool qualityGateEnabled,
            double minR2,
            double maxFinalHfr) {
            if (isFailure) {
                return new QualityResult {
                    Outcome = ReportOutcome.Failure,
                    Reason = string.IsNullOrWhiteSpace(failureReason) ? "Report could not be parsed." : failureReason
                };
            }

            if (!qualityGateEnabled) {
                return new QualityResult {
                    Outcome = ReportOutcome.Success,
                    Reason = string.Empty
                };
            }

            if (!report.R2Hyperbolic.HasValue) {
                return new QualityResult {
                    Outcome = ReportOutcome.Warning,
                    Reason = "Hyperbolic R² is missing or NaN."
                };
            }

            if (report.R2Hyperbolic.Value < minR2) {
                return new QualityResult {
                    Outcome = ReportOutcome.Warning,
                    Reason = $"Hyperbolic R² {report.FormatR2(report.R2Hyperbolic)} is below threshold {minR2:0.00}."
                };
            }

            if (double.IsNaN(report.FinalHfr) || double.IsInfinity(report.FinalHfr)) {
                return new QualityResult {
                    Outcome = ReportOutcome.Warning,
                    Reason = "Final HFR is missing or not a number."
                };
            }

            if (report.FinalHfr > maxFinalHfr) {
                return new QualityResult {
                    Outcome = ReportOutcome.Warning,
                    Reason = $"Final HFR {report.FormatFinalHfr()} is above threshold {maxFinalHfr:0.00}."
                };
            }

            return new QualityResult {
                Outcome = ReportOutcome.Success,
                Reason = string.Empty
            };
        }
    }
}
