using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AutoFocusGraphs.Destinations {
    internal static class EmailSmtpClient {
        public static Task SendTestAsync(
            string host,
            int port,
            bool useSsl,
            string username,
            string password,
            string from,
            string to,
            string subject,
            CancellationToken token) =>
            SendAsync(
                host,
                port,
                useSsl,
                username,
                password,
                from,
                to,
                subject,
                "AutoFocusGraphs test — email delivery is configured.",
                null,
                token);

        public static Task SendReportAsync(
            string host,
            int port,
            bool useSsl,
            string username,
            string password,
            string from,
            string to,
            byte[] graphPng,
            string subject,
            string body,
            bool attachJson,
            string jsonFilePath,
            string jsonFileName,
            CancellationToken token) {
            var attachments = new List<EmailAttachment>();
            if (graphPng != null && graphPng.Length > 0) {
                attachments.Add(new EmailAttachment(graphPng, "autofocus_curve.png", "image/png"));
            }

            if (attachJson && !string.IsNullOrWhiteSpace(jsonFilePath) && File.Exists(jsonFilePath)) {
                attachments.Add(new EmailAttachment(
                    File.ReadAllBytes(jsonFilePath),
                    SanitizeFileName(string.IsNullOrWhiteSpace(jsonFileName) ? Path.GetFileName(jsonFilePath) : jsonFileName),
                    "application/json"));
            }

            return SendAsync(host, port, useSsl, username, password, from, to, subject, body, attachments, token);
        }

        public static Task SendFailureAsync(
            string host,
            int port,
            bool useSsl,
            string username,
            string password,
            string from,
            string to,
            string subject,
            string fileName,
            string reason,
            CancellationToken token) {
            var body = $"Autofocus failure: {fileName}\r\n\r\n{reason ?? "Unknown error"}";
            return SendAsync(host, port, useSsl, username, password, from, to, subject, body, null, token);
        }

        public static Task SendDigestAsync(
            string host,
            int port,
            bool useSsl,
            string username,
            string password,
            string from,
            string to,
            byte[] chartPng,
            string subject,
            string body,
            CancellationToken token) {
            var attachments = chartPng != null && chartPng.Length > 0
                ? new List<EmailAttachment> {
                    new EmailAttachment(chartPng, "autofocus_digest.png", "image/png")
                }
                : null;
            return SendAsync(host, port, useSsl, username, password, from, to, subject, body, attachments, token);
        }

        internal static string StripMarkdown(string text) {
            if (string.IsNullOrEmpty(text)) {
                return string.Empty;
            }

            return text.Replace("**", string.Empty, StringComparison.Ordinal);
        }

        internal static void ValidateBridgeFromAddress(string host, string from, string username) {
            if (!IsLocalBridgeHost(host)) {
                return;
            }

            var fromAddress = NormalizeAddress(from);
            var userAddress = NormalizeAddress(username);
            if (string.IsNullOrEmpty(fromAddress) || string.IsNullOrEmpty(userAddress)) {
                return;
            }

            if (!string.Equals(fromAddress, userAddress, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException(
                    $"Proton Mail Bridge only allows sending as your Proton address ({userAddress}). " +
                    $"From is set to {fromAddress}. Use {userAddress}, or a display name like \"NINA Graphs <{userAddress}>\".");
            }
        }

        internal static string FormatSendError(Exception ex) {
            var details = new List<string>();
            for (var current = ex; current != null; current = current.InnerException) {
                if (!string.IsNullOrWhiteSpace(current.Message) &&
                    !details.Any(d => string.Equals(d, current.Message, StringComparison.Ordinal))) {
                    details.Add(current.Message.Trim());
                }
            }

            var message = details.Count == 0 ? "SMTP send failed." : string.Join(" — ", details);
            if (message.Contains("return path", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Invalid sender", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Cannot send emails from address", StringComparison.OrdinalIgnoreCase)) {
                message += " Proton Mail Bridge requires From to be your Proton account email.";
            }

            return message;
        }

        private static async Task SendAsync(
            string host,
            int port,
            bool useSsl,
            string username,
            string password,
            string from,
            string to,
            string subject,
            string body,
            IReadOnlyList<EmailAttachment> attachments,
            CancellationToken token) {
            ValidateBridgeFromAddress(host, from, username);

            var message = BuildMimeMessage(from, to, subject, body, attachments);
            using var client = CreateClient(host);
            var socketOptions = ResolveSocketOptions(host, port, useSsl);

            try {
                await client.ConnectAsync(host.Trim(), port, socketOptions, token).ConfigureAwait(false);

                username = (username ?? string.Empty).Trim();
                password = password ?? string.Empty;
                if (!string.IsNullOrEmpty(username)) {
                    await client.AuthenticateAsync(username, password, token).ConfigureAwait(false);
                }

                await client.SendAsync(message, token).ConfigureAwait(false);
            } catch (Exception ex) {
                Logger.Error($"AutoFocusGraphs: SMTP send failed: {FormatSendError(ex)}");
                throw new InvalidOperationException(FormatSendError(ex), ex);
            } finally {
                if (client.IsConnected) {
                    try {
                        await client.DisconnectAsync(true, CancellationToken.None).ConfigureAwait(false);
                    } catch (Exception ex) {
                        Logger.Warning($"AutoFocusGraphs: SMTP disconnect failed: {ex.Message}");
                    }
                }
            }
        }

        private static MailKit.Net.Smtp.SmtpClient CreateClient(string host) {
            var client = new MailKit.Net.Smtp.SmtpClient();
            if (IsLocalBridgeHost(host)) {
                client.ServerCertificateValidationCallback = (_, _, _, _) => true;
            }

            return client;
        }

        private static SecureSocketOptions ResolveSocketOptions(string host, int port, bool useSsl) {
            if (!useSsl) {
                return SecureSocketOptions.None;
            }

            if (port == 465) {
                return SecureSocketOptions.SslOnConnect;
            }

            return IsLocalBridgeHost(host)
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.StartTlsWhenAvailable;
        }

        private static MimeMessage BuildMimeMessage(
            string from,
            string to,
            string subject,
            string body,
            IReadOnlyList<EmailAttachment> attachments) {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(from.Trim()));
            foreach (var recipient in ParseRecipients(to)) {
                message.To.Add(MailboxAddress.Parse(recipient));
            }

            message.Subject = TrimSubject(subject);
            var builder = new BodyBuilder {
                TextBody = body ?? string.Empty,
            };

            if (attachments != null) {
                foreach (var attachment in attachments) {
                    builder.Attachments.Add(attachment.FileName, attachment.Bytes, ContentType.Parse(attachment.ContentType));
                }
            }

            message.Body = builder.ToMessageBody();
            return message;
        }

        private static bool IsLocalBridgeHost(string host) {
            host = (host ?? string.Empty).Trim();
            return string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || host == "::1";
        }

        private static string NormalizeAddress(string address) {
            address = (address ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(address)) {
                return string.Empty;
            }

            try {
                if (MailboxAddress.TryParse(address, out var mailbox)) {
                    return mailbox.Address;
                }
            } catch {
                // fall through
            }

            return address;
        }

        private static IEnumerable<string> ParseRecipients(string recipients) =>
            recipients
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => part.Length > 0);

        private static string TrimSubject(string subject) {
            subject = subject ?? string.Empty;
            const int max = 200;
            return subject.Length <= max ? subject : subject.Substring(0, max - 1) + "…";
        }

        private static string SanitizeFileName(string fileName) {
            foreach (var c in Path.GetInvalidFileNameChars()) {
                fileName = fileName.Replace(c, '_');
            }

            return string.IsNullOrWhiteSpace(fileName) ? "report.json" : fileName;
        }

        private sealed class EmailAttachment {
            public EmailAttachment(byte[] bytes, string fileName, string contentType) {
                Bytes = bytes;
                FileName = fileName;
                ContentType = contentType;
            }

            public byte[] Bytes { get; }
            public string FileName { get; }
            public string ContentType { get; }
        }
    }
}
