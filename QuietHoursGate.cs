using System;
using System.Globalization;

namespace AutoFocusGraphs {
    /// <summary>
    /// Local quiet-hours window for suppressing successful per-run posts.
    /// </summary>
    internal static class QuietHoursGate {
        public const string DefaultStart = "22:00";
        public const string DefaultEnd = "07:00";

        public static bool TryParseTime(string value, out TimeSpan time) {
            time = default;
            if (string.IsNullOrWhiteSpace(value)) {
                return false;
            }

            return TimeSpan.TryParseExact(
                       value.Trim(),
                       new[] { @"hh\:mm", @"h\:mm" },
                       CultureInfo.InvariantCulture,
                       out time)
                   || TimeSpan.TryParse(value.Trim(), CultureInfo.InvariantCulture, out time);
        }

        public static string NormalizeTime(string value, string fallback) {
            if (TryParseTime(value, out var time)) {
                return $"{(int)time.TotalHours:00}:{time.Minutes:00}";
            }

            return fallback;
        }

        public static bool IsInQuietHours(DateTime nowLocal, string startText, string endText) {
            if (!TryParseTime(startText, out var start)) {
                TryParseTime(DefaultStart, out start);
            }

            if (!TryParseTime(endText, out var end)) {
                TryParseTime(DefaultEnd, out end);
            }

            var now = nowLocal.TimeOfDay;
            if (start == end) {
                return true;
            }

            if (start < end) {
                return now >= start && now < end;
            }

            // Wraps midnight (e.g. 22:00 → 07:00).
            return now >= start || now < end;
        }

        /// <summary>
        /// During quiet hours, Success per-run posts are suppressed. Warnings/failures always allowed.
        /// </summary>
        public static bool ShouldPostPerRun(
            ReportOutcome outcome,
            bool quietHoursEnabled,
            string startText,
            string endText,
            DateTime? nowLocal = null) {
            if (!quietHoursEnabled) {
                return true;
            }

            if (outcome != ReportOutcome.Success) {
                return true;
            }

            var now = nowLocal ?? DateTime.Now;
            return !IsInQuietHours(now, startText, endText);
        }
    }
}
