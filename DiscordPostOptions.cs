using AutoFocusGraphs.Properties;
using System;
using Settings = AutoFocusGraphs.Properties.Settings;

namespace AutoFocusGraphs {
    internal enum EmbedDetailMode {
        Detailed,
        Compact
    }

    internal enum AttachContentMode {
        Both,
        GraphOnly,
        EmbedOnly
    }

    /// <summary>
    /// Shared Discord presentation options for reports, failures, digests, and tests.
    /// </summary>
    internal sealed class DiscordPostOptions {
        public string Username { get; init; }
        public string AvatarUrl { get; init; }
        public string ThreadId { get; init; }
        public bool UseNightlyThreadName { get; init; }
        public EmbedDetailMode EmbedMode { get; init; }
        public AttachContentMode AttachMode { get; init; }
        public bool IncludeDigestTrendChart { get; init; }
        public int DigestTrendMaxRuns { get; init; }

        public static DiscordPostOptions FromSettings() {
            var threadId = (Settings.Default.DiscordThreadId ?? string.Empty).Trim();
            foreach (var c in threadId) {
                if (!char.IsDigit(c)) {
                    threadId = string.Empty;
                    break;
                }
            }

            return new DiscordPostOptions {
                ThreadId = threadId,
                UseNightlyThreadName = Settings.Default.UseNightlyThreadName,
                EmbedMode = string.Equals(Settings.Default.EmbedMode, "Compact", StringComparison.OrdinalIgnoreCase)
                    ? EmbedDetailMode.Compact
                    : EmbedDetailMode.Detailed,
                AttachMode = ParseAttachMode(Settings.Default.AttachMode),
                IncludeDigestTrendChart = Settings.Default.IncludeDigestTrendChart,
                DigestTrendMaxRuns = Math.Clamp(Settings.Default.DigestTrendMaxRuns, 5, 100)
            };
        }

        /// <summary>
        /// Channel-only options (no forum thread_name). Used for webhook tests and fallback posts.
        /// </summary>
        public DiscordPostOptions WithoutNightlyThread() => new DiscordPostOptions {
            ThreadId = ThreadId,
            UseNightlyThreadName = false,
            EmbedMode = EmbedMode,
            AttachMode = AttachMode,
            IncludeDigestTrendChart = IncludeDigestTrendChart,
            DigestTrendMaxRuns = DigestTrendMaxRuns
        };

        private static AttachContentMode ParseAttachMode(string value) {
            if (string.Equals(value, "GraphOnly", StringComparison.OrdinalIgnoreCase)) {
                return AttachContentMode.GraphOnly;
            }
            if (string.Equals(value, "EmbedOnly", StringComparison.OrdinalIgnoreCase)) {
                return AttachContentMode.EmbedOnly;
            }
            return AttachContentMode.Both;
        }
    }
}
