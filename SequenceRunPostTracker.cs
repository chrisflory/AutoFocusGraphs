using System.Threading;
using System.Threading.Tasks;

namespace AutofocusGraphs {
    /// <summary>
    /// Tracks in-flight per-run Discord posts so sequence digests can be sent after the last AF run.
    /// </summary>
    internal static class SequenceRunPostTracker {
        private static int pendingPosts;

        public static bool HasPendingPosts => Volatile.Read(ref pendingPosts) > 0;

        public static void BeginPost() => Interlocked.Increment(ref pendingPosts);

        public static void EndPost() {
            while (true) {
                var current = Volatile.Read(ref pendingPosts);
                if (current <= 0) {
                    return;
                }

                if (Interlocked.CompareExchange(ref pendingPosts, current - 1, current) == current) {
                    return;
                }
            }
        }

        public static async Task WaitForPendingPostsAsync(CancellationToken token, int maxWaitMs) {
            if (!HasPendingPosts) {
                return;
            }

            var elapsedMs = 0;
            while (elapsedMs < maxWaitMs && HasPendingPosts) {
                token.ThrowIfCancellationRequested();
                await Task.Delay(200, token).ConfigureAwait(false);
                elapsedMs += 200;
            }
        }
    }
}
