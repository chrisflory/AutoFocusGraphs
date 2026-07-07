using System;

namespace AutofocusGraphs {
    /// <summary>
    /// Tracks live autofocus runs so we can detect endings without a JSON report file.
    /// </summary>
    internal sealed class AutofocusRunTracker {
        public static AutofocusRunTracker Instance { get; } = new AutofocusRunTracker();

        private readonly object gate = new object();
        private DateTime? runStartedUtc;
        private DateTime? lastReportUtc;
        private int reportsAtRunStart;

        public void MarkRunStarting() {
            lock (gate) {
                runStartedUtc = DateTime.UtcNow;
                reportsAtRunStart = ReportStore.Instance.SessionReports.Count;
            }
        }

        public void MarkReportReceived() {
            lock (gate) {
                lastReportUtc = DateTime.UtcNow;
                runStartedUtc = null;
            }
        }

        /// <summary>True while an autofocus run has started but its JSON has not been stored yet.</summary>
        public bool HasPendingReport() {
            lock (gate) {
                return runStartedUtc.HasValue;
            }
        }

        public bool ShouldCheckForMissingReport(out DateTime startedUtc, out int baselineReports) {
            lock (gate) {
                if (!runStartedUtc.HasValue) {
                    startedUtc = default;
                    baselineReports = 0;
                    return false;
                }

                startedUtc = runStartedUtc.Value;
                baselineReports = reportsAtRunStart;
                runStartedUtc = null;
                return true;
            }
        }

        public bool HasNewReportSince(DateTime startedUtc, int baselineReports) {
            var reports = ReportStore.Instance.SessionReports;
            if (reports.Count > baselineReports) {
                return true;
            }

            foreach (var report in reports) {
                if (report?.CapturedUtc >= startedUtc) {
                    return true;
                }
            }

            lock (gate) {
                return lastReportUtc.HasValue && lastReportUtc.Value >= startedUtc;
            }
        }
    }
}
