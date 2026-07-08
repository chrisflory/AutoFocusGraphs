using System;

namespace AutoFocusGraphs.Destinations {
    internal static class ReportMessageFormatter {
        public static string BuildReportMessage(AutofocusReport report, string messageTemplate, QualityResult quality) {
            var template = string.IsNullOrWhiteSpace(messageTemplate)
                ? "New autofocus report: **{shortfilename}** ({filter})"
                : messageTemplate;
            var message = template
                .Replace("{prefix}", quality?.ContentPrefix ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{shortfilename}", report?.FormatShortFileName() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{time}", report?.FormatDigestTimestamp() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{filenamefull}", report?.FormatFullFileName() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{filename}", report?.FormatTruncatedFileName() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{filter}", report?.Filter ?? "N/A", StringComparison.OrdinalIgnoreCase);

            if (!template.Contains("{prefix}", StringComparison.OrdinalIgnoreCase) &&
                quality?.Outcome != ReportOutcome.Success) {
                message = $"{quality?.ContentPrefix}: {message}";
            }

            if (!string.IsNullOrWhiteSpace(quality?.Reason)) {
                message += $"\n{quality.Reason}";
            }

            return message;
        }
    }
}
