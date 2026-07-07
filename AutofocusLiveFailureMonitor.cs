using AutofocusGraphs.Destinations;
using AutofocusGraphs.Properties;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Settings = AutofocusGraphs.Properties.Settings;

namespace AutofocusGraphs {
    internal static class AutofocusLiveFailureMonitor {
        private static int pendingChecks;

        public static void ScheduleMissingReportCheck(AutoFocusInfo info) {
            if (!Settings.Default.PostLiveAfFailures) {
                return;
            }

            if (!AutofocusRunTracker.Instance.ShouldCheckForMissingReport(out var startedUtc, out var baselineReports)) {
                return;
            }

            var delaySeconds = Math.Max(5, Settings.Default.UploadDelaySeconds + 8);
            Interlocked.Increment(ref pendingChecks);
            _ = Task.Run(async () => {
                try {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), CancellationToken.None).ConfigureAwait(false);
                    await CheckForMissingReportAsync(startedUtc, baselineReports, info).ConfigureAwait(false);
                } catch (Exception ex) {
                    Logger.Warning($"AutofocusGraphs: live AF failure check error: {ex.Message}");
                } finally {
                    Interlocked.Decrement(ref pendingChecks);
                }
            });
        }

        private static async Task CheckForMissingReportAsync(DateTime startedUtc, int baselineReports, AutoFocusInfo info) {
            if (AutofocusRunTracker.Instance.HasNewReportSince(startedUtc, baselineReports)) {
                return;
            }

            var options = PluginRuntimeOptions.FromSettings();
            if (!options.Enabled || !options.PostLiveAfFailures) {
                return;
            }

            if (!AutofocusDestinationRouter.AnyActiveDestination()) {
                return;
            }

            var filter = string.IsNullOrWhiteSpace(info?.Filter) ? "N/A" : info.Filter.Trim();
            var timestamp = info?.Timestamp ?? startedUtc.ToLocalTime();
            var fileName = $"live-af-{timestamp.ToString("yyyy-MM-dd--HH-mm-ss", CultureInfo.InvariantCulture)}.json";
            var reason =
                "Autofocus ended in NINA without a JSON report file. " +
                "The run may have been cancelled, timed out, or failed before NINA wrote a report.";

            if (info != null) {
                reason += $"\nLast known filter: **{filter}** | position: **{info.Position.ToString("0", CultureInfo.InvariantCulture)}** | " +
                           $"temperature: **{info.Temperature.ToString("0.00", CultureInfo.InvariantCulture)}**";
            }

            try {
                await AutofocusDestinationRouter.PostFailureAsync(new FailurePostRequest {
                    FileName = fileName,
                    Reason = reason,
                }, CancellationToken.None).ConfigureAwait(false);
                Logger.Info($"AutofocusGraphs: posted live AF failure for {fileName}");
            } catch (Exception ex) {
                Logger.Error($"AutofocusGraphs: could not post live AF failure: {ex.Message}");
            }
        }
    }
}
