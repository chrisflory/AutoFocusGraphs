using AutoFocusGraphs.Properties;
using NINA.Core.Utility;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Settings = AutoFocusGraphs.Properties.Settings;

namespace AutoFocusGraphs.Destinations {
    internal sealed class SlackDestination : IAutofocusDestination {
        public string Name => "Slack";

        public bool IsEnabled => Settings.Default.SlackEnabled;

        public bool IsConfigured =>
            SlackBotValidator.TryValidate(Settings.Default.SlackBotToken, Settings.Default.SlackChannelId, out _);

        public bool TryValidate(out string error) {
            if (!IsEnabled) {
                error = null;
                return true;
            }

            return SlackBotValidator.TryValidate(
                Settings.Default.SlackBotToken,
                Settings.Default.SlackChannelId,
                out error);
        }

        public async Task PostReportAsync(ReportPostRequest request, CancellationToken token) {
            var message = SlackBotClient.ConvertMarkdownForSlack(
                ReportMessageFormatter.BuildReportMessage(request.Report, request.MessageTemplate, request.Quality));
            try {
                await SlackBotClient.SendReportAsync(
                    Settings.Default.SlackBotToken.Trim(),
                    Settings.Default.SlackChannelId.Trim(),
                    request.GraphPng,
                    message,
                    request.AttachJson,
                    request.JsonFilePath,
                    request.Report?.FileName,
                    token).ConfigureAwait(false);
                PostStatusTracker.RecordSuccess($"{request.Quality.Outcome} {request.Report?.FileName} (Slack)");
                Logger.Info($"AutoFocusGraphs: posted report to Slack ({request.Quality.Outcome})");
            } catch (Exception ex) {
                PostStatusTracker.RecordFailure(ex.Message);
                throw;
            }
        }

        public Task PostFailureAsync(FailurePostRequest request, CancellationToken token) {
            Logger.Info($"AutoFocusGraphs: posting failure to Slack ({request.FileName})");
            return PostFailureCoreAsync(request, token);
        }

        private static async Task PostFailureCoreAsync(FailurePostRequest request, CancellationToken token) {
            try {
                await SlackBotClient.SendFailureAsync(
                    Settings.Default.SlackBotToken.Trim(),
                    Settings.Default.SlackChannelId.Trim(),
                    request.FileName,
                    SlackBotClient.ConvertMarkdownForSlack(request.Reason),
                    token).ConfigureAwait(false);
                PostStatusTracker.RecordSuccess($"failure {request.FileName} (Slack)");
                Logger.Info($"AutoFocusGraphs: posted failure to Slack ({request.FileName})");
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
                } catch (Exception ex) {
                    Logger.Warning($"AutoFocusGraphs: Slack digest chart failed: {ex.Message}");
                }
            }

            var caption = BuildDigestCaption(request);
            await SlackBotClient.SendDigestAsync(
                Settings.Default.SlackBotToken.Trim(),
                Settings.Default.SlackChannelId.Trim(),
                chart,
                caption,
                token).ConfigureAwait(false);
            Logger.Info($"AutoFocusGraphs: posted {request.DigestLabel} digest to Slack ({request.Reports?.Count ?? 0} run(s))");
        }

        public Task TestConnectionAsync(CancellationToken token) =>
            SlackBotClient.SendTestAsync(
                Settings.Default.SlackBotToken.Trim(),
                Settings.Default.SlackChannelId.Trim(),
                token);

        private static string BuildDigestCaption(DigestPostRequest request) {
            var sb = new StringBuilder();
            var label = string.Equals(request.DigestLabel, "sequence", StringComparison.OrdinalIgnoreCase)
                ? "Sequence digest"
                : "Session digest";
            sb.Append('*').Append(label).Append('*');
            if (!string.IsNullOrWhiteSpace(request.SequenceName)) {
                sb.Append(" — ").Append(request.SequenceName);
            }

            sb.Append('\n').Append(request.Reports?.Count ?? 0).Append(" run(s)");
            if (request.Reports?.Count > 0) {
                var filters = string.Join(", ", request.Reports.Select(r => r.Filter).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct());
                if (!string.IsNullOrWhiteSpace(filters)) {
                    sb.Append("\nFilters: ").Append(filters);
                }
            }

            return sb.ToString();
        }
    }
}
