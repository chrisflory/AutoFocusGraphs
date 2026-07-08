using Newtonsoft.Json.Linq;
using NINA.Core.Utility;
using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoFocusGraphs.Destinations {
    internal static class TelegramBotClient {
        private static readonly HttpClient Http = new HttpClient {
            Timeout = TimeSpan.FromSeconds(60)
        };

        public static async Task SendTestAsync(string token, string chatId, CancellationToken tokenCt) {
            await SendMessageAsync(
                token,
                chatId,
                "AutoFocusGraphs test — Telegram delivery is configured.",
                tokenCt).ConfigureAwait(false);
        }

        public static async Task SendReportAsync(
            string token,
            string chatId,
            byte[] graphPng,
            string caption,
            bool attachJson,
            string jsonFilePath,
            string jsonFileName,
            CancellationToken tokenCt) {
            if (graphPng != null && graphPng.Length > 0) {
                await SendPhotoAsync(token, chatId, graphPng, caption, tokenCt).ConfigureAwait(false);
            } else if (!string.IsNullOrWhiteSpace(caption)) {
                await SendMessageAsync(token, chatId, caption, tokenCt).ConfigureAwait(false);
            }

            if (attachJson && !string.IsNullOrWhiteSpace(jsonFilePath) && File.Exists(jsonFilePath)) {
                var bytes = await File.ReadAllBytesAsync(jsonFilePath, tokenCt).ConfigureAwait(false);
                await SendDocumentAsync(
                    token,
                    chatId,
                    bytes,
                    string.IsNullOrWhiteSpace(jsonFileName) ? Path.GetFileName(jsonFilePath) : jsonFileName,
                    tokenCt).ConfigureAwait(false);
            }
        }

        public static async Task SendFailureAsync(string token, string chatId, string fileName, string reason, CancellationToken tokenCt) {
            var text = $"Autofocus failure: {EscapeHtml(fileName)}\n{EscapeHtml(reason ?? "Unknown error")}";
            await SendMessageAsync(token, chatId, text, tokenCt).ConfigureAwait(false);
        }

        public static async Task SendDigestAsync(
            string token,
            string chatId,
            byte[] chartPng,
            string caption,
            CancellationToken tokenCt) {
            if (chartPng != null && chartPng.Length > 0) {
                await SendPhotoAsync(token, chatId, chartPng, caption, tokenCt).ConfigureAwait(false);
            } else {
                await SendMessageAsync(token, chatId, caption, tokenCt).ConfigureAwait(false);
            }
        }

        private static async Task SendPhotoAsync(string token, string chatId, byte[] png, string caption, CancellationToken tokenCt) {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(chatId), "chat_id");
            content.Add(new ByteArrayContent(png), "photo", "autofocus_curve.png");
            if (!string.IsNullOrWhiteSpace(caption)) {
                content.Add(new StringContent(TrimCaption(caption)), "caption");
                content.Add(new StringContent("HTML"), "parse_mode");
            }

            await PostAsync(token, "sendPhoto", content, tokenCt).ConfigureAwait(false);
        }

        private static async Task SendDocumentAsync(string token, string chatId, byte[] bytes, string fileName, CancellationToken tokenCt) {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(chatId), "chat_id");
            content.Add(new ByteArrayContent(bytes), "document", SanitizeFileName(fileName));
            await PostAsync(token, "sendDocument", content, tokenCt).ConfigureAwait(false);
        }

        private static async Task SendMessageAsync(string token, string chatId, string text, CancellationToken tokenCt) {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(chatId), "chat_id");
            content.Add(new StringContent(TrimCaption(text)), "text");
            content.Add(new StringContent("HTML"), "parse_mode");
            await PostAsync(token, "sendMessage", content, tokenCt).ConfigureAwait(false);
        }

        private static async Task PostAsync(string token, string method, HttpContent content, CancellationToken tokenCt) {
            var url = $"https://api.telegram.org/bot{token}/{method}";
            using var response = await Http.PostAsync(url, content, tokenCt).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(tokenCt).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) {
                throw new InvalidOperationException(FormatTelegramError(body, response.StatusCode.ToString()));
            }

            try {
                var json = JObject.Parse(body);
                if (json["ok"]?.Value<bool>() != true) {
                    throw new InvalidOperationException(FormatTelegramError(body, json["description"]?.ToString()));
                }
            } catch (InvalidOperationException) {
                throw;
            } catch (Exception ex) {
                Logger.Warning($"AutoFocusGraphs: Telegram response parse warning: {ex.Message}");
            }
        }

        private static string FormatTelegramError(string body, string fallback) {
            try {
                var json = JObject.Parse(body);
                var description = json["description"]?.ToString();
                if (!string.IsNullOrWhiteSpace(description)) {
                    return description;
                }
            } catch {
                // ignore parse errors
            }

            return string.IsNullOrWhiteSpace(fallback) ? "Telegram API request failed." : fallback;
        }

        internal static string ConvertDiscordMarkdownToHtml(string text) {
            if (string.IsNullOrEmpty(text)) {
                return string.Empty;
            }

            var sb = new StringBuilder(text.Length + 32);
            for (var i = 0; i < text.Length; i++) {
                if (text[i] == '*' && i + 1 < text.Length && text[i + 1] == '*') {
                    var close = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                    if (close > i + 2) {
                        sb.Append("<b>");
                        sb.Append(EscapeHtml(text.Substring(i + 2, close - i - 2)));
                        sb.Append("</b>");
                        i = close + 1;
                        continue;
                    }
                }

                sb.Append(EscapeHtml(text[i].ToString(CultureInfo.InvariantCulture)));
            }

            return sb.ToString();
        }

        private static string EscapeHtml(string text) {
            if (string.IsNullOrEmpty(text)) {
                return string.Empty;
            }

            return text
                .Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal);
        }

        private static string TrimCaption(string caption) {
            if (string.IsNullOrEmpty(caption)) {
                return string.Empty;
            }

            const int max = 1024;
            return caption.Length <= max ? caption : caption.Substring(0, max - 1) + "…";
        }

        private static string SanitizeFileName(string fileName) {
            foreach (var c in Path.GetInvalidFileNameChars()) {
                fileName = fileName.Replace(c, '_');
            }

            return string.IsNullOrWhiteSpace(fileName) ? "report.json" : fileName;
        }
    }
}
