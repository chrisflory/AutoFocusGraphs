using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AutoFocusGraphs.Destinations {
    internal static class SlackBotClient {
        private static readonly HttpClient Http = new HttpClient {
            Timeout = TimeSpan.FromSeconds(90)
        };

        public static Task SendTestAsync(string token, string channelId, CancellationToken tokenCt) =>
            PostMessageAsync(token, channelId, "AutoFocusGraphs test — Slack delivery is configured.", tokenCt);

        public static async Task SendReportAsync(
            string token,
            string channelId,
            byte[] graphPng,
            string message,
            bool attachJson,
            string jsonFilePath,
            string jsonFileName,
            CancellationToken tokenCt) {
            var uploads = new List<(byte[] Bytes, string FileName)>();
            if (graphPng != null && graphPng.Length > 0) {
                uploads.Add((graphPng, "autofocus_curve.png"));
            }

            if (attachJson && !string.IsNullOrWhiteSpace(jsonFilePath) && File.Exists(jsonFilePath)) {
                var bytes = await File.ReadAllBytesAsync(jsonFilePath, tokenCt).ConfigureAwait(false);
                uploads.Add((bytes, SanitizeFileName(jsonFileName ?? Path.GetFileName(jsonFilePath))));
            }

            if (uploads.Count > 0) {
                await UploadFilesV2Async(token, channelId, uploads, message, tokenCt).ConfigureAwait(false);
            } else if (!string.IsNullOrWhiteSpace(message)) {
                await PostMessageAsync(token, channelId, message, tokenCt).ConfigureAwait(false);
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
                await UploadFilesV2Async(
                    token,
                    channelId,
                    new[] { (chartPng, "autofocus_digest.png") },
                    caption,
                    tokenCt).ConfigureAwait(false);
            } else {
                await PostMessageAsync(token, channelId, caption, tokenCt).ConfigureAwait(false);
            }
        }

        private static async Task UploadFilesV2Async(
            string token,
            string channelId,
            IReadOnlyList<(byte[] Bytes, string FileName)> files,
            string initialComment,
            CancellationToken tokenCt) {
            var uploaded = new List<(string Id, string Title)>();
            foreach (var file in files) {
                if (file.Bytes == null || file.Bytes.Length == 0) {
                    continue;
                }

                var safeName = SanitizeFileName(file.FileName);
                var (uploadUrl, fileId) = await GetUploadUrlAsync(token, safeName, file.Bytes.Length, tokenCt).ConfigureAwait(false);
                await UploadBytesToExternalUrlAsync(uploadUrl, file.Bytes, safeName, tokenCt).ConfigureAwait(false);
                uploaded.Add((fileId, safeName));
            }

            if (uploaded.Count == 0) {
                if (!string.IsNullOrWhiteSpace(initialComment)) {
                    await PostMessageAsync(token, channelId, initialComment, tokenCt).ConfigureAwait(false);
                }
                return;
            }

            var filesJson = new JArray();
            foreach (var entry in uploaded) {
                filesJson.Add(new JObject {
                    ["id"] = entry.Id,
                    ["title"] = entry.Title,
                });
            }

            var form = new Dictionary<string, string> {
                ["token"] = token,
                ["channel_id"] = channelId,
                ["files"] = filesJson.ToString(Formatting.None),
            };
            if (!string.IsNullOrWhiteSpace(initialComment)) {
                form["initial_comment"] = TrimText(initialComment);
            }

            await PostSlackApiAsync("files.completeUploadExternal", form, null, tokenCt).ConfigureAwait(false);
        }

        private static async Task<(string UploadUrl, string FileId)> GetUploadUrlAsync(
            string token,
            string fileName,
            int length,
            CancellationToken tokenCt) {
            var form = new Dictionary<string, string> {
                ["token"] = token,
                ["filename"] = fileName,
                ["length"] = length.ToString(),
            };

            var body = await PostSlackApiRawAsync("files.getUploadURLExternal", form, null, tokenCt).ConfigureAwait(false);
            var json = JObject.Parse(body);
            var uploadUrl = json["upload_url"]?.ToString();
            var fileId = json["file_id"]?.ToString();
            if (string.IsNullOrWhiteSpace(uploadUrl) || string.IsNullOrWhiteSpace(fileId)) {
                throw new InvalidOperationException(FormatSlackError(body, "Slack upload URL response was incomplete."));
            }

            return (uploadUrl, fileId);
        }

        private static async Task UploadBytesToExternalUrlAsync(
            string uploadUrl,
            byte[] bytes,
            string fileName,
            CancellationToken tokenCt) {
            if (!HttpsFetchUrlValidator.TryValidateSlackUploadUrl(uploadUrl, out var urlError)) {
                throw new InvalidOperationException($"Slack upload URL rejected: {urlError}");
            }

            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(bytes), "file", fileName);
            using var response = await Http.PostAsync(uploadUrl, content, tokenCt).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) {
                var body = await response.Content.ReadAsStringAsync(tokenCt).ConfigureAwait(false);
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(body)
                        ? $"Slack file upload failed ({(int)response.StatusCode})."
                        : body);
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

        private static Task PostSlackApiAsync(
            string method,
            Dictionary<string, string> formFields,
            HttpContent multipartContent,
            CancellationToken tokenCt) =>
            PostSlackApiRawAsync(method, formFields, multipartContent, tokenCt);

        private static async Task<string> PostSlackApiRawAsync(
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
                throw new InvalidOperationException($"Slack returned an unreadable response: {ex.Message}");
            }

            return body;
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
                        "method_deprecated" => "Slack rejected the legacy file upload API. Update AutoFocusGraphs to the latest build.",
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
