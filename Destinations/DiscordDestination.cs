using AutoFocusGraphs.Properties;
using System.Threading;
using System.Threading.Tasks;
using Settings = AutoFocusGraphs.Properties.Settings;

namespace AutoFocusGraphs.Destinations {
    internal sealed class DiscordDestination : IAutofocusDestination {
        public string Name => "Discord";

        public bool IsEnabled => Settings.Default.DiscordEnabled;

        public bool IsConfigured =>
            WebhookUrlValidator.TryValidate(Settings.Default.WebhookUrl, out _, out _);

        public bool TryValidate(out string error) {
            if (!IsEnabled) {
                error = null;
                return true;
            }

            return WebhookUrlValidator.TryValidate(Settings.Default.WebhookUrl, out _, out error);
        }

        public Task PostReportAsync(ReportPostRequest request, CancellationToken token) =>
            DiscordWebhookClient.UploadReportAsync(
                Settings.Default.WebhookUrl,
                request.Report,
                request.GraphPng,
                request.MessageTemplate,
                request.AttachJson,
                request.JsonFilePath,
                request.Quality,
                DiscordPostOptions.FromSettings(),
                token);

        public Task PostFailureAsync(FailurePostRequest request, CancellationToken token) =>
            DiscordWebhookClient.UploadFailureAsync(
                Settings.Default.WebhookUrl,
                request.FileName,
                request.Reason,
                DiscordPostOptions.FromSettings(),
                token);

        public Task PostDigestAsync(DigestPostRequest request, CancellationToken token) =>
            DiscordWebhookClient.UploadDigestAsync(
                Settings.Default.WebhookUrl,
                request.Reports,
                DiscordPostOptions.FromSettings(),
                request.DigestLabel,
                token,
                request.SequenceName);

        public Task TestConnectionAsync(CancellationToken token) =>
            DiscordWebhookClient.UploadTestAsync(
                Settings.Default.WebhookUrl,
                DiscordPostOptions.FromSettings(),
                token);
    }
}
