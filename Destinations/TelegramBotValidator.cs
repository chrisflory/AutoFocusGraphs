using System;
using System.Text.RegularExpressions;

namespace AutoFocusGraphs.Destinations {
    internal static class TelegramBotValidator {
        private static readonly Regex TokenPattern = new Regex(
            @"^\d{8,10}:[A-Za-z0-9_-]{30,}$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static bool TryValidateToken(string token, out string error) {
            token = (token ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(token)) {
                error = "Telegram bot token is required.";
                return false;
            }

            if (!TokenPattern.IsMatch(token)) {
                error = "Telegram bot token format looks invalid (expected 123456789:ABC... from @BotFather).";
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryValidateChatId(string chatId, out string error) {
            chatId = (chatId ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(chatId)) {
                error = "Telegram chat ID is required.";
                return false;
            }

            if (chatId.StartsWith("@", StringComparison.Ordinal)) {
                if (chatId.Length < 2) {
                    error = "Telegram channel username looks invalid.";
                    return false;
                }

                error = null;
                return true;
            }

            if (long.TryParse(chatId, out var numeric) && numeric != 0) {
                error = null;
                return true;
            }

            error = "Telegram chat ID must be numeric (e.g. -1001234567890) or @channelusername.";
            return false;
        }

        public static bool TryValidate(string token, string chatId, out string error) {
            if (!TryValidateToken(token, out error)) {
                return false;
            }

            return TryValidateChatId(chatId, out error);
        }
    }
}
