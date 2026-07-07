using AutofocusGraphs.Destinations;
using AutofocusGraphs.Properties;
using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Settings = AutofocusGraphs.Properties.Settings;

namespace AutofocusGraphs {
    internal static class SessionDigestService {
        public static IReadOnlyList<AutofocusReport> GetDigestReports() =>
            ReportStore.Instance.GetSessionDigestReports();

        public static IReadOnlyList<AutofocusReport> GetShutdownDigestReports() =>
            GetDigestReports();

        public static IReadOnlyList<AutofocusReport> GetSequenceDigestReports() =>
            ReportStore.Instance.GetSequenceDigestReports();

        public static bool ShouldSkipAutomaticDigest(out string reason) {
            if (ReportStore.Instance.SessionReports.Count > 0) {
                reason = null;
                return false;
            }

            reason = "no new autofocus JSON since NINA opened";
            return true;
        }

        public static async Task PostDigestAsync(CancellationToken token = default) {
            await PostDigestAsync(GetDigestReports(), "session", token, requireMonitoringEnabled: true).ConfigureAwait(false);
        }

        /// <summary>Posts a session digest on NINA exit even if monitoring was disabled before teardown.</summary>
        public static async Task PostShutdownDigestAsync(CancellationToken token = default) {
            if (ShouldSkipAutomaticDigest(out var reason)) {
                Logger.Info($"AutofocusGraphs: skipping shutdown digest — {reason}");
                return;
            }

            var reports = GetShutdownDigestReports();
            if (reports.Count == 0) {
                return;
            }

            await PostDigestAsync(reports, "session", token, requireMonitoringEnabled: false).ConfigureAwait(false);
        }

        public static async Task PostSequenceDigestAsync(CancellationToken token = default) {
            var reports = GetSequenceDigestReports();
            await PostSequenceDigestAsync(reports, token).ConfigureAwait(false);
        }

        public static async Task PostSequenceDigestAsync(IReadOnlyList<AutofocusReport> reports, CancellationToken token = default) {
            var sequenceName = ReportStore.Instance.GetPendingSequenceName();
            await PostSequenceDigestAsync(reports, sequenceName, token).ConfigureAwait(false);
        }

        public static async Task PostSequenceDigestAsync(
            IReadOnlyList<AutofocusReport> reports,
            string sequenceName,
            CancellationToken token = default) {
            if (reports == null || reports.Count == 0) {
                throw new InvalidOperationException("No autofocus reports found for a sequence digest.");
            }

            await PostDigestAsync(reports, "sequence", token, requireMonitoringEnabled: true, sequenceName).ConfigureAwait(false);
            ReportStore.Instance.ClearSequenceReports();
        }

        private static async Task PostDigestAsync(
            IReadOnlyList<AutofocusReport> reports,
            string digestLabel,
            CancellationToken token,
            bool requireMonitoringEnabled,
            string sequenceName = null) {
            if (requireMonitoringEnabled && !Settings.Default.Enabled) {
                throw new InvalidOperationException("AutofocusGraphs monitoring is disabled.");
            }

            if (!AutofocusDestinationRouter.AnyActiveDestination()) {
                throw new InvalidOperationException("No graph posting destination is enabled and configured.");
            }

            if (!AutofocusDestinationRouter.ValidateActiveDestinations(out var error)) {
                throw new InvalidOperationException(error);
            }

            if (reports == null || reports.Count == 0) {
                throw new InvalidOperationException("No autofocus reports found for a digest.");
            }

            await AutofocusDestinationRouter.PostDigestAsync(new DigestPostRequest {
                Reports = reports,
                DigestLabel = digestLabel,
                SequenceName = sequenceName,
            }, token).ConfigureAwait(false);

            Logger.Info($"AutofocusGraphs: posted {digestLabel} digest ({reports.Count} run(s))");
        }
    }
}
