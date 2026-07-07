using AutofocusGraphs.Destinations;
using AutofocusGraphs.Properties;
using NINA.Core.Utility;
using NINA.Sequencer.Interfaces.Mediator;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Settings = AutofocusGraphs.Properties.Settings;

namespace AutofocusGraphs {
    /// <summary>
    /// Posts an autofocus digest when the Advanced (or simple) sequencer run finishes.
    /// </summary>
    internal sealed class SequenceDigestCoordinator {
        private readonly ISequenceMediator sequenceMediator;
        private CancellationTokenSource subscribeCts;
        private bool subscribed;

        public SequenceDigestCoordinator(ISequenceMediator sequenceMediator) {
            this.sequenceMediator = sequenceMediator;
        }

        public void Start() {
            if (subscribed || sequenceMediator == null) {
                return;
            }

            if (subscribeCts != null) {
                return;
            }

            subscribeCts = new CancellationTokenSource();
            _ = WaitAndSubscribeAsync(subscribeCts.Token);
        }

        public void Stop() {
            subscribeCts?.Cancel();
            subscribeCts?.Dispose();
            subscribeCts = null;

            if (subscribed && sequenceMediator != null) {
                sequenceMediator.SequenceStarting -= OnSequenceStarting;
                sequenceMediator.SequenceFinished -= OnSequenceFinished;
                subscribed = false;
            }
        }

        private async Task WaitAndSubscribeAsync(CancellationToken token) {
            try {
                while (!token.IsCancellationRequested && sequenceMediator != null && !sequenceMediator.Initialized) {
                    await Task.Delay(100, token).ConfigureAwait(false);
                }

                if (token.IsCancellationRequested || sequenceMediator == null) {
                    return;
                }

                sequenceMediator.SequenceStarting += OnSequenceStarting;
                sequenceMediator.SequenceFinished += OnSequenceFinished;
                subscribed = true;
                Logger.Info("AutofocusGraphs: listening for sequencer start/finish (per-sequence digest).");
            } catch (OperationCanceledException) {
                // shutting down
            } catch (Exception ex) {
                Logger.Warning($"AutofocusGraphs: could not subscribe to sequencer events: {ex.Message}");
            }
        }

        private Task OnSequenceStarting(object sender, EventArgs e) {
            ReportStore.Instance.BeginSequence();
            ReportStore.Instance.SetPendingSequenceName(SequenceNameResolver.Resolve(sequenceMediator));
            Logger.Info("AutofocusGraphs: sequence started — tracking autofocus runs for sequence digest.");
            return Task.CompletedTask;
        }

        private async Task OnSequenceFinished(object sender, EventArgs e) {
            var sequenceName = ReportStore.Instance.RecordSequenceCompleted();

            if (!Settings.Default.Enabled || !Settings.Default.PostDigestOnSequenceEnd) {
                return;
            }

            if (!AutofocusDestinationRouter.AnyActiveDestination() ||
                !AutofocusDestinationRouter.ValidateActiveDestinations(out _)) {
                return;
            }

            if (SessionDigestService.ShouldSkipAutomaticDigest(out var skipReason)) {
                Logger.Info($"AutofocusGraphs: skipping sequence digest — {skipReason}");
                return;
            }

            var snapshot = await WaitAndSnapshotSequenceReportsAsync(CancellationToken.None).ConfigureAwait(false);
            if (snapshot == null || snapshot.Count == 0) {
                Logger.Info("AutofocusGraphs: sequence finished with no autofocus runs — skipping digest.");
                return;
            }

            try {
                await SessionDigestService.PostSequenceDigestAsync(snapshot, sequenceName, CancellationToken.None).ConfigureAwait(false);
                Logger.Info($"AutofocusGraphs: sequence digest posted after sequence finished ({snapshot.Count} run(s)).");
            } catch (Exception ex) {
                Logger.Error($"AutofocusGraphs: sequence digest failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Waits for AF JSON in ReportStore, for any per-run Discord posts to finish, then snapshots reports.
        /// </summary>
        private static async Task<IReadOnlyList<AutofocusReport>> WaitAndSnapshotSequenceReportsAsync(CancellationToken token) {
            var uploadDelayMs = (int)Math.Clamp(Settings.Default.UploadDelaySeconds * 1000, 0, 60000);
            var maxWaitMs = Math.Max(15000, uploadDelayMs + 12000);
            var idleExitMs = 2500;
            var elapsedMs = 0;

            while (elapsedMs < maxWaitMs) {
                token.ThrowIfCancellationRequested();

                if (ReportStore.Instance.GetSequenceDigestReports().Count > 0) {
                    break;
                }

                if (AutofocusRunTracker.Instance.HasPendingReport()) {
                    await Task.Delay(500, token).ConfigureAwait(false);
                    elapsedMs += 500;
                    continue;
                }

                if (elapsedMs >= idleExitMs) {
                    return null;
                }

                await Task.Delay(500, token).ConfigureAwait(false);
                elapsedMs += 500;
            }

            if (ReportStore.Instance.GetSequenceDigestReports().Count == 0) {
                return null;
            }

            if (Settings.Default.PostPerRun) {
                var postWaitMs = Math.Max(30000, uploadDelayMs + 20000);
                await SequenceRunPostTracker.WaitForPendingPostsAsync(token, postWaitMs).ConfigureAwait(false);
                if (SequenceRunPostTracker.HasPendingPosts) {
                    Logger.Warning("AutofocusGraphs: sequence digest proceeding while per-run posts are still in flight.");
                }
            }

            return ReportStore.Instance.GetSequenceDigestReports();
        }
    }
}
