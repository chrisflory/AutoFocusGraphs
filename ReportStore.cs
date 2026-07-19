using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AutoFocusGraphs {
    /// <summary>
    /// Loads autofocus reports from disk and tracks reports seen in this NINA session.
    /// </summary>
    internal sealed class ReportStore {
        public static ReportStore Instance { get; } = new ReportStore();

        private readonly object gate = new object();
        private readonly List<AutofocusReport> sessionReports = new List<AutofocusReport>();
        private readonly List<AutofocusReport> sequenceReports = new List<AutofocusReport>();
        private int sessionSequencesCompleted;
        private readonly List<string> sessionSequenceNames = new List<string>();
        private string pendingSequenceName;

        public event EventHandler Changed;

        /// <summary>Sequencer runs that finished since this NINA process started.</summary>
        public int SessionSequencesCompleted {
            get {
                lock (gate) {
                    return sessionSequencesCompleted;
                }
            }
        }

        public IReadOnlyList<string> SessionSequenceNames {
            get {
                lock (gate) {
                    return sessionSequenceNames.ToList();
                }
            }
        }

        public void SetPendingSequenceName(string name) {
            lock (gate) {
                pendingSequenceName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            }
        }

        public IReadOnlyList<AutofocusReport> SessionReports {
            get {
                lock (gate) {
                    return sessionReports.ToList();
                }
            }
        }

        public IReadOnlyList<AutofocusReport> SequenceReports {
            get {
                lock (gate) {
                    return sequenceReports.ToList();
                }
            }
        }

        /// <summary>Clears runs tracked for the current sequencer run.</summary>
        public void BeginSequence() {
            lock (gate) {
                sequenceReports.Clear();
            }
            try {
                Changed?.Invoke(this, EventArgs.Empty);
            } catch (Exception ex) {
                Logger.Warning($"AutoFocusGraphs: report change notification failed: {ex.Message}");
            }
        }

        public string GetPendingSequenceName() {
            lock (gate) {
                return pendingSequenceName;
            }
        }

        /// <summary>Pending sequence name, or the most recently completed sequence in this session.</summary>
        public string GetSequenceNameForDigest() {
            lock (gate) {
                if (!string.IsNullOrWhiteSpace(pendingSequenceName)) {
                    return pendingSequenceName;
                }

                return sessionSequenceNames.Count > 0
                    ? sessionSequenceNames[sessionSequenceNames.Count - 1]
                    : null;
            }
        }

        /// <summary>Records that the sequencer reported a finished run (session digest stat).</summary>
        public string RecordSequenceCompleted() {
            string name;
            lock (gate) {
                sessionSequencesCompleted++;
                name = string.IsNullOrWhiteSpace(pendingSequenceName) ? "Unnamed sequence" : pendingSequenceName;
                sessionSequenceNames.Add(name);
                pendingSequenceName = null;
            }
            try {
                Changed?.Invoke(this, EventArgs.Empty);
            } catch (Exception ex) {
                Logger.Warning($"AutoFocusGraphs: report change notification failed: {ex.Message}");
            }

            return name;
        }

        public void ClearSequenceReports() {
            BeginSequence();
        }

        public IReadOnlyList<AutofocusReport> GetSequenceDigestReports() {
            lock (gate) {
                return sequenceReports
                    .OrderBy(r => r.FormattedTimestamp, StringComparer.Ordinal)
                    .ThenBy(r => r.FileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        public void AddSessionReport(AutofocusReport report) {
            if (report == null) {
                return;
            }
            lock (gate) {
                sessionReports.RemoveAll(r => string.Equals(r.FileName, report.FileName, StringComparison.OrdinalIgnoreCase));
                sessionReports.Add(report);
                sequenceReports.RemoveAll(r => string.Equals(r.FileName, report.FileName, StringComparison.OrdinalIgnoreCase));
                sequenceReports.Add(report);
            }
            try {
                Changed?.Invoke(this, EventArgs.Empty);
            } catch (Exception ex) {
                // A UI subscriber failing must never abort report processing/posting.
                Logger.Warning($"AutoFocusGraphs: report change notification failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculated focus from the most recent prior session report (excludes the current file).
        /// Used when JSON PreviousFocusPoint matches the new result and hides the real step delta.
        /// </summary>
        public double? GetPreviousCalculatedFocusPosition(string excludeFileName) {
            lock (gate) {
                AutofocusReport previous = null;
                foreach (var candidate in sessionReports) {
                    if (string.Equals(candidate.FileName, excludeFileName, StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }
                    if (!candidate.CalculatedPosition.HasValue) {
                        continue;
                    }
                    if (previous == null || CompareReportOrder(candidate, previous) > 0) {
                        previous = candidate;
                    }
                }

                return previous?.CalculatedPosition;
            }
        }

        /// <summary>
        /// Most recent prior session report with the same filter and a usable V-curve (≥3 measure points).
        /// </summary>
        public AutofocusReport GetPreviousReportSameFilter(string excludeFileName, string filter) {
            if (string.IsNullOrWhiteSpace(filter)) {
                return null;
            }

            lock (gate) {
                AutofocusReport previous = null;
                foreach (var candidate in sessionReports) {
                    if (string.Equals(candidate.FileName, excludeFileName, StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }

                    if (!string.Equals(candidate.Filter, filter, StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }

                    if (candidate.MeasurePoints == null || candidate.MeasurePoints.Count < 3) {
                        continue;
                    }

                    if (previous == null || CompareReportOrder(candidate, previous) > 0) {
                        previous = candidate;
                    }
                }

                return previous;
            }
        }

        private static int CompareReportOrder(AutofocusReport a, AutofocusReport b) {
            var aUtc = a.CapturedUtc ?? DateTime.MinValue;
            var bUtc = b.CapturedUtc ?? DateTime.MinValue;
            var cmp = aUtc.CompareTo(bUtc);
            if (cmp != 0) {
                return cmp;
            }

            return string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Autofocus runs captured since this NINA process started.</summary>
        public IReadOnlyList<AutofocusReport> GetSessionDigestReports() =>
            OrderDigestReports(SessionReports);

        /// <summary>Merges session reports with disk reports, preferring in-memory session copies on name collision.</summary>
        public IReadOnlyList<AutofocusReport> MergeDigestReports(
            IReadOnlyList<AutofocusReport> sessionReports,
            IReadOnlyList<AutofocusReport> additionalReports) {
            if (additionalReports == null || additionalReports.Count == 0) {
                return OrderDigestReports(sessionReports ?? Array.Empty<AutofocusReport>());
            }

            lock (gate) {
                var merged = new Dictionary<string, AutofocusReport>(StringComparer.OrdinalIgnoreCase);
                foreach (var report in additionalReports) {
                    if (report == null || string.IsNullOrWhiteSpace(report.FileName)) {
                        continue;
                    }

                    merged[report.FileName] = report;
                }

                foreach (var report in sessionReports ?? Array.Empty<AutofocusReport>()) {
                    if (report == null || string.IsNullOrWhiteSpace(report.FileName)) {
                        continue;
                    }

                    merged[report.FileName] = report;
                }

                return OrderDigestReports(merged.Values);
            }
        }

        private static IReadOnlyList<AutofocusReport> OrderDigestReports(IEnumerable<AutofocusReport> reports) =>
            reports
                .OrderBy(r => r.FormattedTimestamp, StringComparer.Ordinal)
                .ThenBy(r => r.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

        public IReadOnlyList<AutofocusReport> LoadTodayFromDisk(int maxCount = 200) {
            var folder = AutofocusFolderWatcher.AutoFocusFolder;
            if (!Directory.Exists(folder)) {
                return Array.Empty<AutofocusReport>();
            }

            var today = DateTime.Now.Date;
            var files = Directory.GetFiles(folder, "*.json")
                .Select(f => new FileInfo(f))
                .Where(f => f.Length > 0 && f.Length <= AutofocusFolderWatcher.MaxReportBytes)
                .Where(f => f.LastWriteTime.Date == today || f.CreationTime.Date == today)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(maxCount)
                .ToList();

            return LoadFiles(files);
        }

        public IReadOnlyList<AutofocusReport> LoadFromDisk(int maxCount = 100) {
            var folder = AutofocusFolderWatcher.AutoFocusFolder;
            if (!Directory.Exists(folder)) {
                return Array.Empty<AutofocusReport>();
            }

            var files = Directory.GetFiles(folder, "*.json")
                .Select(f => new FileInfo(f))
                .Where(f => f.Length > 0 && f.Length <= AutofocusFolderWatcher.MaxReportBytes)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(maxCount)
                .ToList();

            return LoadFiles(files);
        }

        private static IReadOnlyList<AutofocusReport> LoadFiles(IEnumerable<FileInfo> files) {
            var reports = new List<AutofocusReport>();
            foreach (var file in files) {
                try {
                    var json = File.ReadAllText(file.FullName);
                    reports.Add(AutofocusReport.Parse(json, file.Name, file.FullName));
                } catch (Exception ex) {
                    Logger.Warning($"AutoFocusGraphs: skipped {file.Name}: {ex.Message}");
                }
            }
            return reports;
        }
    }
}
