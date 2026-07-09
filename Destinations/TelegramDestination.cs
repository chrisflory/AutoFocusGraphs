using AutoFocusGraphs.Properties;
using NINA.Core.Utility;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Settings = AutoFocusGraphs.Properties.Settings;

namespace AutoFocusGraphs.Destinations {
    internal sealed class TelegramDestination : IAutofocusDestination {
        public string Name => "Telegram";

        public bool IsEnabled => Settings.Default.TelegramEnabled;

        public bool IsConfigured =>
            TelegramBotValidator.TryValidate(Settings.Default.TelegramBotToken, Settings.Default.TelegramChatId, out _);

        public bool TryValidate(out string error) {
            if (!IsEnabled) {
                error = null;
                return true;
            }

            return TelegramBotValidator.TryValidate(
                Settings.Default.TelegramBotToken,
                Settings.Default.TelegramChatId,
                out error);
        }

        public async Task PostReportAsync(ReportPostRequest request, CancellationToken token) {
            var caption = BuildReportCaption(request);
            try {
                await TelegramBotClient.SendReportAsync(
                    Settings.Default.TelegramBotToken.Trim(),
                    Settings.Default.TelegramChatId.Trim(),
                    request.GraphPng,
                    caption,
                    request.AttachJson,
                    request.JsonFilePath,
                    request.Report?.FileName,
                    token).ConfigureAwait(false);
                PostStatusTracker.RecordSuccess($"{request.Quality.Outcome} {request.Report?.FileName} (Telegram)");
                Logger.Info($"AutoFocusGraphs: posted report to Telegram ({request.Quality.Outcome})");
            } catch (System.Exception ex) {
                PostStatusTracker.RecordFailure(ex.Message);
                throw;
            }
        }

        public async Task PostFailureAsync(FailurePostRequest request, CancellationToken token) {
            try {
                await TelegramBotClient.SendFailureAsync(
                    Settings.Default.TelegramBotToken.Trim(),
                    Settings.Default.TelegramChatId.Trim(),
                    request.FileName,
                    request.Reason,
                    token).ConfigureAwait(false);
                PostStatusTracker.RecordSuccess($"failure {request.FileName} (Telegram)");
                Logger.Info($"AutoFocusGraphs: posted failure to Telegram ({request.FileName})");
            } catch (Exception ex) {
                PostStatusTracker.RecordFailure(ex.Message);
                throw;
            }
        }

        public async Task PostDigestAsync(DigestPostRequest request, CancellationToken token) {
            byte[] chart = null;
            if (Settings.Default.IncludeDigestTrendChart && request.Reports?.Count > 0) {
                try {
                    var ordered = request.Reports.OrderBy(r => r.CapturedUtc ?? DateTime.MinValue).ToList();
                    chart = AutofocusGraphGenerator.CreateTrendPng(
                        ordered,
                        Settings.Default.DigestTrendMaxRuns);
                } catch (System.Exception ex) {
                    Logger.Warning($"AutoFocusGraphs: Telegram digest chart failed: {ex.Message}");
                }
            }

            var caption = BuildDigestCaption(request);
            await TelegramBotClient.SendDigestAsync(
                Settings.Default.TelegramBotToken.Trim(),
                Settings.Default.TelegramChatId.Trim(),
                chart,
                caption,
                token).ConfigureAwait(false);
            Logger.Info($"AutoFocusGraphs: posted {request.DigestLabel} digest to Telegram ({request.Reports?.Count ?? 0} run(s))");
        }

        public Task TestConnectionAsync(CancellationToken token) =>
            TelegramBotClient.SendTestAsync(
                Settings.Default.TelegramBotToken.Trim(),
                Settings.Default.TelegramChatId.Trim(),
                token);

        private static string BuildReportCaption(ReportPostRequest request) {
            var report = request.Report;
            var quality = request.Quality;
            var sb = new StringBuilder();
            sb.Append("<b>Autofocus report</b>");
            if (!string.IsNullOrWhiteSpace(report?.Filter)) {
                sb.Append(" — ").Append(TelegramBotClient.ConvertDiscordMarkdownToHtml(report.Filter));
            }

            sb.Append('\n');
            if (report?.FinalHfr is double hfr) {
                sb.Append("HFR: <b>").Append(hfr.ToString("0.00")).Append("</b>");
            }

            if (report?.CalculatedPosition is double pos) {
                sb.Append(" | Focus: <b>").Append(pos.ToString("0")).Append("</b>");
            }

            if (quality?.Outcome == ReportOutcome.Warning && !string.IsNullOrWhiteSpace(quality.Reason)) {
                sb.Append("\n⚠ ").Append(TelegramBotClient.ConvertDiscordMarkdownToHtml(quality.Reason));
            }

            if (!string.IsNullOrWhiteSpace(request.MessageTemplate) && report != null) {
                var message = ReportMessageFormatter.BuildReportMessage(report, request.MessageTemplate, quality);
                if (!string.IsNullOrWhiteSpace(message)) {
                    sb.Append('\n').Append(TelegramBotClient.ConvertDiscordMarkdownToHtml(message));
                }
            }

            return sb.ToString();
        }

        private static string BuildDigestCaption(DigestPostRequest request) {
            var sb = new StringBuilder();
            var label = string.Equals(request.DigestLabel, "sequence", System.StringComparison.OrdinalIgnoreCase)
                ? "Sequence digest"
                : "Session digest";
            sb.Append("<b>").Append(label).Append("</b>");
            if (!string.IsNullOrWhiteSpace(request.SequenceName)) {
                sb.Append(" — ").Append(TelegramBotClient.ConvertDiscordMarkdownToHtml(request.SequenceName));
            }

            sb.Append('\n').Append(request.Reports?.Count ?? 0).Append(" run(s)");
            if (request.Reports?.Count > 0) {
                var filters = string.Join(", ", request.Reports.Select(r => r.Filter).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct());
                if (!string.IsNullOrWhiteSpace(filters)) {
                    sb.Append("\nFilters: ").Append(TelegramBotClient.ConvertDiscordMarkdownToHtml(filters));
                }
            }

            return sb.ToString();
        }
    }
}
