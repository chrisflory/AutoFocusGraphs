using System;

namespace AutofocusGraphs {
    /// <summary>
    /// Tracks the most recent Discord post outcome for the options UI.
    /// </summary>
    internal static class PostStatusTracker {
        private static readonly object Gate = new object();
        private static string lastPostStatus = "No posts yet this session.";

        public static event EventHandler Changed;

        public static string LastPostStatus {
            get {
                lock (Gate) {
                    return lastPostStatus;
                }
            }
        }

        public static void RecordSuccess(string summary) {
            Set($"Last post OK {DateTime.Now:HH:mm:ss}: {summary}");
        }

        public static void RecordFailure(string summary) {
            Set($"Last post failed {DateTime.Now:HH:mm:ss}: {summary}");
        }

        private static void Set(string status) {
            lock (Gate) {
                lastPostStatus = status;
            }
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }
}
