using AutoFocusGraphs.Properties;
using NINA.Core.Utility;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Settings = AutoFocusGraphs.Properties.Settings;

namespace AutoFocusGraphs.Destinations {
    internal sealed class EmailDestination : IAutofocusDestination {
        public string Name => "Email";

        public bool IsEnabled => Settings.Default.EmailEnabled;

        public bool IsConfigured => TryValidateSettings(out _);

        public bool TryValidate(out string error) {
            if (!IsEnabled) {
                error = null;
                return true;
            }

            return TryValidateSettings(out error);
        }

        public async Task PostReportAsync(ReportPostRequest request, CancellationToken token) {
            var settings = ReadSettings();
            if (!EmailSmtpValidator.TryValidateRecipients(settings.To, out var recipientError)) {
                throw new InvalidOperationException(recipientError);
            }

            var message = EmailSmtpClient.StripMarkdown(
                ReportMessageFormatter.BuildReportMessage(request.Report, request.MessageTemplate, request.Quality));
            var subject = EmailSubjectFormatter.FormatReportSubject(
                request.Report,
                request.Quality,
                request.SequenceName,
                Settings.Default.EmailSubjectTemplate);
            try {
                await EmailSmtpClient.SendReportAsync(
                    settings.Host,
                    settings.Port,
                    settings.UseSsl,
                    settings.Username,
                    settings.Password,
                    settings.From,
                    settings.To,
                    request.GraphPng,
                    subject,
                    message,
                    request.AttachJson,
                    request.JsonFilePath,
                    request.Report?.FileName,
                    token).ConfigureAwait(false);
                PostStatusTracker.RecordSuccess($"{request.Quality.Outcome} {request.Report?.FileName} (Email)");
                Logger.Info($"AutoFocusGraphs: posted report to Email ({request.Quality.Outcome})");
            } catch (Exception ex) {
                PostStatusTracker.RecordFailure(ex.Message);
                throw;
            }
        }

        public async Task PostFailureAsync(FailurePostRequest request, CancellationToken token) {
            var settings = ReadSettings();
            if (!EmailSmtpValidator.TryValidateRecipients(settings.To, out var recipientError)) {
                throw new InvalidOperationException(recipientError);
            }

            Logger.Info($"AutoFocusGraphs: posting failure to Email ({request.FileName})");
            var subject = EmailSubjectFormatter.FormatFailureSubject(
                request.FileName,
                request.Reason,
                request.SequenceName,
                Settings.Default.EmailSubjectTemplate);
            try {
                await EmailSmtpClient.SendFailureAsync(
                    settings.Host,
                    settings.Port,
                    settings.UseSsl,
                    settings.Username,
                    settings.Password,
                    settings.From,
                    settings.To,
                    subject,
                    request.FileName,
                    EmailSmtpClient.StripMarkdown(request.Reason),
                    token).ConfigureAwait(false);
                PostStatusTracker.RecordSuccess($"failure {request.FileName} (Email)");
                Logger.Info($"AutoFocusGraphs: posted failure to Email ({request.FileName})");
            } catch (Exception ex) {
                PostStatusTracker.RecordFailure(ex.Message);
                throw;
            }
        }

        public async Task PostDigestAsync(DigestPostRequest request, CancellationToken token) {
            var settings = ReadSettings();
            if (!EmailSmtpValidator.TryValidateRecipients(settings.To, out var recipientError)) {
                throw new InvalidOperationException(recipientError);
            }

            byte[] chart = null;
            if (Settings.Default.IncludeDigestTrendChart && request.Reports?.Count > 0) {
                try {
                    var ordered = request.Reports.OrderBy(r => r.CapturedUtc ?? DateTime.MinValue).ToList();
                    chart = AutofocusGraphGenerator.CreateTrendPng(
                        ordered,
                        Settings.Default.DigestTrendMaxRuns);
                } catch (Exception ex) {
                    Logger.Warning($"AutoFocusGraphs: Email digest chart failed: {ex.Message}");
                }
            }

            var subject = EmailSubjectFormatter.FormatDigestSubject(
                request.DigestLabel,
                request.SequenceName,
                Settings.Default.EmailSubjectTemplate);
            var body = BuildDigestBody(request);
            await EmailSmtpClient.SendDigestAsync(
                settings.Host,
                settings.Port,
                settings.UseSsl,
                settings.Username,
                settings.Password,
                settings.From,
                settings.To,
                chart,
                subject,
                body,
                token).ConfigureAwait(false);
            Logger.Info($"AutoFocusGraphs: posted {request.DigestLabel} digest to Email ({request.Reports?.Count ?? 0} run(s))");
        }

        public Task TestConnectionAsync(CancellationToken token) {
            var settings = ReadSettings();
            if (!EmailSmtpValidator.TryValidateRecipientsOrDefault(
                settings.To,
                Settings.Default.EmailFrom,
                Settings.Default.EmailUsername,
                out var to,
                out var error)) {
                throw new InvalidOperationException(error);
            }

            return EmailSmtpClient.SendTestAsync(
                settings.Host,
                settings.Port,
                settings.UseSsl,
                settings.Username,
                settings.Password,
                settings.From,
                to,
                EmailSubjectFormatter.FormatTestSubject(
                    Settings.Default.EmailSubjectTemplate,
                    ReportStore.Instance.GetPendingSequenceName(),
                    DateTime.Now),
                token);
        }

        private static bool TryValidateSettings(out string error) =>
            EmailSmtpValidator.TryValidate(
                Settings.Default.EmailSmtpHost,
                Settings.Default.EmailSmtpPort,
                Settings.Default.EmailFrom,
                Settings.Default.EmailTo,
                Settings.Default.EmailUsername,
                Settings.Default.EmailPassword,
                out error);

        private static EmailSettings ReadSettings() => new EmailSettings {
            Host = Settings.Default.EmailSmtpHost.Trim(),
            Port = Settings.Default.EmailSmtpPort,
            UseSsl = Settings.Default.EmailUseSsl,
            Username = Settings.Default.EmailUsername.Trim(),
            Password = Settings.Default.EmailPassword,
            From = EmailSmtpValidator.ResolveFromAddress(Settings.Default.EmailFrom, Settings.Default.EmailUsername),
            To = Settings.Default.EmailTo.Trim(),
        };

        private static string BuildDigestBody(DigestPostRequest request) {
            var sb = new StringBuilder();
            var label = string.Equals(request.DigestLabel, "sequence", StringComparison.OrdinalIgnoreCase)
                ? "Sequence digest"
                : "Session digest";
            sb.Append(label);
            if (!string.IsNullOrWhiteSpace(request.SequenceName)) {
                sb.Append(" — ").Append(request.SequenceName);
            }

            sb.Append("\r\nRuns: ").Append(request.Reports?.Count ?? 0);
            if (request.Reports?.Count > 0) {
                var filters = string.Join(", ", request.Reports.Select(r => r.Filter).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct());
                if (!string.IsNullOrWhiteSpace(filters)) {
                    sb.Append("\r\nFilters: ").Append(filters);
                }
            }

            return sb.ToString();
        }

        private sealed class EmailSettings {
            public string Host { get; init; }
            public int Port { get; init; }
            public bool UseSsl { get; init; }
            public string Username { get; init; }
            public string Password { get; init; }
            public string From { get; init; }
            public string To { get; init; }
        }
    }
}
