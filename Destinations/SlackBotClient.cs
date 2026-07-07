using Newtonsoft.Json.Linq;
using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AutofocusGraphs.Destinations {
    internal static class SlackBotClient {
        private static readonly HttpClient Http = new HttpClient {
            Timeout = TimeSpan.FromSeconds(90)
        };

        public static Task SendTestAsync(string token, string channelId, CancellationToken tokenCt) =>
            PostMessageAsync(token, channelId, "AutofocusGraphs test — Slack delivery is configured.", tokenCt);

        public static async Task SendReportAsync(
            string token,
            string channelId,
            byte[] graphPng,
            string message,
            bool attachJson,
            string jsonFilePath,
            string jsonFileName,
            CancellationToken tokenCt) {
            if (graphPng != null && graphPng.Length > 0) {
                await UploadFileAsync(
                    token,
                    channelId,
                    graphPng,
                    "autofocus_curve.png",
                    message,
                    tokenCt).ConfigureAwait(false);
            } else if (!string.IsNullOrWhiteSpace(message)) {
                await PostMessageAsync(token, channelId, message, tokenCt).ConfigureAwait(false);
            }

            if (attachJson && !string.IsNullOrWhiteSpace(jsonFilePath) && File.Exists(jsonFilePath)) {
                var bytes = await File.ReadAllBytesAsync(jsonFilePath, tokenCt).ConfigureAwait(false);
                await UploadFileAsync(
                    token,
                    channelId,
                    bytes,
                    SanitizeFileName(jsonFileName ?? Path.GetFileName(jsonFilePath)),
                    null,
                    tokenCt).ConfigureAwait(false);
            }
        }

        public static Task SendFailureAsync(string token, string channelId, string fileName, string reason, CancellationToken tokenCt) {
            var text = $"*Autofocus failure:* `{EscapeInline(fileName)}`\n{reason ?? "Unknown error"}";
            return PostMessageAsync(token, channelId, text, tokenCt);
        }

        public static async Task SendDigestAsync(
            string token,
            string channelId,
            byte[] chartPng,
            string caption,
            CancellationToken tokenCt) {
            if (chartPng != null && chartPng.Length > 0) {
                await UploadFileAsync(token, channelId, chartPng, "autofocus_digest.png", caption, tokenCt).ConfigureAwait(false);
            } else {
                await PostMessageAsync(token, channelId, caption, tokenCt).ConfigureAwait(false);
            }
        }

        private static async Task PostMessageAsync(string token, string channelId, string text, CancellationToken tokenCt) {
            var form = new Dictionary<string, string> {
                ["token"] = token,
                ["channel"] = channelId,
                ["text"] = TrimText(text),
                ["mrkdwn"] = "true",
            };

            await PostSlackApiAsync("chat.postMessage", form, null, tokenCt).ConfigureAwait(false);
        }

        private static async Task UploadFileAsync(
            string token,
            string channelId,
            byte[] bytes,
            string fileName,
            string initialComment,
            CancellationToken tokenCt) {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(token), "token");
            content.Add(new StringContent(channelId), "channels");
            content.Add(new StringContent(SanitizeFileName(fileName)), "filename");
            if (!string.IsNullOrWhiteSpace(initialComment)) {
                content.Add(new StringContent(TrimText(initialComment)), "initial_comment");
            }

            content.Add(new ByteArrayContent(bytes), "file", SanitizeFileName(fileName));
            await PostSlackApiAsync("files.upload", null, content, tokenCt).ConfigureAwait(false);
        }

        private static async Task PostSlackApiAsync(
            string method,
            Dictionary<string, string> formFields,
            HttpContent multipartContent,
            CancellationToken tokenCt) {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://slack.com/api/{method}");
            if (multipartContent != null) {
                request.Content = multipartContent;
            } else if (formFields != null) {
                request.Content = new FormUrlEncodedContent(formFields);
            }

            using var response = await Http.SendAsync(request, tokenCt).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(tokenCt).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) {
                throw new InvalidOperationException(FormatSlackError(body, response.ReasonPhrase));
            }

            try {
                var json = JObject.Parse(body);
                if (json["ok"]?.Value<bool>() != true) {
                    throw new InvalidOperationException(FormatSlackError(body, json["error"]?.ToString()));
                }
            } catch (InvalidOperationException) {
                throw;
            } catch (Exception ex) {
                Logger.Warning($"AutofocusGraphs: Slack response parse warning: {ex.Message}");
            }
        }

        internal static string ConvertMarkdownForSlack(string text) {
            if (string.IsNullOrEmpty(text)) {
                return string.Empty;
            }

            return text.Replace("**", "*", StringComparison.Ordinal);
        }

        private static string FormatSlackError(string body, string fallback) {
            try {
                var json = JObject.Parse(body);
                var error = json["error"]?.ToString();
                if (!string.IsNullOrWhiteSpace(error)) {
                    return error switch {
                        "not_in_channel" => "Slack bot is not in that channel. Invite the bot first.",
                        "channel_not_found" => "Slack channel ID was not found.",
                        "invalid_auth" => "Slack bot token is invalid or revoked.",
                        "missing_scope" => "Slack bot token is missing required scopes (chat:write, files:write).",
                        _ => error,
                    };
                }
            } catch {
                // ignore parse errors
            }

            return string.IsNullOrWhiteSpace(fallback) ? "Slack API request failed." : fallback;
        }

        private static string EscapeInline(string text) =>
            (text ?? string.Empty).Replace("`", "'", StringComparison.Ordinal);

        private static string TrimText(string text) {
            if (string.IsNullOrEmpty(text)) {
                return string.Empty;
            }

            const int max = 3900;
            return text.Length <= max ? text : text.Substring(0, max - 1) + "…";
        }

        private static string SanitizeFileName(string fileName) {
            foreach (var c in Path.GetInvalidFileNameChars()) {
                fileName = fileName.Replace(c, '_');
            }

            return string.IsNullOrWhiteSpace(fileName) ? "report.json" : fileName;
        }
    }
}
