using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoFocusGraphs {
    /// <summary>
    /// Posts autofocus reports to a Discord webhook (graph + embed).
    /// </summary>
    internal static class DiscordWebhookClient {
        private static readonly HttpClient Http = new HttpClient {
            Timeout = TimeSpan.FromSeconds(60)
        };

        private static readonly SemaphoreSlim WebhookGate = new SemaphoreSlim(1, 1);

        /// <summary>Discord can show the previous/default icon if we POST immediately after PATCH.</summary>
        private static readonly TimeSpan ProfileSettleDelay = TimeSpan.FromMilliseconds(600);

        public static async Task UploadReportAsync(
            string webhookUrl,
            AutofocusReport report,
            byte[] graphPng,
            string messageTemplate,
            bool attachJson,
            string jsonFilePath,
            QualityResult quality,
            DiscordPostOptions postOptions,
            CancellationToken token) {
            postOptions ??= DiscordPostOptions.FromSettings();
            if (!WebhookUrlValidator.TryValidate(webhookUrl, out _, out var error)) {
                throw new InvalidOperationException(error);
            }

            var includeGraph = postOptions.AttachMode != AttachContentMode.EmbedOnly
                               && graphPng != null
                               && graphPng.Length > 0;
            var includeEmbed = postOptions.AttachMode != AttachContentMode.GraphOnly;
            var message = BuildReportMessage(report, messageTemplate, quality);

            try {
                await RunSerializedWebhookAsync(async () => {
                    await EnsureWebhookProfileAsync(webhookUrl, postOptions, token, throwOnError: false).ConfigureAwait(false);
                    await PostPayloadWithForumFallbackAsync(
                        webhookUrl,
                        postOptions,
                        opts => BuildReportPayload(report, message, includeGraph, attachJson, quality, opts, includeEmbed),
                        includeGraph ? graphPng : null,
                        attachJson,
                        jsonFilePath,
                        report?.FileName,
                        "autofocus_curve.png",
                        token).ConfigureAwait(false);
                }).ConfigureAwait(false);
                PostStatusTracker.RecordSuccess($"{quality.Outcome} {SanitizeFileName(report.FileName)}");
                Logger.Info($"AutoFocusGraphs: posted report for {SanitizeFileName(report.FileName)} ({quality.Outcome}, graph={(includeGraph ? "yes" : "no")})");
            } catch (Exception ex) {
                var friendly = FormatDiscordError(ex.Message);
                PostStatusTracker.RecordFailure(friendly);
                throw new InvalidOperationException(friendly, ex);
            }
        }

        public static async Task UploadTestAsync(string webhookUrl, DiscordPostOptions postOptions, CancellationToken token) {
            // Test the channel webhook only — do not send forum thread_name (that fails on text channels).
            postOptions = (postOptions ?? DiscordPostOptions.FromSettings()).WithoutNightlyThread();
            if (!WebhookUrlValidator.TryValidate(webhookUrl, out var safeUrl, out var error)) {
                throw new InvalidOperationException(error);
            }

            try {
                await RunSerializedWebhookAsync(async () => {
                    // Name/avatar live on the webhook profile only — never per-message overrides.
                    await EnsureWebhookProfileAsync(webhookUrl, postOptions, token, throwOnError: true).ConfigureAwait(false);

                    var embed = new JObject {
                        ["title"] = "Webhook test successful",
                        ["description"] = "Autofocus can post to this channel.",
                        ["color"] = 0x2ECC71,
                        ["timestamp"] = DateTime.UtcNow.ToString("o")
                    };

                    var root = ApplyIdentity(new JObject {
                        ["content"] = "Autofocus webhook test",
                        ["embeds"] = new JArray { embed }
                    }, postOptions);

                    await PostJsonAsync(safeUrl, root.ToString(Formatting.None), token).ConfigureAwait(false);
                }).ConfigureAwait(false);
                PostStatusTracker.RecordSuccess("webhook test");
                Logger.Info("AutoFocusGraphs: posted webhook test message");
            } catch (Exception ex) {
                var friendly = FormatDiscordError(ex.Message);
                PostStatusTracker.RecordFailure(friendly);
                throw new InvalidOperationException(friendly, ex);
            }
        }

        /// <summary>
        /// The webhook's own profile (name/icon set in Discord) is the source of truth and is left
        /// alone. Only when the webhook has NO icon at all does the plugin PATCH the built-in AF icon
        /// onto it once, so bare webhooks still get an icon. Serialized with posts so the PATCH
        /// settles before the message is sent.
        /// </summary>
        private static async Task EnsureWebhookProfileAsync(
            string webhookUrl,
            DiscordPostOptions postOptions,
            CancellationToken token,
            bool throwOnError) {
            postOptions ??= DiscordPostOptions.FromSettings();
            if (!WebhookUrlValidator.TryValidate(webhookUrl, out var safeUrl, out _)) {
                return;
            }

            var (ok, _, hasAvatar) = await TryGetWebhookProfileAsync(safeUrl, token).ConfigureAwait(false);
            if (!ok || hasAvatar) {
                // Webhook already has an icon (or we can't tell) — never overwrite the user's setup.
                return;
            }

            Logger.Info("AutoFocusGraphs: webhook has no icon; applying the built-in AF icon once.");

            const int maxAttempts = 2;
            Exception lastError = null;
            for (var attempt = 1; attempt <= maxAttempts; attempt++) {
                try {
                    await ApplyWebhookProfileAsync(webhookUrl, postOptions, token).ConfigureAwait(false);
                    await Task.Delay(ProfileSettleDelay, token).ConfigureAwait(false);
                    return;
                } catch (Exception ex) {
                    lastError = ex;
                    if (attempt < maxAttempts) {
                        Logger.Warning($"AutoFocusGraphs: webhook profile update failed, retrying ({ex.Message})");
                        await Task.Delay(TimeSpan.FromMilliseconds(300), token).ConfigureAwait(false);
                    }
                }
            }

            if (throwOnError && lastError != null) {
                throw lastError;
            }
            if (lastError != null) {
                Logger.Warning($"AutoFocusGraphs: could not set webhook icon: {lastError.Message}");
            }
        }

        /// <summary>
        /// GET the webhook object (no auth needed on the webhook URL) to check its current name/avatar.
        /// </summary>
        private static async Task<(bool ok, string name, bool hasAvatar)> TryGetWebhookProfileAsync(
            string safeUrl,
            CancellationToken token) {
            try {
                using var response = await Http.GetAsync(safeUrl, token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) {
                    return (false, null, false);
                }
                var body = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                var obj = JObject.Parse(body);
                return (true, obj.Value<string>("name"), !string.IsNullOrEmpty(obj.Value<string>("avatar")));
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                Logger.Warning($"AutoFocusGraphs: could not read webhook profile: {ex.Message}");
                return (false, null, false);
            }
        }

        private static async Task RunSerializedWebhookAsync(Func<Task> action) {
            await WebhookGate.WaitAsync().ConfigureAwait(false);
            try {
                await action().ConfigureAwait(false);
            } finally {
                WebhookGate.Release();
            }
        }

        /// <summary>
        /// Sets the webhook's default display name and avatar (PATCH).
        /// Messages must not send username/avatar_url overrides — those can hide the icon.
        /// Avatar prefers a custom URL, otherwise the built-in AF icon embedded in the plugin.
        /// </summary>
        public static async Task ApplyWebhookProfileAsync(
            string webhookUrl,
            DiscordPostOptions postOptions,
            CancellationToken token) {
            postOptions ??= DiscordPostOptions.FromSettings();
            if (!WebhookUrlValidator.TryValidate(webhookUrl, out var safeUrl, out var error)) {
                throw new InvalidOperationException(error);
            }

            var body = new JObject();
            if (!string.IsNullOrWhiteSpace(postOptions.Username)) {
                body["name"] = postOptions.Username;
            }

            var (avatarBytes, mediaType) = await LoadAvatarBytesAsync(postOptions.AvatarUrl, token).ConfigureAwait(false);
            if (avatarBytes != null && avatarBytes.Length > 0) {
                body["avatar"] = $"data:{mediaType};base64,{Convert.ToBase64String(avatarBytes)}";
            } else {
                Logger.Warning("AutoFocusGraphs: no avatar image available for webhook profile update.");
            }

            if (body.Count == 0) {
                return;
            }

            using var request = new HttpRequestMessage(HttpMethod.Patch, safeUrl) {
                Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json")
            };
            using var patchResponse = await Http.SendAsync(request, token).ConfigureAwait(false);
            if (!patchResponse.IsSuccessStatusCode) {
                var errBody = await patchResponse.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"Could not update webhook profile ({(int)patchResponse.StatusCode}): {Truncate(errBody, 300)}");
            }

            Logger.Info("AutoFocusGraphs: updated webhook display name/avatar");
        }

        private static async Task<(byte[] bytes, string mediaType)> LoadAvatarBytesAsync(
            string avatarUrl,
            CancellationToken token) {
            if (!string.IsNullOrWhiteSpace(avatarUrl)) {
                try {
                    using var response = await Http.GetAsync(avatarUrl, token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode) {
                        var bytes = await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
                        if (bytes != null && bytes.Length > 0 && bytes.Length <= 8 * 1024 * 1024) {
                            var mediaType = response.Content.Headers.ContentType?.MediaType;
                            if (string.IsNullOrWhiteSpace(mediaType) ||
                                !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) {
                                mediaType = GuessImageMediaType(avatarUrl, bytes);
                            }
                            return (bytes, mediaType);
                        }
                    }
                    Logger.Warning($"AutoFocusGraphs: avatar URL failed (HTTP {(int)response.StatusCode}); using built-in icon.");
                } catch (Exception ex) {
                    Logger.Warning($"AutoFocusGraphs: avatar URL failed ({ex.Message}); using built-in icon.");
                }
            }

            var embedded = LoadEmbeddedAvatar();
            return embedded == null ? (null, "image/png") : (embedded, "image/png");
        }

        private static byte[] embeddedAvatarBytes;

        private static byte[] LoadEmbeddedAvatar() {
            if (embeddedAvatarBytes != null) {
                return embeddedAvatarBytes;
            }

            var asm = typeof(DiscordWebhookClient).Assembly;
            var name = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("avatar.png", StringComparison.OrdinalIgnoreCase));
            if (name == null) {
                Logger.Warning("AutoFocusGraphs: embedded avatar.png not found in assembly.");
                return null;
            }

            using var stream = asm.GetManifestResourceStream(name);
            if (stream == null) {
                return null;
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            embeddedAvatarBytes = ms.ToArray();
            return embeddedAvatarBytes;
        }

        private static string GuessImageMediaType(string url, byte[] bytes) {
            if (bytes.Length >= 8 &&
                bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) {
                return "image/png";
            }
            if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) {
                return "image/jpeg";
            }
            if (bytes.Length >= 6 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46) {
                return "image/gif";
            }
            if (bytes.Length >= 12 &&
                bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
                bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) {
                return "image/webp";
            }

            var path = url ?? string.Empty;
            if (path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)) {
                return "image/jpeg";
            }
            if (path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)) {
                return "image/gif";
            }
            if (path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)) {
                return "image/webp";
            }
            return "image/png";
        }

        public static async Task UploadFailureAsync(
            string webhookUrl,
            string fileName,
            string reason,
            DiscordPostOptions postOptions,
            CancellationToken token) {
            postOptions ??= DiscordPostOptions.FromSettings();
            if (!WebhookUrlValidator.TryValidate(webhookUrl, out _, out var error)) {
                throw new InvalidOperationException(error);
            }

            try {
                await RunSerializedWebhookAsync(async () => {
                    await EnsureWebhookProfileAsync(webhookUrl, postOptions, token, throwOnError: false).ConfigureAwait(false);
                    await PostJsonWithForumFallbackAsync(
                        webhookUrl,
                        postOptions,
                        opts => {
                            var embed = new JObject {
                                ["title"] = "Autofocus failure",
                                ["color"] = 0xE74C3C,
                                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                                ["fields"] = new JArray {
                                    new JObject {
                                        ["name"] = "File",
                                        ["value"] = Truncate(Safe(SanitizeFileName(fileName)), 256),
                                        ["inline"] = true
                                    },
                                    new JObject {
                                        ["name"] = "Reason",
                                        ["value"] = Truncate(Safe(reason), 1024),
                                        ["inline"] = false
                                    }
                                }
                            };
                            return ApplyIdentity(new JObject {
                                ["content"] = Truncate(DiscordRolePing.ApplyToContent($"Autofocus **failed**: `{SanitizeFileName(fileName)}`", ReportOutcome.Failure), 2000),
                                ["embeds"] = new JArray { embed }
                            }, opts).ToString(Formatting.None);
                        },
                        token).ConfigureAwait(false);
                }).ConfigureAwait(false);
                PostStatusTracker.RecordSuccess($"failure {SanitizeFileName(fileName)}");
                Logger.Info($"AutoFocusGraphs: posted failure for {SanitizeFileName(fileName)}");
            } catch (Exception ex) {
                var friendly = FormatDiscordError(ex.Message);
                PostStatusTracker.RecordFailure(friendly);
                throw new InvalidOperationException(friendly, ex);
            }
        }

        public static async Task UploadDigestAsync(
            string webhookUrl,
            IReadOnlyList<AutofocusReport> reports,
            DiscordPostOptions postOptions,
            string digestLabel,
            CancellationToken token,
            string sequenceName = null) {
            postOptions ??= DiscordPostOptions.FromSettings();
            digestLabel = string.IsNullOrWhiteSpace(digestLabel) ? "session" : digestLabel.Trim();
            sequenceName = string.IsNullOrWhiteSpace(sequenceName) ? null : sequenceName.Trim();
            if (!WebhookUrlValidator.TryValidate(webhookUrl, out _, out var error)) {
                throw new InvalidOperationException(error);
            }
            if (reports == null || reports.Count == 0) {
                throw new InvalidOperationException("No autofocus reports available for a digest.");
            }

            var ordered = reports
                .OrderBy(r => r.FormattedTimestamp, StringComparer.Ordinal)
                .ThenBy(r => r.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var stats = BuildDigestStats(
                ordered,
                string.Equals(digestLabel, "session", StringComparison.OrdinalIgnoreCase)
                    ? ReportStore.Instance.SessionSequencesCompleted
                    : (int?)null,
                string.Equals(digestLabel, "session", StringComparison.OrdinalIgnoreCase)
                    ? ReportStore.Instance.SessionSequenceNames
                    : null,
                string.Equals(digestLabel, "sequence", StringComparison.OrdinalIgnoreCase)
                    ? sequenceName
                    : null);
            var lines = ordered
                .Select((r, i) =>
                    $"• **#{i + 1}** `{Safe(r.FormatShortFileName())}` · **{Safe(r.Filter)}** | HFR {Safe(r.FormatFinalHfr())} | Pos {Safe(r.FormatCalculatedPosition())} | T {Safe(r.FormatTemperature())} · {Safe(r.FormatDigestTimestamp())}")
                .ToList();

            var body = string.Join("\n", lines);
            if (body.Length > 2800) {
                body = body.Substring(0, 2799) + "…";
            }

            byte[] chartPng = null;
            var includeGraph = postOptions.IncludeDigestTrendChart
                               && postOptions.AttachMode != AttachContentMode.EmbedOnly;
            if (includeGraph) {
                try {
                    chartPng = AutofocusGraphGenerator.CreateTrendPng(ordered, postOptions.DigestTrendMaxRuns);
                } catch {
                    // text-only digest
                }
            }

            var hasTrend = chartPng != null && chartPng.Length > 0;
            var includeEmbed = postOptions.AttachMode != AttachContentMode.GraphOnly;
            var description = Truncate($"{stats}\n\n{body}", 4096);

            try {
                await RunSerializedWebhookAsync(async () => {
                    await EnsureWebhookProfileAsync(webhookUrl, postOptions, token, throwOnError: false).ConfigureAwait(false);
                    await PostPayloadWithForumFallbackAsync(
                        webhookUrl,
                        postOptions,
                        opts => BuildDigestPayload(ordered.Count, description, hasTrend, includeEmbed, opts, digestLabel),
                        hasTrend ? chartPng : null,
                        attachJson: false,
                        jsonFilePath: null,
                        reportFileName: null,
                        imageFileName: "af_trend.png",
                        token).ConfigureAwait(false);
                }).ConfigureAwait(false);
                PostStatusTracker.RecordSuccess($"{digestLabel} digest ({ordered.Count} runs)");
                Logger.Info($"AutoFocusGraphs: posted {digestLabel} digest ({ordered.Count} runs)");
            } catch (Exception ex) {
                var friendly = FormatDiscordError(ex.Message);
                PostStatusTracker.RecordFailure(friendly);
                throw new InvalidOperationException(friendly, ex);
            }
        }

        private static string BuildDigestPayload(
            int runCount,
            string description,
            bool hasTrend,
            bool includeEmbed,
            DiscordPostOptions postOptions,
            string digestLabel) {
            var title = FormatDigestTitle(digestLabel);
            var root = ApplyIdentity(new JObject {
                ["content"] = Truncate($"{title} ({runCount} run(s))", 2000)
            }, postOptions);

            if (includeEmbed) {
                var embed = new JObject {
                    ["title"] = title,
                    ["color"] = 0x3498DB,
                    ["timestamp"] = DateTime.UtcNow.ToString("o"),
                    ["description"] = description
                };
                if (hasTrend) {
                    embed["image"] = new JObject { ["url"] = "attachment://af_trend.png" };
                }
                root["embeds"] = new JArray { embed };
            }

            if (hasTrend) {
                root["attachments"] = new JArray {
                    new JObject { ["id"] = 0, ["filename"] = "af_trend.png" }
                };
            }

            return root.ToString(Formatting.None);
        }

        private static string BuildDigestStats(
            IReadOnlyList<AutofocusReport> ordered,
            int? sessionSequencesCompleted = null,
            IReadOnlyList<string> sessionSequenceNames = null,
            string sequenceName = null) {
            var hfrs = ordered.Select(r => r.FinalHfr).ToList();
            var min = hfrs.Min();
            var max = hfrs.Max();
            var avg = hfrs.Average();
            var best = ordered.OrderBy(r => r.FinalHfr).First();
            var worst = ordered.OrderByDescending(r => r.FinalHfr).First();

            var options = PluginRuntimeOptions.FromSettings();
            var warnings = 0;
            foreach (var report in ordered) {
                var (minR2, maxHfr, _) = FilterQualityProfiles.Resolve(
                    report.Filter,
                    options.FilterQualityProfiles,
                    options.MinR2,
                    options.MaxFinalHfr);
                var quality = QualityEvaluator.Evaluate(
                    report,
                    isFailure: false,
                    failureReason: null,
                    qualityGateEnabled: options.QualityGateEnabled,
                    minR2: minR2,
                    maxFinalHfr: maxHfr);
                if (quality.Outcome == ReportOutcome.Warning) {
                    warnings++;
                }
            }

            var byFilter = ordered
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Filter) ? "N/A" : r.Filter, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => $"**{Safe(g.Key)}** ×{g.Count()} (avg HFR {g.Average(r => r.FinalHfr).ToString("0.00", CultureInfo.InvariantCulture)})");

            var filterLine = string.Join(" · ", byFilter);
            if (filterLine.Length > 500) {
                filterLine = filterLine.Substring(0, 499) + "…";
            }

            var sequencePart = sessionSequencesCompleted.HasValue
                ? $" · **{sessionSequencesCompleted.Value}** sequence{(sessionSequencesCompleted.Value == 1 ? string.Empty : "s")} this session"
                : string.Empty;

            var sequenceNamesLine = string.Empty;
            if (!string.IsNullOrWhiteSpace(sequenceName)) {
                sequenceNamesLine = $"Sequence: `{Safe(sequenceName)}`\n";
            } else if (sessionSequencesCompleted is > 0 && sessionSequenceNames != null && sessionSequenceNames.Count > 0) {
                sequenceNamesLine = $"Sequences: {FormatSequenceNameList(sessionSequenceNames)}\n";
            }

            return
                $"**{ordered.Count}** run(s){sequencePart} · HFR min **{min.ToString("0.00", CultureInfo.InvariantCulture)}** / " +
                $"avg **{avg.ToString("0.00", CultureInfo.InvariantCulture)}** / max **{max.ToString("0.00", CultureInfo.InvariantCulture)}**\n" +
                sequenceNamesLine +
                $"Best: `{Safe(best.FormatShortFileName())}` ({Safe(best.Filter)}, HFR {Safe(best.FormatFinalHfr())}) · " +
                $"Worst: `{Safe(worst.FormatShortFileName())}` ({Safe(worst.Filter)}, HFR {Safe(worst.FormatFinalHfr())})\n" +
                $"Quality warnings: **{warnings}**\n" +
                $"By filter: {filterLine}";
        }

        private static string BuildReportMessage(AutofocusReport report, string messageTemplate, QualityResult quality) {
            var template = string.IsNullOrWhiteSpace(messageTemplate)
                ? "New autofocus report: **{shortfilename}** ({filter})"
                : messageTemplate;
            var message = template
                .Replace("{prefix}", quality.ContentPrefix, StringComparison.OrdinalIgnoreCase)
                .Replace("{shortfilename}", Safe(report.FormatShortFileName()), StringComparison.OrdinalIgnoreCase)
                .Replace("{time}", Safe(report.FormatDigestTimestamp()), StringComparison.OrdinalIgnoreCase)
                .Replace("{filenamefull}", SanitizeFileName(report.FormatFullFileName()), StringComparison.OrdinalIgnoreCase)
                .Replace("{filename}", Safe(report.FormatTruncatedFileName()), StringComparison.OrdinalIgnoreCase)
                .Replace("{filter}", Safe(report.Filter ?? "N/A"), StringComparison.OrdinalIgnoreCase);

            if (!template.Contains("{prefix}", StringComparison.OrdinalIgnoreCase) &&
                quality.Outcome != ReportOutcome.Success) {
                message = $"{quality.ContentPrefix}: {message}";
            }

            if (!string.IsNullOrWhiteSpace(quality.Reason)) {
                message += $"\n{quality.Reason}";
            }

            return message;
        }

        private static string BuildReportPayload(
            AutofocusReport report,
            string message,
            bool hasGraph,
            bool attachJson,
            QualityResult quality,
            DiscordPostOptions postOptions,
            bool includeEmbed) {
            var root = ApplyIdentity(new JObject {
                ["content"] = Truncate(DiscordRolePing.ApplyToContent(message ?? string.Empty, quality?.Outcome ?? ReportOutcome.Success), 2000)
            }, postOptions);

            if (includeEmbed) {
                var embed = postOptions.EmbedMode == EmbedDetailMode.Compact
                    ? BuildCompactEmbed(report, hasGraph, quality)
                    : BuildDetailedEmbed(report, hasGraph, quality);
                root["embeds"] = new JArray { embed };
            }

            var attachments = new JArray();
            var attachmentId = 0;
            if (hasGraph) {
                attachments.Add(new JObject {
                    ["id"] = attachmentId++,
                    ["filename"] = "autofocus_curve.png"
                });
                if (!includeEmbed) {
                    // Graph-only still needs attachment metadata for Discord.
                }
            }
            if (attachJson) {
                attachments.Add(new JObject {
                    ["id"] = attachmentId,
                    ["filename"] = SanitizeFileName(report.FileName)
                });
            }
            if (attachments.Count > 0) {
                root["attachments"] = attachments;
            }

            return root.ToString(Formatting.None);
        }

        private static JObject BuildDetailedEmbed(AutofocusReport report, bool hasGraph, QualityResult quality) {
            var details =
                $"**Method:** {Safe(report.Method)} | **Fitting:** {Safe(report.Fitting)} | **Temperature:** {Safe(report.FormatTemperature())}\n" +
                $"**Step-Size:** {Safe(report.StepSize?.ToString() ?? "N/A")} | **Calculated-Focus-Position:** {Safe(report.FormatCalculatedPosition())} | **Filter:** {Safe(report.Filter)}\n" +
                $"**Backlash Method:** {Safe(report.BacklashMethod)} | **Backlash IN:** {Safe(report.BacklashIn)} | **Backlash OUT:** {Safe(report.BacklashOut)}";

            var r2 =
                $"Hyperbolic: {Safe(report.FormatR2(report.R2Hyperbolic))} | " +
                $"Parabolic: {Safe(report.FormatR2(report.R2Parabolic))} | " +
                $"Left Trend: {Safe(report.FormatR2(report.R2Left))} | " +
                $"Right Trend: {Safe(report.FormatR2(report.R2Right))}";

            var title = string.IsNullOrWhiteSpace(report.Filter) || report.Filter == "N/A"
                ? "AutoFocus Details"
                : $"AutoFocus Details — Filter {report.Filter}";

            var fields = new JArray {
                new JObject {
                    ["name"] = "\u200B",
                    ["value"] = Truncate(details, 1024),
                    ["inline"] = false
                },
                new JObject {
                    ["name"] = "R² - Coefficient of determination",
                    ["value"] = Truncate(r2, 1024),
                    ["inline"] = false
                }
            };

            if (!string.IsNullOrWhiteSpace(quality?.Reason)) {
                fields.Add(new JObject {
                    ["name"] = "Quality",
                    ["value"] = Truncate(Safe(quality.Reason), 1024),
                    ["inline"] = false
                });
            }

            fields.Add(new JObject {
                ["name"] = "Date & Time",
                ["value"] = Truncate(Safe(report.FormattedTimestamp), 256),
                ["inline"] = true
            });
            fields.Add(new JObject {
                ["name"] = "AF Duration",
                ["value"] = Truncate(Safe(report.FormattedDuration), 256),
                ["inline"] = true
            });
            fields.Add(new JObject {
                ["name"] = "Final HFR",
                ["value"] = Truncate(Safe(report.FormatFinalHfr()), 256),
                ["inline"] = true
            });

            var embed = new JObject {
                ["title"] = Truncate(title, 256),
                ["color"] = quality?.EmbedColor ?? 0x7289DA,
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["fields"] = fields
            };
            var footer = BuildReportFileFooter(report);
            if (footer != null) {
                embed["footer"] = footer;
            }
            if (hasGraph) {
                embed["image"] = new JObject { ["url"] = "attachment://autofocus_curve.png" };
            }
            return embed;
        }

        private static JObject BuildCompactEmbed(AutofocusReport report, bool hasGraph, QualityResult quality) {
            var title = string.IsNullOrWhiteSpace(report.Filter) || report.Filter == "N/A"
                ? "AutoFocus"
                : $"AutoFocus — {report.Filter}";
            var description =
                $"HFR **{Safe(report.FormatFinalHfr())}** · Pos **{Safe(report.FormatCalculatedPosition())}** · " +
                $"R² **{Safe(report.FormatR2(report.R2Hyperbolic))}** · {Safe(report.FormatDigestTimestamp())}";
            if (!string.IsNullOrWhiteSpace(quality?.Reason)) {
                description += $"\n{Safe(quality.Reason)}";
            }

            var embed = new JObject {
                ["title"] = Truncate(title, 256),
                ["description"] = Truncate(description, 4096),
                ["color"] = quality?.EmbedColor ?? 0x7289DA,
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            };
            var footer = BuildReportFileFooter(report);
            if (footer != null) {
                embed["footer"] = footer;
            }
            if (hasGraph) {
                embed["image"] = new JObject { ["url"] = "attachment://autofocus_curve.png" };
            }
            return embed;
        }

        private static JObject BuildReportFileFooter(AutofocusReport report) {
            var full = SanitizeFileName(report?.FormatFullFileName() ?? string.Empty);
            if (string.IsNullOrWhiteSpace(full)) {
                return null;
            }
            return new JObject {
                ["text"] = Truncate(full, 2048)
            };
        }

        private static JObject ApplyIdentity(JObject root, DiscordPostOptions postOptions) {
            // Default: override nothing — Discord renders the webhook's own name and icon,
            // exactly as configured in Server Settings → Integrations → Webhooks.
            // Only explicit user settings override per message.
            if (!string.IsNullOrWhiteSpace(postOptions?.Username)) {
                root["username"] = postOptions.Username;
            }
            if (!string.IsNullOrWhiteSpace(postOptions?.AvatarUrl)) {
                root["avatar_url"] = postOptions.AvatarUrl;
            }

            if (postOptions?.UseNightlyThreadName == true && string.IsNullOrWhiteSpace(postOptions.ThreadId)) {
                root["thread_name"] = $"AF {DateTime.Now:yyyy-MM-dd}";
            }
            return root;
        }

        private static string ApplyThreadId(string safeUrl, DiscordPostOptions postOptions) {
            if (string.IsNullOrWhiteSpace(postOptions?.ThreadId)) {
                return safeUrl;
            }
            var separator = safeUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return $"{safeUrl}{separator}thread_id={postOptions.ThreadId}";
        }

        private static async Task PostJsonWithForumFallbackAsync(
            string webhookUrl,
            DiscordPostOptions postOptions,
            Func<DiscordPostOptions, string> buildPayload,
            CancellationToken token) {
            if (!WebhookUrlValidator.TryValidate(webhookUrl, out var safeUrl, out var error)) {
                throw new InvalidOperationException(error);
            }

            try {
                await PostJsonAsync(ApplyThreadId(safeUrl, postOptions), buildPayload(postOptions), token).ConfigureAwait(false);
            } catch (InvalidOperationException ex) when (IsForumThreadError(ex.Message) && postOptions.UseNightlyThreadName) {
                Logger.Warning("AutoFocusGraphs: channel is not a forum; posting without nightly thread name.");
                var fallback = postOptions.WithoutNightlyThread();
                await PostJsonAsync(ApplyThreadId(safeUrl, fallback), buildPayload(fallback), token).ConfigureAwait(false);
            }
        }

        private static async Task PostPayloadWithForumFallbackAsync(
            string webhookUrl,
            DiscordPostOptions postOptions,
            Func<DiscordPostOptions, string> buildPayload,
            byte[] imagePng,
            bool attachJson,
            string jsonFilePath,
            string reportFileName,
            string imageFileName,
            CancellationToken token) {
            if (!WebhookUrlValidator.TryValidate(webhookUrl, out var safeUrl, out var error)) {
                throw new InvalidOperationException(error);
            }

            try {
                await PostMultipartAsync(
                    ApplyThreadId(safeUrl, postOptions),
                    buildPayload(postOptions),
                    imagePng,
                    attachJson,
                    jsonFilePath,
                    reportFileName,
                    token,
                    imageFileName).ConfigureAwait(false);
            } catch (InvalidOperationException ex) when (IsForumThreadError(ex.Message) && postOptions.UseNightlyThreadName) {
                Logger.Warning("AutoFocusGraphs: channel is not a forum; posting without nightly thread name.");
                var fallback = postOptions.WithoutNightlyThread();
                await PostMultipartAsync(
                    ApplyThreadId(safeUrl, fallback),
                    buildPayload(fallback),
                    imagePng,
                    attachJson,
                    jsonFilePath,
                    reportFileName,
                    token,
                    imageFileName).ConfigureAwait(false);
            }
        }

        private static bool IsForumThreadError(string message) =>
            !string.IsNullOrEmpty(message) &&
            (message.Contains("forum channels", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("220003", StringComparison.Ordinal));

        private static string FormatDiscordError(string message) {
            if (IsForumThreadError(message)) {
                return "Nightly forum thread only works in Discord forum channels. Turn off that option for a normal text channel.";
            }
            if (!string.IsNullOrEmpty(message) &&
                message.Contains("cannot contain", StringComparison.OrdinalIgnoreCase) &&
                message.Contains("discord", StringComparison.OrdinalIgnoreCase)) {
                return "Webhook display name cannot contain the word “discord”.";
            }
            return message ?? "Discord request failed.";
        }

        private static async Task PostJsonAsync(string safeUrl, string payload, CancellationToken token) {
            await PostWithRetryAsync(async () => {
                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                return await Http.PostAsync(safeUrl, content, token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);
        }

        private static async Task PostMultipartAsync(
            string safeUrl,
            string payload,
            byte[] imagePng,
            bool attachJson,
            string jsonFilePath,
            string reportFileName,
            CancellationToken token,
            string imageFileName = "autofocus_curve.png") {
            byte[] jsonBytes = null;
            if (attachJson && !string.IsNullOrWhiteSpace(jsonFilePath) && File.Exists(jsonFilePath)) {
                jsonBytes = await ReadFileWithRetryAsync(jsonFilePath, token).ConfigureAwait(false);
            }

            await PostWithRetryAsync(async () => {
                var form = new MultipartFormDataContent();
                var payloadContent = new StringContent(payload, Encoding.UTF8);
                payloadContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                form.Add(payloadContent, "payload_json");

                var fileIndex = 0;
                if (imagePng != null && imagePng.Length > 0) {
                    var imageContent = new ByteArrayContent(imagePng);
                    imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                    form.Add(imageContent, $"files[{fileIndex}]", imageFileName);
                    fileIndex++;
                }

                if (jsonBytes != null) {
                    var jsonContent = new ByteArrayContent(jsonBytes);
                    jsonContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    form.Add(jsonContent, $"files[{fileIndex}]", SanitizeFileName(reportFileName));
                }

                return await Http.PostAsync(safeUrl, form, token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);
        }

        private static async Task PostWithRetryAsync(Func<Task<HttpResponseMessage>> send, CancellationToken token) {
            const int maxAttempts = 4;
            for (var attempt = 1; attempt <= maxAttempts; attempt++) {
                token.ThrowIfCancellationRequested();
                HttpResponseMessage response = null;
                try {
                    response = await send().ConfigureAwait(false);
                    if (response.IsSuccessStatusCode) {
                        response.Dispose();
                        return;
                    }

                    var status = (int)response.StatusCode;
                    var body = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                    var retriable = status == 429 || status >= 500;
                    if (!retriable || attempt == maxAttempts) {
                        throw new InvalidOperationException($"Discord webhook returned {status}: {Truncate(body, 300)}");
                    }

                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    if (response.Headers.RetryAfter?.Delta is TimeSpan retryAfter) {
                        delay = retryAfter;
                    } else if (response.StatusCode == (HttpStatusCode)429 &&
                               response.Headers.TryGetValues("Retry-After", out var values) &&
                               double.TryParse(values.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)) {
                        delay = TimeSpan.FromSeconds(Math.Max(1, seconds));
                    }

                    Logger.Warning($"AutoFocusGraphs: Discord {status}, retry {attempt}/{maxAttempts} in {delay.TotalSeconds:0}s");
                    response.Dispose();
                    await Task.Delay(delay, token).ConfigureAwait(false);
                } catch (HttpRequestException) when (attempt < maxAttempts) {
                    response?.Dispose();
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    Logger.Warning($"AutoFocusGraphs: network error, retry {attempt}/{maxAttempts} in {delay.TotalSeconds:0}s");
                    await Task.Delay(delay, token).ConfigureAwait(false);
                }
            }
        }

        private static string FormatSequenceNameList(IReadOnlyList<string> names) {
            const int maxShown = 8;
            var shown = names.Take(maxShown).Select(n => $"`{Safe(n)}`");
            var line = string.Join(" · ", shown);
            if (names.Count > maxShown) {
                line += $" · *+{names.Count - maxShown} more*";
            }

            return line;
        }

        private static string FormatDigestTitle(string digestLabel) {
            if (string.Equals(digestLabel, "sequence", StringComparison.OrdinalIgnoreCase)) {
                return "Autofocus Sequence digest (at end of sequence)";
            }

            if (string.Equals(digestLabel, "session", StringComparison.OrdinalIgnoreCase)) {
                return "Autofocus Session digest (on NINA close)";
            }

            return $"Autofocus {CapitalizeDigestLabel(digestLabel)} digest";
        }

        private static string CapitalizeDigestLabel(string digestLabel) {
            if (string.IsNullOrWhiteSpace(digestLabel)) {
                return "Session";
            }
            return char.ToUpperInvariant(digestLabel[0]) + digestLabel.Substring(1);
        }

        private static string Safe(object value) {
            if (value == null) {
                return "N/A";
            }
            return value.ToString()
                .Replace("@everyone", "@\u200Beveryone", StringComparison.OrdinalIgnoreCase)
                .Replace("@here", "@\u200Bhere", StringComparison.OrdinalIgnoreCase);
        }

        private static string SanitizeFileName(string fileName) {
            var name = Path.GetFileName(fileName ?? "report.json");
            foreach (var c in Path.GetInvalidFileNameChars()) {
                name = name.Replace(c, '_');
            }
            return string.IsNullOrWhiteSpace(name) ? "report.json" : name;
        }

        private static string Truncate(string value, int maxLen) {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLen) {
                return value ?? string.Empty;
            }
            return value.Substring(0, maxLen - 1) + "…";
        }

        private static async Task<byte[]> ReadFileWithRetryAsync(string filePath, CancellationToken token) {
            const int attempts = 5;
            for (var i = 0; i < attempts; i++) {
                token.ThrowIfCancellationRequested();
                try {
                    return await File.ReadAllBytesAsync(filePath, token).ConfigureAwait(false);
                } catch (IOException) when (i < attempts - 1) {
                    await Task.Delay(250, token).ConfigureAwait(false);
                }
            }

            return await File.ReadAllBytesAsync(filePath, token).ConfigureAwait(false);
        }
    }
}
