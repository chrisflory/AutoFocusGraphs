using AutoFocusGraphs.Properties;
using System;
using System.Text.RegularExpressions;
using Settings = AutoFocusGraphs.Properties.Settings;

namespace AutoFocusGraphs {
    internal static class DiscordRolePing {
        private static readonly Regex RoleIdPattern = new Regex(@"^\d{17,20}$", RegexOptions.Compiled);

        public static string FormatPrefix(ReportOutcome outcome) {
            if (!ShouldPing(outcome)) {
                return string.Empty;
            }

            var roleId = (Settings.Default.DiscordAlertRoleId ?? string.Empty).Trim();
            if (!RoleIdPattern.IsMatch(roleId)) {
                return string.Empty;
            }

            return $"<@&{roleId}> ";
        }

        public static string ApplyToContent(string content, ReportOutcome outcome) {
            var prefix = FormatPrefix(outcome);
            if (string.IsNullOrEmpty(prefix)) {
                return content ?? string.Empty;
            }

            var body = content ?? string.Empty;
            if (body.StartsWith(prefix, StringComparison.Ordinal)) {
                return body;
            }

            return prefix + body;
        }

        private static bool ShouldPing(ReportOutcome outcome) {
            if (string.IsNullOrWhiteSpace(Settings.Default.DiscordAlertRoleId)) {
                return false;
            }

            if (outcome == ReportOutcome.Failure) {
                return Settings.Default.PingRoleOnFailure;
            }

            if (outcome == ReportOutcome.Warning) {
                return Settings.Default.PingRoleOnWarning;
            }

            return false;
        }
    }
}
