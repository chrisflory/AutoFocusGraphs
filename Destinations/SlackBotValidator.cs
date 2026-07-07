using System;
using System.Text.RegularExpressions;

namespace AutofocusGraphs.Destinations {
    internal static class SlackBotValidator {
        private static readonly Regex TokenPattern = new Regex(
            @"^xox[baprs]-[A-Za-z0-9-]+$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex ChannelPattern = new Regex(
            @"^[CG][A-Z0-9]+$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static bool TryValidateToken(string token, out string error) {
            token = (token ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(token)) {
                error = "Slack bot token is required.";
                return false;
            }

            if (!TokenPattern.IsMatch(token)) {
                error = "Slack bot token format looks invalid (expected xoxb-... from your Slack app).";
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryValidateChannelId(string channelId, out string error) {
            channelId = (channelId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(channelId)) {
                error = "Slack channel ID is required.";
                return false;
            }

            if (!ChannelPattern.IsMatch(channelId)) {
                error = "Slack channel ID must look like C0123456789 or G0123456789.";
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryValidate(string token, string channelId, out string error) {
            if (!TryValidateToken(token, out error)) {
                return false;
            }

            return TryValidateChannelId(channelId, out error);
        }
    }
}
