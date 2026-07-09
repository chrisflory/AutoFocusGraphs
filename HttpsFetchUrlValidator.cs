using System;
using System.Net;
using System.Net.Sockets;

namespace AutoFocusGraphs {
    /// <summary>
    /// SSRF guards for outbound HTTPS fetches (avatar images, Slack upload URLs).
    /// </summary>
    internal static class HttpsFetchUrlValidator {
        public static bool TryValidateImageFetchUrl(string url, out string error) =>
            TryValidateHttpsUrl(url, IsAllowedImageHost, out error);

        public static bool TryValidateSlackUploadUrl(string url, out string error) =>
            TryValidateHttpsUrl(url, IsAllowedSlackUploadHost, out error);

        private static bool TryValidateHttpsUrl(
            string url,
            Func<string, bool> hostAllowed,
            out string error) {
            error = null;
            if (string.IsNullOrWhiteSpace(url)) {
                error = "URL is empty.";
                return false;
            }

            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) {
                error = "URL is not a valid absolute URI.";
                return false;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) {
                error = "URL must use HTTPS.";
                return false;
            }

            var host = uri.IdnHost;
            if (IsBlockedNetworkHost(host)) {
                error = "URL points to a private or local network address.";
                return false;
            }

            if (!hostAllowed(host)) {
                error = "URL host is not allowed.";
                return false;
            }

            return true;
        }

        private static bool IsBlockedNetworkHost(string host) {
            if (string.IsNullOrWhiteSpace(host)) {
                return true;
            }

            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            if (host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            if (!IPAddress.TryParse(host, out var ip)) {
                return false;
            }

            if (IPAddress.IsLoopback(ip)) {
                return true;
            }

            if (ip.AddressFamily == AddressFamily.InterNetwork) {
                var bytes = ip.GetAddressBytes();
                if (bytes[0] == 10) {
                    return true;
                }

                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) {
                    return true;
                }

                if (bytes[0] == 192 && bytes[1] == 168) {
                    return true;
                }

                if (bytes[0] == 169 && bytes[1] == 254) {
                    return true;
                }

                if (bytes[0] == 127) {
                    return true;
                }
            }

            if (ip.AddressFamily == AddressFamily.InterNetworkV6) {
                if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal) {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAllowedImageHost(string host) {
            return host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
                   host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
                   host.EndsWith(".discordapp.com", StringComparison.OrdinalIgnoreCase) ||
                   host.EndsWith(".discord.com", StringComparison.OrdinalIgnoreCase) ||
                   host.EndsWith(".discordapp.net", StringComparison.OrdinalIgnoreCase) ||
                   host.EndsWith(".discord.media", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAllowedSlackUploadHost(string host) =>
            host.Equals("slack.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".slack.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".slack-edge.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".slack-files.com", StringComparison.OrdinalIgnoreCase);
    }
}
