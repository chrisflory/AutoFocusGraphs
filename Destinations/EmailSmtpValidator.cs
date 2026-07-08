using System;
using System.Linq;
using System.Net.Mail;

namespace AutoFocusGraphs.Destinations {
    internal static class EmailSmtpValidator {
        public static bool TryValidateHost(string host, out string error) {
            host = (host ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(host)) {
                error = "SMTP host is required (e.g. smtp.gmail.com).";
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryValidatePort(int port, out string error) {
            if (port is < 1 or > 65535) {
                error = "SMTP port must be between 1 and 65535.";
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryValidateAddress(string address, string label, out string error) {
            address = (address ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(address)) {
                error = $"{label} is required.";
                return false;
            }

            try {
                _ = new MailAddress(address);
            } catch (FormatException) {
                error = $"{label} is not a valid email address.";
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryValidateRecipients(string recipients, out string error) {
            recipients = (recipients ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(recipients)) {
                error = "At least one recipient (To) is required.";
                return false;
            }

            var parts = recipients
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToList();
            if (parts.Count == 0) {
                error = "At least one recipient (To) is required.";
                return false;
            }

            foreach (var part in parts) {
                try {
                    _ = new MailAddress(part);
                } catch (FormatException) {
                    error = $"Recipient address is not valid: {part}";
                    return false;
                }
            }

            error = null;
            return true;
        }

        public static bool TryValidate(
            string host,
            int port,
            string from,
            string to,
            string username,
            string password,
            out string error) {
            if (!TryValidateHost(host, out error)) {
                return false;
            }

            if (!TryValidatePort(port, out error)) {
                return false;
            }

            from = ResolveFromAddress(from, username);
            if (!TryValidateAddress(from, "From address", out error)) {
                return false;
            }

            username = (username ?? string.Empty).Trim();
            password = password ?? string.Empty;
            if (!string.IsNullOrEmpty(username) && string.IsNullOrWhiteSpace(password)) {
                error = "SMTP password is required when a username is set.";
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryValidateRecipientsOrDefault(string to, string from, string username, out string effectiveTo, out string error) {
            effectiveTo = (to ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(effectiveTo)) {
                return TryValidateRecipients(effectiveTo, out error);
            }

            var resolvedFrom = ResolveFromAddress(from, username);
            if (string.IsNullOrEmpty(resolvedFrom)) {
                error = "To address is required (or set From / SMTP username to a valid email for self-test).";
                effectiveTo = null;
                return false;
            }

            effectiveTo = resolvedFrom;
            error = null;
            return true;
        }

        internal static string ResolveFromAddress(string from, string username) {
            from = (from ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(from)) {
                return from;
            }

            username = (username ?? string.Empty).Trim();
            return TryValidateAddress(username, "SMTP username", out _) ? username : string.Empty;
        }
    }
}
