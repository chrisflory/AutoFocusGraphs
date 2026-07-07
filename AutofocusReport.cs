using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AutofocusGraphs {
    /// <summary>
    /// Parsed N.I.N.A. autofocus JSON report (Hocus Focus / built-in).
    /// </summary>
    public sealed class AutofocusReport {
        public string FileName { get; init; }
        public string SourcePath { get; init; }
        public double FinalHfr { get; init; }
        public string Filter { get; init; }
        public string Method { get; init; }
        public string Fitting { get; init; }
        public double? Temperature { get; init; }
        public double? CalculatedPosition { get; init; }
        public int? StepSize { get; init; }
        public string BacklashMethod { get; init; }
        public object BacklashIn { get; init; }
        public object BacklashOut { get; init; }
        public double? R2Hyperbolic { get; init; }
        /// <summary>JSON field is "Quadratic"; NINA UI calls this Parabolic.</summary>
        public double? R2Parabolic { get; init; }
        public double? R2Left { get; init; }
        public double? R2Right { get; init; }
        public double? HyperbolicMinimumPosition { get; init; }
        public double? HyperbolicMinimumValue { get; init; }
        public double? ParabolicMinimumPosition { get; init; }
        public double? ParabolicMinimumValue { get; init; }
        public double? InitialFocusPosition { get; init; }
        public double? InitialFocusValue { get; init; }
        public double? PreviousFocusPosition { get; init; }
        public double? PreviousFocusValue { get; init; }
        public string FormattedTimestamp { get; init; }
        public DateTime? CapturedUtc { get; init; }
        public string FormattedDuration { get; init; }
        public IReadOnlyList<MeasurePoint> MeasurePoints { get; init; }

        public bool HasHyperbolicFit =>
            R2Hyperbolic.HasValue ||
            HyperbolicMinimumPosition.HasValue ||
            string.Equals(Fitting, "HYPERBOLIC", StringComparison.OrdinalIgnoreCase);

        public bool HasParabolicFit =>
            R2Parabolic.HasValue ||
            ParabolicMinimumPosition.HasValue ||
            string.Equals(Fitting, "QUADRATIC", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Fitting, "PARABOLIC", StringComparison.OrdinalIgnoreCase);

        public sealed class MeasurePoint {
            public double Position { get; init; }
            public double Value { get; init; }
            public double Error { get; init; }
            /// <summary>Hocus Focus / NINA may mark points excluded from the weighted fit.</summary>
            public bool? IsOutlier { get; init; }
        }

        public double? HyperbolicMinimumStdError { get; init; }
        public double? HyperbolicReducedChiSquared { get; init; }
        public double? HyperbolicLeaveOneOutStdError { get; init; }
        public int? HyperbolicFitModelChosen { get; init; }
        public int? MaxOutlierRejections { get; init; }

        public IReadOnlyList<int> GetOutlierPointIndices() {
            var flagged = new List<int>();
            for (var i = 0; i < MeasurePoints.Count; i++) {
                if (MeasurePoints[i].IsOutlier == true) {
                    flagged.Add(i);
                }
            }

            if (flagged.Count > 0) {
                return flagged;
            }

            if (TryDetectHighResidualPoint(out var index)) {
                flagged.Add(index);
            }

            return flagged;
        }

        private bool TryDetectHighResidualPoint(out int index) {
            index = -1;
            if (MeasurePoints == null || MeasurePoints.Count < 5) {
                return false;
            }

            var minIdx = 0;
            for (var i = 1; i < MeasurePoints.Count; i++) {
                if (MeasurePoints[i].Value < MeasurePoints[minIdx].Value) {
                    minIdx = i;
                }
            }

            var bestResidual = 0.0;
            TryMaxWingResidual(0, minIdx, ref bestResidual, ref index);
            TryMaxWingResidual(minIdx, MeasurePoints.Count - 1, ref bestResidual, ref index);
            return index >= 0;
        }

        private void TryMaxWingResidual(int startIdx, int endIdx, ref double bestResidual, ref int bestIndex) {
            if (endIdx - startIdx < 2) {
                return;
            }

            var xs = new List<double>();
            var ys = new List<double>();
            for (var i = startIdx; i <= endIdx; i++) {
                xs.Add(MeasurePoints[i].Position);
                ys.Add(MeasurePoints[i].Value);
            }

            if (!TryLinearFit(xs, ys, out var slope, out var intercept)) {
                return;
            }

            var residuals = new List<double>();
            for (var i = startIdx; i <= endIdx; i++) {
                var expected = slope * MeasurePoints[i].Position + intercept;
                residuals.Add(Math.Abs(MeasurePoints[i].Value - expected));
            }

            var rmse = Math.Sqrt(residuals.Average(r => r * r));
            var threshold = Math.Max(0.12, rmse * 2.5);
            for (var i = startIdx; i <= endIdx; i++) {
                var expected = slope * MeasurePoints[i].Position + intercept;
                var residual = Math.Abs(MeasurePoints[i].Value - expected);
                if (residual < threshold || residual <= bestResidual) {
                    continue;
                }

                bestResidual = residual;
                bestIndex = i;
            }
        }

        private static bool TryLinearFit(IReadOnlyList<double> xs, IReadOnlyList<double> ys, out double slope, out double intercept) {
            slope = 0;
            intercept = 0;
            if (xs.Count != ys.Count || xs.Count < 2) {
                return false;
            }

            var count = xs.Count;
            double sumX = 0, sumY = 0, sumXy = 0, sumXx = 0;
            for (var i = 0; i < count; i++) {
                sumX += xs[i];
                sumY += ys[i];
                sumXy += xs[i] * ys[i];
                sumXx += xs[i] * xs[i];
            }

            var denom = count * sumXx - sumX * sumX;
            if (Math.Abs(denom) < 1e-9) {
                return false;
            }

            slope = (count * sumXy - sumX * sumY) / denom;
            intercept = (sumY - slope * sumX) / count;
            return true;
        }

        private const int MaxMeasurePoints = 500;

        public string DisplayLabel =>
            $"{FormattedTimestamp} | Filter {Filter} | HFR {FormatFinalHfr()} | Pos {FormatCalculatedPosition()}";

        public static AutofocusReport Parse(string json, string fileName, string sourcePath = null) {
            var root = JObject.Parse(json);
            var points = root["MeasurePoints"] as JArray;
            if (points == null || points.Count == 0) {
                throw new InvalidOperationException("Report has no MeasurePoints.");
            }
            if (points.Count > MaxMeasurePoints) {
                throw new InvalidOperationException($"Report has too many MeasurePoints ({points.Count}).");
            }

            var measurePoints = points
                .Select(p => new MeasurePoint {
                    Position = p.Value<double>("Position"),
                    Value = p.Value<double>("Value"),
                    Error = p.Value<double?>("Error") ?? 0,
                    IsOutlier = ReadOptionalBool(p, "IsOutlier") ?? ReadOptionalBool(p, "Outlier")
                })
                .OrderBy(p => p.Position)
                .ToList();

            var hocusFocus = root["HocusFocusAutoFocusOptions"] as JObject;

            var r2 = root["RSquares"] as JObject;
            var bc = root["BacklashCompensation"] as JObject;
            var focuser = root["FocuserOptions"] as JObject;
            var calc = root["CalculatedFocusPoint"] as JObject;
            var initial = root["InitialFocusPoint"] as JObject;
            var previous = root["PreviousFocusPoint"] as JObject;
            var intersections = root["Intersections"] as JObject;
            var hyperbolicMin = intersections?["HyperbolicMinimum"] as JObject;
            // JSON uses "Quadratic"; NINA's AF UI labels it "Parabolic".
            var parabolicMin = intersections?["QuadraticMinimum"] as JObject;

            var rawTimestamp = ReadJsonString(root, "Timestamp");
            DateTime? capturedUtc = null;
            if (DateTime.TryParse(rawTimestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedUtc)) {
                capturedUtc = parsedUtc.ToUniversalTime();
            }

            return new AutofocusReport {
                FileName = fileName,
                SourcePath = sourcePath,
                FinalHfr = root.Value<double?>("FinalHFR") ?? measurePoints.Min(p => p.Value),
                Filter = root.Value<string>("Filter") ?? "N/A",
                Method = root.Value<string>("Method") ?? "N/A",
                Fitting = root.Value<string>("Fitting") ?? "N/A",
                Temperature = root.Value<double?>("Temperature"),
                CalculatedPosition = calc?.Value<double?>("Position"),
                StepSize = focuser?.Value<int?>("AutoFocusStepSize"),
                BacklashMethod = bc?.Value<string>("BacklashCompensationModel") ?? "N/A",
                BacklashIn = bc?["BacklashIN"]?.ToObject<object>() ?? "N/A",
                BacklashOut = bc?["BacklashOUT"]?.ToObject<object>() ?? "N/A",
                R2Hyperbolic = ReadR2(r2, "Hyperbolic"),
                R2Parabolic = ReadR2(r2, "Quadratic"),
                R2Left = ReadR2(r2, "LeftTrend"),
                R2Right = ReadR2(r2, "RightTrend"),
                HyperbolicMinimumPosition = hyperbolicMin?.Value<double?>("Position"),
                HyperbolicMinimumValue = hyperbolicMin?.Value<double?>("Value"),
                ParabolicMinimumPosition = parabolicMin?.Value<double?>("Position"),
                ParabolicMinimumValue = parabolicMin?.Value<double?>("Value"),
                InitialFocusPosition = initial?.Value<double?>("Position"),
                InitialFocusValue = initial?.Value<double?>("Value"),
                PreviousFocusPosition = previous?.Value<double?>("Position"),
                PreviousFocusValue = previous?.Value<double?>("Value"),
                FormattedTimestamp = FormatTimestamp(rawTimestamp),
                CapturedUtc = capturedUtc,
                FormattedDuration = FormatDuration(ReadJsonString(root, "Duration")),
                MeasurePoints = measurePoints,
                HyperbolicMinimumStdError = root.Value<double?>("HyperbolicMinimumStdError"),
                HyperbolicReducedChiSquared = root.Value<double?>("HyperbolicReducedChiSquared"),
                HyperbolicLeaveOneOutStdError = root.Value<double?>("HyperbolicLeaveOneOutStdError"),
                HyperbolicFitModelChosen = root.Value<int?>("HyperbolicFitModelChosen"),
                MaxOutlierRejections = hocusFocus?.Value<int?>("MaxOutlierRejections")
            };
        }

        private static bool? ReadOptionalBool(JToken token, string name) {
            if (token?[name] == null || token[name].Type == JTokenType.Null) {
                return null;
            }

            if (token[name].Type == JTokenType.Boolean) {
                return token.Value<bool>(name);
            }

            return bool.TryParse(token.Value<string>(name), out var parsed) ? parsed : null;
        }

        private static double? ReadR2(JObject r2, string name) {
            if (r2 == null || r2[name] == null) {
                return null;
            }
            if (r2[name].Type == JTokenType.String) {
                var text = r2.Value<string>(name);
                if (string.Equals(text, "NaN", StringComparison.OrdinalIgnoreCase)) {
                    return null;
                }
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) {
                    return double.IsNaN(parsed) ? null : parsed;
                }
                return null;
            }
            var value = r2.Value<double?>(name);
            return value.HasValue && !double.IsNaN(value.Value) ? value : null;
        }

        private static string ReadJsonString(JObject root, string name) {
            var token = root[name];
            if (token == null || token.Type == JTokenType.Null) {
                return null;
            }
            // Newtonsoft auto-parses ISO-8601 strings as DateTime; Value<string> then returns null.
            if (token.Type == JTokenType.Date) {
                return token.ToObject<DateTime>().ToString("O", CultureInfo.InvariantCulture);
            }
            return token.Type == JTokenType.String ? token.Value<string>() : token.ToString();
        }

        private static string FormatTimestamp(string ts) {
            if (string.IsNullOrWhiteSpace(ts) || !ts.Contains('T')) {
                return "N/A";
            }
            var parts = ts.Split('T');
            var datePart = parts[0];
            var timePart = parts[1];
            var timeCore = timePart.Split('.')[0];
            // Strip timezone markers from time-only segment for display
            foreach (var sep in new[] { "+", "-" }) {
                var idx = timeCore.IndexOf(sep, StringComparison.Ordinal);
                // timezone offset only appears after HH:mm:ss; ignore minus in date
                if (idx > 0) {
                    timeCore = timeCore.Substring(0, idx);
                }
            }
            var frac = string.Empty;
            if (timePart.Contains('.')) {
                var afterDot = timePart.Split('.')[1];
                var digits = new string(afterDot.TakeWhile(char.IsDigit).ToArray());
                if (digits.Length > 0) {
                    frac = "." + (digits.Length >= 2 ? digits.Substring(0, 2) : digits);
                }
            }
            return $"{datePart} {timeCore}{frac}";
        }

        private static string FormatDuration(string dur) {
            if (string.IsNullOrWhiteSpace(dur) || !dur.Contains(':')) {
                return "N/A";
            }
            var parts = dur.Split(':');
            if (parts.Length != 3) {
                return dur;
            }
            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)) {
                return dur;
            }
            // Match bot style: 00:02:11.38
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1}:{2:00.00}",
                parts[0].PadLeft(2, '0'),
                parts[1].PadLeft(2, '0'),
                seconds);
        }

        /// <summary>
        /// Compact timestamp for session digest lines (no fractional seconds).
        /// Falls back to the time encoded in NINA's filename when JSON Timestamp is missing.
        /// </summary>
        public string FormatDigestTimestamp() {
            var full = FormattedTimestamp;
            if (string.IsNullOrWhiteSpace(full) || full == "N/A") {
                return FormatShortFileName();
            }
            // "2026-07-03 20:00:01.00" -> "2026-07-03 20:00:01"
            var dot = full.LastIndexOf('.');
            if (dot > 0 && full.IndexOf(' ', StringComparison.Ordinal) < dot) {
                return full.Substring(0, dot);
            }
            return full;
        }

        /// <summary>
        /// NINA filenames: yyyy-MM-dd--HH-mm-ss--{profileId}.json → yyyy-MM-dd--HH-mm-ss
        /// </summary>
        public string FormatShortFileName() {
            var name = Path.GetFileName(FileName ?? string.Empty);
            if (string.IsNullOrEmpty(name)) {
                return "N/A";
            }

            var parts = name.Split(new[] { "--" }, StringSplitOptions.None);
            if (parts.Length >= 2 && parts[0].Length == 10) {
                return $"{parts[0]}--{parts[1]}";
            }

            return name;
        }

        /// <summary>
        /// Compact one-line label for Discord content (date/time from filename, no profile GUID).
        /// </summary>
        public string FormatTruncatedFileName(int maxLength = 44) {
            var shortName = FormatShortFileName();
            if (!string.IsNullOrEmpty(shortName) && shortName != "N/A" && shortName.Length <= maxLength) {
                return shortName;
            }

            var name = Path.GetFileName(FileName ?? string.Empty);
            if (string.IsNullOrEmpty(name) || name.Length <= maxLength) {
                return string.IsNullOrEmpty(name) ? "N/A" : name;
            }

            const string ellipsis = "…";
            if (maxLength <= ellipsis.Length + 4) {
                return name.Substring(0, maxLength);
            }

            var keepStart = Math.Max(12, (maxLength - ellipsis.Length) * 2 / 3);
            var keepEnd = maxLength - ellipsis.Length - keepStart;
            if (keepEnd < 4) {
                keepStart = maxLength - ellipsis.Length - 4;
                keepEnd = 4;
            }

            return name.Substring(0, keepStart) + ellipsis + name.Substring(name.Length - keepEnd);
        }

        public string FormatFullFileName() => Path.GetFileName(FileName ?? string.Empty);

        public string FormatR2(double? value) => value.HasValue ? value.Value.ToString("0.00", CultureInfo.InvariantCulture) : "N/A";

        public string FormatTemperature() =>
            Temperature.HasValue ? Temperature.Value.ToString("0.00", CultureInfo.InvariantCulture) : "N/A";

        public string FormatFinalHfr() => FinalHfr.ToString("0.00", CultureInfo.InvariantCulture);

        public string FormatCalculatedPosition() =>
            CalculatedPosition.HasValue ? ((int)CalculatedPosition.Value).ToString(CultureInfo.InvariantCulture) : "N/A";

        /// <summary>Focus steps moved from the previous AF position to the calculated result.</summary>
        public int? FocusDeltaFromPrevious(double? sessionFallbackPreviousPosition = null) {
            var previous = ResolvePreviousFocusPosition(sessionFallbackPreviousPosition);
            if (!CalculatedPosition.HasValue || !previous.HasValue) {
                return null;
            }
            return (int)Math.Round(CalculatedPosition.Value - previous.Value);
        }

        /// <summary>
        /// Best available previous focus position: JSON PreviousFocusPoint, or prior session run when JSON matches current.
        /// </summary>
        public double? ResolvePreviousFocusPosition(double? sessionFallbackPreviousPosition) {
            if (!CalculatedPosition.HasValue) {
                return PreviousFocusPosition ?? sessionFallbackPreviousPosition;
            }

            var calc = CalculatedPosition.Value;
            if (PreviousFocusPosition is { } jsonPrevious && Math.Abs(jsonPrevious - calc) >= 0.5) {
                return jsonPrevious;
            }

            if (sessionFallbackPreviousPosition is { } sessionPrevious && Math.Abs(sessionPrevious - calc) >= 0.5) {
                return sessionPrevious;
            }

            return PreviousFocusPosition ?? sessionFallbackPreviousPosition;
        }

        /// <summary>Steps between hyperbolic-fit minimum and calculated focus (when both exist).</summary>
        public int? HyperbolicFitDeltaSteps() {
            if (!CalculatedPosition.HasValue || !HyperbolicMinimumPosition.HasValue) {
                return null;
            }
            return (int)Math.Round(HyperbolicMinimumPosition.Value - CalculatedPosition.Value);
        }

        /// <summary>Steps between parabolic-fit minimum and calculated focus (when both exist).</summary>
        public int? ParabolicFitDeltaSteps() {
            if (!CalculatedPosition.HasValue || !ParabolicMinimumPosition.HasValue) {
                return null;
            }
            return (int)Math.Round(ParabolicMinimumPosition.Value - CalculatedPosition.Value);
        }

        public bool UsesParabolicRunFit() {
            return string.Equals(Fitting, "PARABOLIC", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(Fitting, "QUADRATIC", StringComparison.OrdinalIgnoreCase);
        }

        public bool UsesHyperbolicRunFit() {
            return string.Equals(Fitting, "HYPERBOLIC", StringComparison.OrdinalIgnoreCase);
        }

        public double? PrimaryFitR2(bool preferParabolicOnGraph, bool preferHyperbolicOnGraph) {
            if (preferParabolicOnGraph && !preferHyperbolicOnGraph) {
                return R2Parabolic ?? R2Hyperbolic;
            }

            if (preferHyperbolicOnGraph && !preferParabolicOnGraph) {
                return R2Hyperbolic ?? R2Parabolic;
            }

            if (UsesParabolicRunFit()) {
                return R2Parabolic ?? R2Hyperbolic;
            }

            if (UsesHyperbolicRunFit()) {
                return R2Hyperbolic ?? R2Parabolic;
            }

            return R2Hyperbolic ?? R2Parabolic;
        }

        public string PrimaryFitLabel(bool preferParabolicOnGraph, bool preferHyperbolicOnGraph) {
            if (preferParabolicOnGraph && !preferHyperbolicOnGraph) {
                return "Parabolic";
            }

            if (preferHyperbolicOnGraph && !preferParabolicOnGraph) {
                return "Hyperbolic";
            }

            if (UsesParabolicRunFit()) {
                return "Parabolic";
            }

            if (UsesHyperbolicRunFit()) {
                return "Hyperbolic";
            }

            return R2Parabolic.HasValue && !R2Hyperbolic.HasValue ? "Parabolic" : "Hyperbolic";
        }

        public string FormatGraphDuration() {
            var dur = FormattedDuration;
            if (string.IsNullOrWhiteSpace(dur) || dur == "N/A" || !dur.Contains(':')) {
                return dur ?? "N/A";
            }
            var parts = dur.Split(':');
            if (parts.Length != 3) {
                return dur;
            }
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours)) {
                return dur;
            }
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes)) {
                return dur;
            }
            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)) {
                return dur;
            }
            var totalSeconds = (int)Math.Round(hours * 3600 + minutes * 60 + seconds);
            if (totalSeconds < 60) {
                return $"{totalSeconds}s";
            }
            if (hours > 0) {
                minutes += hours * 60;
            }
            var remSec = totalSeconds % 60;
            return remSec > 0 ? $"{minutes}m {remSec}s" : $"{minutes}m";
        }
    }
}
