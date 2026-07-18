using System;
using System.Threading;

namespace AutoFocusGraphs {
    /// <summary>
    /// Decides whether a parsed autofocus report should be posted to destinations
    /// when per-run posting is enabled. Digests are unaffected.
    /// </summary>
    internal static class PerRunSendGate {
        public const string ModeEveryRun = "EveryRun";
        public const string ModeEveryNth = "EveryNth";
        public const string ModeProblemsOnly = "ProblemsOnly";

        public const string DisplayEveryRun = "Every run";
        public const string DisplayEveryNth = "Every Nth run";
        public const string DisplayProblemsOnly = "Problems only";

        public const int MinEveryN = 2;
        public const int MaxEveryN = 50;
        public const int DefaultEveryN = 5;

        private static int successCounter;

        public static void ResetSession() => Interlocked.Exchange(ref successCounter, 0);

        public static int SuccessCount => Volatile.Read(ref successCounter);

        public static string NormalizeMode(string mode) {
            if (string.Equals(mode, ModeEveryNth, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, DisplayEveryNth, StringComparison.OrdinalIgnoreCase)) {
                return ModeEveryNth;
            }

            if (string.Equals(mode, ModeProblemsOnly, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, DisplayProblemsOnly, StringComparison.OrdinalIgnoreCase)) {
                return ModeProblemsOnly;
            }

            return ModeEveryRun;
        }

        public static string ToDisplay(string mode) => NormalizeMode(mode) switch {
            ModeEveryNth => DisplayEveryNth,
            ModeProblemsOnly => DisplayProblemsOnly,
            _ => DisplayEveryRun
        };

        public static int ClampEveryN(int n) {
            if (n < MinEveryN) {
                return MinEveryN;
            }

            if (n > MaxEveryN) {
                return MaxEveryN;
            }

            return n;
        }

        /// <summary>
        /// Returns whether to post. For EveryNth, only Success outcomes advance the counter;
        /// warnings/failures always post and do not consume the Nth counter.
        /// </summary>
        public static bool ShouldPost(QualityResult quality, string mode, int everyN, out string skipReason) {
            skipReason = null;
            var outcome = quality?.Outcome ?? ReportOutcome.Success;
            var normalized = NormalizeMode(mode);

            if (normalized == ModeEveryRun) {
                return true;
            }

            if (normalized == ModeProblemsOnly) {
                if (outcome == ReportOutcome.Warning || outcome == ReportOutcome.Failure) {
                    return true;
                }

                skipReason = "problems-only";
                return false;
            }

            // EveryNth
            if (outcome == ReportOutcome.Warning || outcome == ReportOutcome.Failure) {
                return true;
            }

            var n = ClampEveryN(everyN);
            var count = Interlocked.Increment(ref successCounter);
            if (count % n == 0) {
                return true;
            }

            skipReason = $"every {n}th (success #{count})";
            return false;
        }
    }
}
