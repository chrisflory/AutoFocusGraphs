using System;

namespace AutofocusGraphs {
    /// <summary>
    /// Ensures outbound posts only go to Discord webhook endpoints (SSRF protection).
    /// </summary>
    internal static class WebhookUrlValidator {
        public static bool TryValidate(string url, out string normalized, out string error) {
            normalized = null;
            error = null;

            if (string.IsNullOrWhiteSpace(url)) {
                error = "Webhook URL is empty.";
                return false;
            }

            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) {
                error = "Webhook URL is not a valid absolute URI.";
                return false;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) {
                error = "Webhook URL must use HTTPS.";
                return false;
            }

            var host = uri.IdnHost;
            var isDiscordHost =
                string.Equals(host, "discord.com", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(host, "discordapp.com", StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith(".discord.com", StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith(".discordapp.com", StringComparison.OrdinalIgnoreCase);

            if (!isDiscordHost) {
                error = "Webhook URL host must be discord.com or discordapp.com.";
                return false;
            }

            // https://discord.com/api/webhooks/{id}/{token}
            // also allow /api/v10/webhooks/...
            var path = uri.AbsolutePath.TrimEnd('/');
            if (!path.StartsWith("/api/webhooks/", StringComparison.OrdinalIgnoreCase) &&
                !path.StartsWith("/api/v", StringComparison.OrdinalIgnoreCase)) {
                error = "Webhook URL path must be a Discord webhook endpoint.";
                return false;
            }

            if (path.StartsWith("/api/v", StringComparison.OrdinalIgnoreCase) &&
                path.IndexOf("/webhooks/", StringComparison.OrdinalIgnoreCase) < 0) {
                error = "Webhook URL path must be a Discord webhook endpoint.";
                return false;
            }

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            // api, webhooks, id, token  OR  api, v10, webhooks, id, token
            var webhooksIndex = Array.FindIndex(segments, s => string.Equals(s, "webhooks", StringComparison.OrdinalIgnoreCase));
            if (webhooksIndex < 0 || segments.Length < webhooksIndex + 3) {
                error = "Webhook URL must include webhook id and token segments.";
                return false;
            }

            normalized = uri.GetLeftPart(UriPartial.Path);
            if (!string.IsNullOrEmpty(uri.Query)) {
                // Discord webhooks do not need query strings; drop them.
                normalized = uri.GetLeftPart(UriPartial.Path);
            }

            return true;
        }
    }
}
