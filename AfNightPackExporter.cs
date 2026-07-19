using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using Settings = AutoFocusGraphs.Properties.Settings;

namespace AutoFocusGraphs {
    /// <summary>
    /// Builds a zip of session AF V-curve PNGs, trend/drift charts, CSV, README, and original JSON for forums/support.
    /// </summary>
    internal static class AfNightPackExporter {
        internal sealed class Result {
            public string ZipPath { get; init; }
            public int RunCount { get; init; }
            public int GraphCount { get; init; }
            public int JsonCount { get; init; }
            public bool HasTrend { get; init; }
            public bool HasDrift { get; init; }
        }

        public static string SuggestFileName(DateTime? localDate = null) {
            var day = localDate ?? DateTime.Now;
            return $"AutoFocusGraphs-night-pack-{day:yyyy-MM-dd}.zip";
        }

        public static string GetPluginVersion() {
            try {
                return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            } catch {
                return "unknown";
            }
        }

        public static Result Export(string zipPath, IReadOnlyList<AutofocusReport> reports) {
            if (string.IsNullOrWhiteSpace(zipPath)) {
                throw new ArgumentException("Zip path is required.", nameof(zipPath));
            }

            if (reports == null || reports.Count == 0) {
                throw new InvalidOperationException("No autofocus reports found for a night pack.");
            }

            var ordered = reports
                .Where(r => r != null)
                .OrderBy(r => r.FormattedTimestamp, StringComparer.Ordinal)
                .ThenBy(r => r.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (ordered.Count == 0) {
                throw new InvalidOperationException("No autofocus reports found for a night pack.");
            }

            var packRoot = Path.GetFileNameWithoutExtension(zipPath);
            if (string.IsNullOrWhiteSpace(packRoot)) {
                packRoot = Path.GetFileNameWithoutExtension(SuggestFileName());
            }

            var options = PluginRuntimeOptions.FromSettings();
            var maxRuns = Math.Max(5, Settings.Default.DigestTrendMaxRuns);
            var version = GetPluginVersion();
            var graphCount = 0;
            var jsonCount = 0;
            var hasTrend = false;
            var hasDrift = false;

            var directory = Path.GetDirectoryName(zipPath);
            if (!string.IsNullOrEmpty(directory)) {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(zipPath)) {
                File.Delete(zipPath);
            }

            using (var zipStream = new FileStream(zipPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create)) {
                WriteEntryText(archive, $"{packRoot}/README.txt", BuildReadme(ordered, version, packRoot));
                WriteEntryText(archive, $"{packRoot}/runs.csv", BuildCsv(ordered));

                for (var i = 0; i < ordered.Count; i++) {
                    var report = ordered[i];
                    var graphName = BuildGraphFileName(i + 1, report);
                    try {
                        var png = RenderGraphPng(report, options);
                        if (png != null && png.Length > 0) {
                            WriteEntryBytes(archive, $"{packRoot}/graphs/{graphName}", png);
                            graphCount++;
                        }
                    } catch (Exception ex) {
                        Logger.Warning($"AutoFocusGraphs: night pack graph failed for {report.FileName}: {ex.Message}");
                    }

                    var jsonCopied = TryCopyJson(archive, $"{packRoot}/json", report);
                    if (jsonCopied) {
                        jsonCount++;
                    }
                }

                try {
                    var charts = DigestChartBuilder.TryBuild(ordered, maxRuns);
                    if (charts.HasTrend) {
                        WriteEntryBytes(archive, $"{packRoot}/charts/trend.png", charts.TrendPng);
                        hasTrend = true;
                    }
                    if (charts.HasDrift) {
                        WriteEntryBytes(archive, $"{packRoot}/charts/focus-drift.png", charts.DriftPng);
                        hasDrift = true;
                    }
                } catch (Exception ex) {
                    Logger.Warning($"AutoFocusGraphs: night pack session charts failed: {ex.Message}");
                }
            }

            return new Result {
                ZipPath = zipPath,
                RunCount = ordered.Count,
                GraphCount = graphCount,
                JsonCount = jsonCount,
                HasTrend = hasTrend,
                HasDrift = hasDrift
            };
        }

        private static byte[] RenderGraphPng(AutofocusReport report, PluginRuntimeOptions options) =>
            AutofocusGraphGenerator.CreatePng(
                report,
                options.ShowHyperbolicFit,
                options.ShowParabolicFit,
                options.ShowTrendLines,
                options.ShowFocusPositionLine,
                options.ShowFilterInGraphTitle,
                options.LabelTrendSegments,
                options.MinimalGraphMode,
                options.ShowMeasurePointLabels,
                options.ShowGraphContextStrip,
                options.ShowPreviousFocusMarker,
                options.ShowTrendR2InLegend,
                options.ShowInitialFocusMarker,
                options.ShowMeasurePointErrorBars,
                options.ShowFitDisagreementWarning,
                options.ShowGraphAnalysisHints,
                options.ConservativeGraphHints,
                options.MinR2,
                options.MaxFinalHfr,
                options.ShowCompareToLastCurve);

        private static string BuildCsv(IReadOnlyList<AutofocusReport> reports) {
            var sb = new StringBuilder();
            sb.AppendLine("index,timestamp,filter,final_hfr,position,temperature,step_size,r2_hyperbolic,r2_parabolic,duration,filename,source_path");
            for (var i = 0; i < reports.Count; i++) {
                var r = reports[i];
                sb.Append(i + 1).Append(',')
                    .Append(Csv(r.FormattedTimestamp)).Append(',')
                    .Append(Csv(r.Filter)).Append(',')
                    .Append(Fmt(r.FinalHfr)).Append(',')
                    .Append(FmtNullable(r.CalculatedPosition)).Append(',')
                    .Append(FmtNullable(r.Temperature)).Append(',')
                    .Append(r.StepSize.HasValue ? r.StepSize.Value.ToString(CultureInfo.InvariantCulture) : "").Append(',')
                    .Append(FmtNullable(r.R2Hyperbolic)).Append(',')
                    .Append(FmtNullable(r.R2Parabolic)).Append(',')
                    .Append(Csv(r.FormattedDuration)).Append(',')
                    .Append(Csv(r.FileName)).Append(',')
                    .Append(Csv(r.SourcePath))
                    .AppendLine();
            }

            return sb.ToString();
        }

        private static string BuildReadme(IReadOnlyList<AutofocusReport> reports, string version, string packRoot) {
            var filters = reports
                .Select(r => r.Filter)
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var first = reports[0].FormattedTimestamp;
            var last = reports[reports.Count - 1].FormattedTimestamp;
            var sb = new StringBuilder();
            sb.AppendLine("AutoFocusGraphs — AF night pack");
            sb.AppendLine("================================");
            sb.AppendLine();
            sb.AppendLine($"Plugin version: {version}");
            sb.AppendLine($"Pack: {packRoot}");
            sb.AppendLine($"Exported (local): {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"Runs: {reports.Count}");
            sb.AppendLine($"Time range: {first} → {last}");
            sb.AppendLine($"Filters: {(filters.Count == 0 ? "(none)" : string.Join(", ", filters))}");
            sb.AppendLine();
            sb.AppendLine("Contents");
            sb.AppendLine("--------");
            sb.AppendLine("runs.csv              One row per autofocus run (summary metrics)");
            sb.AppendLine("graphs/               V-curve PNG for each run (current overlay settings)");
            sb.AppendLine("charts/trend.png      Session HFR trend (when enough runs)");
            sb.AppendLine("charts/focus-drift.png Focus position vs temperature (when enough runs)");
            sb.AppendLine("json/                 Original NINA autofocus JSON when still on disk");
            sb.AppendLine();
            sb.AppendLine("Forum / support tips");
            sb.AppendLine("-------------------");
            sb.AppendLine("- Attach this zip (or graphs/ + runs.csv) when asking about focus issues.");
            sb.AppendLine("- Include the specific graph PNG and matching json/ file for the problem run.");
            sb.AppendLine("- Graph overlays and drift chart toggles match your AutoFocusGraphs options at export time.");
            sb.AppendLine();
            sb.AppendLine("https://github.com/chrisflory/AutoFocusGraphs");
            return sb.ToString();
        }

        private static string BuildGraphFileName(int index, AutofocusReport report) {
            var filter = SanitizeFilePart(report.Filter);
            if (string.IsNullOrWhiteSpace(filter) ||
                string.Equals(filter, "N_A", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(filter, "NA", StringComparison.OrdinalIgnoreCase)) {
                filter = "Filter";
            }

            var stamp = SanitizeFilePart(report.FormattedTimestamp);
            if (string.IsNullOrWhiteSpace(stamp)) {
                stamp = SanitizeFilePart(Path.GetFileNameWithoutExtension(report.FileName));
            }

            if (string.IsNullOrWhiteSpace(stamp)) {
                stamp = "run";
            }

            return $"{index:000}_{filter}_{stamp}.png";
        }

        private static bool TryCopyJson(ZipArchive archive, string jsonFolder, AutofocusReport report) {
            try {
                var path = report.SourcePath;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
                    return false;
                }

                var name = Path.GetFileName(path);
                if (string.IsNullOrWhiteSpace(name)) {
                    name = report.FileName;
                }

                if (string.IsNullOrWhiteSpace(name)) {
                    return false;
                }

                name = SanitizeFilePart(Path.GetFileNameWithoutExtension(name)) + ".json";
                var entry = archive.CreateEntry($"{jsonFolder}/{name}", CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                using var fileStream = File.OpenRead(path);
                fileStream.CopyTo(entryStream);
                return true;
            } catch (Exception ex) {
                Logger.Warning($"AutoFocusGraphs: night pack JSON copy failed for {report.FileName}: {ex.Message}");
                return false;
            }
        }

        private static void WriteEntryText(ZipArchive archive, string entryName, string text) {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(text);
        }

        private static void WriteEntryBytes(ZipArchive archive, string entryName, byte[] bytes) {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var stream = entry.Open();
            stream.Write(bytes, 0, bytes.Length);
        }

        private static string SanitizeFilePart(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return string.Empty;
            }

            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.Trim().Select(c => invalid.Contains(c) || c == ' ' ? '_' : c).ToArray();
            var cleaned = new string(chars);
            while (cleaned.Contains("__")) {
                cleaned = cleaned.Replace("__", "_");
            }

            return cleaned.Trim('_');
        }

        private static string Csv(string value) {
            if (value == null) {
                return "";
            }

            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0) {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }

        private static string Fmt(double value) =>
            value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string FmtNullable(double? value) =>
            value.HasValue ? Fmt(value.Value) : "";
    }
}
