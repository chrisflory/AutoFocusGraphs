using System;
using System.IO;
using System.Text.RegularExpressions;

namespace AutoFocusGraphs.Destinations {
    internal static class EmailSubjectFormatter {
        public const string DefaultReportTemplate = "NINA AutoFocus Graphs - {sequence} - {date}";
        private const string SequencePrefix = "NINA AutoFocus Graphs";
        private const string ManualPrefix = "NINA Manual AutoFocus Graphs";

        public static string FormatReportSubject(
            AutofocusReport report,
            QualityResult quality,
            string sequenceName,
            string template) {
            template = UseManualPrefixIfNeeded(NormalizeTemplate(template, DefaultReportTemplate), sequenceName);
            var subject = ApplyReportTokens(template, report, quality, sequenceName);
            return CollapseSubject(subject);
        }

        public static string FormatFailureSubject(string fileName, string reason, string sequenceName, string template) {
            template = UseManualPrefixIfNeeded(
                NormalizeTemplate(template, "NINA AutoFocus Graphs failure - {date}"),
                sequenceName);
            return CollapseSubject(ApplySimpleTokens(
                template,
                fileName,
                reason,
                filter: null,
                sequenceName,
                prefix: null,
                date: DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                time: DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)));
        }

        public static string FormatDigestSubject(string digestLabel, string sequenceName, string template) {
            var fallback = string.Equals(digestLabel, "sequence", StringComparison.OrdinalIgnoreCase)
                ? "NINA AutoFocus Graphs digest - {sequence}"
                : "NINA AutoFocus Graphs session digest - {date}";
            template = NormalizeTemplate(template, fallback);
            return CollapseSubject(ApplySimpleTokens(
                template,
                fileName: null,
                reason: null,
                filter: null,
                sequenceName,
                prefix: null,
                date: DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                time: DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)));
        }

        public static string FormatTestSubject(string template, string sequenceName, DateTime now) {
            template = UseManualPrefixIfNeeded(
                NormalizeTemplate(template, "NINA AutoFocus Graphs test - {date} at {time}"),
                sequenceName);
            var subject = CollapseSubject(ApplySimpleTokens(
                template,
                "test-email.json",
                reason: null,
                filter: null,
                sequenceName,
                prefix: null,
                date: now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                time: now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)));
            if (!template.Contains("{time}", StringComparison.OrdinalIgnoreCase)) {
                subject += $" [{now:HH:mm:ss}]";
            }

            return subject;
        }

        private static string NormalizeTemplate(string template, string fallback) {
            template = (template ?? string.Empty).Trim();
            return string.IsNullOrEmpty(template) ? fallback : template;
        }

        private static string UseManualPrefixIfNeeded(string template, string sequenceName) {
            if (!string.IsNullOrWhiteSpace(FormatSequenceName(sequenceName))) {
                return template;
            }

            return template.Replace(SequencePrefix, ManualPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string ApplyReportTokens(
            string template,
            AutofocusReport report,
            QualityResult quality,
            string sequenceName) {
            var prefix = EmailSmtpClient.StripMarkdown(quality?.ContentPrefix ?? string.Empty);
            return template
                .Replace("{prefix}", prefix, StringComparison.OrdinalIgnoreCase)
                .Replace("{reason}", quality?.Reason ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{shortfilename}", report?.FormatShortFileName() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{time}", report?.FormatDigestTimestamp() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{date}", report?.FormatSubjectDate() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{filenamefull}", report?.FormatFullFileName() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{filename}", report?.FormatTruncatedFileName() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{filter}", report?.Filter ?? "N/A", StringComparison.OrdinalIgnoreCase)
                .Replace("{sequence}", FormatSequenceName(sequenceName), StringComparison.OrdinalIgnoreCase);
        }

        private static string ApplySimpleTokens(
            string template,
            string fileName,
            string reason,
            string filter,
            string sequenceName,
            string prefix,
            string date,
            string time) {
            var shortName = string.IsNullOrWhiteSpace(fileName) ? string.Empty : Path.GetFileName(fileName);
            return template
                .Replace("{prefix}", prefix ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{reason}", reason ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{shortfilename}", shortName, StringComparison.OrdinalIgnoreCase)
                .Replace("{filename}", fileName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{filenamefull}", fileName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{filter}", filter ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{sequence}", FormatSequenceName(sequenceName), StringComparison.OrdinalIgnoreCase)
                .Replace("{date}", date ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{time}", time ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatSequenceName(string sequenceName) {
            if (string.IsNullOrWhiteSpace(sequenceName)) {
                return string.Empty;
            }

            sequenceName = sequenceName.Trim();
            if (sequenceName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                sequenceName.EndsWith(".nina", StringComparison.OrdinalIgnoreCase)) {
                sequenceName = Path.GetFileNameWithoutExtension(sequenceName);
            }

            return sequenceName;
        }

        private static string CollapseSubject(string subject) {
            subject = (subject ?? string.Empty).Trim();
            subject = Regex.Replace(subject, @"\s{2,}", " ");
            while (subject.Contains(" -  - ", StringComparison.Ordinal)) {
                subject = subject.Replace(" -  - ", " - ", StringComparison.Ordinal);
            }

            subject = Regex.Replace(subject, @"\s-\s-\s", " - ");
            subject = Regex.Replace(subject, @"^\s*-\s*", string.Empty);
            subject = Regex.Replace(subject, @"\s*-\s*$", string.Empty);
            return subject.Trim();
        }
    }
}
