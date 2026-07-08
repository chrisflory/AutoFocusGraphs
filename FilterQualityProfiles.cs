using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AutoFocusGraphs {
    /// <summary>
    /// Optional per-filter quality thresholds.
    /// Stored as lines: FilterName,minR2,maxHfr
    /// Unmatched filters use the global defaults.
    /// </summary>
    internal static class FilterQualityProfiles {
        public static (double minR2, double maxFinalHfr, string profileLabel) Resolve(
            string filter,
            string profilesText,
            double defaultMinR2,
            double defaultMaxFinalHfr) {
            var map = Parse(profilesText);
            if (map.Count == 0 || string.IsNullOrWhiteSpace(filter) || filter == "N/A") {
                return (defaultMinR2, defaultMaxFinalHfr, "default");
            }

            var reportFilter = filter.Trim();

            // Exact match first.
            if (map.TryGetValue(reportFilter, out var exact)) {
                return (exact.MinR2, exact.MaxFinalHfr, reportFilter);
            }

            // Prefix match: profile "Ha" matches report filter "Ha-7nm" / "Ha_3nm" / "Ha 7nm".
            // Prefer the longest profile key when several match (e.g. "Ha-7nm" over "Ha").
            string bestKey = null;
            FilterProfile bestProfile = default;
            foreach (var pair in map) {
                if (!FilterMatchesProfile(reportFilter, pair.Key)) {
                    continue;
                }
                if (bestKey == null || pair.Key.Length > bestKey.Length) {
                    bestKey = pair.Key;
                    bestProfile = pair.Value;
                }
            }

            if (bestKey != null) {
                return (bestProfile.MinR2, bestProfile.MaxFinalHfr, bestKey);
            }

            return (defaultMinR2, defaultMaxFinalHfr, "default");
        }

        /// <summary>
        /// True when the report filter equals the profile name, or starts with it followed by a non-letter
        /// (so "Ha" matches "Ha-7nm" but not "HaOIII").
        /// </summary>
        internal static bool FilterMatchesProfile(string reportFilter, string profileName) {
            if (string.IsNullOrWhiteSpace(reportFilter) || string.IsNullOrWhiteSpace(profileName)) {
                return false;
            }
            reportFilter = reportFilter.Trim();
            profileName = profileName.Trim();
            if (string.Equals(reportFilter, profileName, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
            if (!reportFilter.StartsWith(profileName, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
            if (reportFilter.Length == profileName.Length) {
                return true;
            }
            return !char.IsLetter(reportFilter[profileName.Length]);
        }

        public static IReadOnlyDictionary<string, FilterProfile> Parse(string profilesText) {
            var map = new Dictionary<string, FilterProfile>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(profilesText)) {
                return map;
            }

            foreach (var rawLine in profilesText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#')) {
                    continue;
                }

                var parts = line.Split(',');
                if (parts.Length < 3) {
                    continue;
                }

                var name = parts[0].Trim();
                if (name.Length == 0) {
                    continue;
                }

                if (!double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var minR2)) {
                    continue;
                }
                if (!double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var maxHfr)) {
                    continue;
                }

                minR2 = Math.Clamp(minR2, 0, 1);
                maxHfr = Math.Max(0.1, maxHfr);
                map[name] = new FilterProfile(minR2, maxHfr);
            }

            return map;
        }

        public static string Serialize(IEnumerable<FilterProfileRow> rows) {
            if (rows == null) {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var row in rows) {
                if (row == null || !row.TryGetValues(out var name, out var minR2, out var maxHfr)) {
                    continue;
                }
                if (sb.Length > 0) {
                    sb.AppendLine();
                }
                sb.Append(name)
                    .Append(',')
                    .Append(minR2.ToString("0.##", CultureInfo.InvariantCulture))
                    .Append(',')
                    .Append(maxHfr.ToString("0.##", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        public static List<FilterProfileRow> ToRows(string profilesText, Action onChanged) {
            var rows = new List<FilterProfileRow>();
            foreach (var pair in Parse(profilesText).OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)) {
                rows.Add(new FilterProfileRow(onChanged, pair.Key, pair.Value.MinR2, pair.Value.MaxFinalHfr));
            }
            return rows;
        }

        internal readonly struct FilterProfile {
            public FilterProfile(double minR2, double maxFinalHfr) {
                MinR2 = minR2;
                MaxFinalHfr = maxFinalHfr;
            }

            public double MinR2 { get; }
            public double MaxFinalHfr { get; }
        }
    }
}
