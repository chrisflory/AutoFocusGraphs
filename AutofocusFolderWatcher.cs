using AutoFocusGraphs.Destinations;
using AutoFocusGraphs.Properties;
using NINA.Core.Utility;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using Settings = AutoFocusGraphs.Properties.Settings;

namespace AutoFocusGraphs {
    /// <summary>
    /// Watches AutoFocus JSON reports, evaluates quality, and posts to configured destinations.
    /// </summary>
    internal sealed class AutofocusFolderWatcher : IDisposable {
        internal const long MaxReportBytes = 2 * 1024 * 1024;

        private readonly Func<PluginRuntimeOptions> getOptions;
        private readonly ConcurrentDictionary<string, byte> inFlight = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        private FileSystemWatcher watcher;
        private CancellationTokenSource cts;
        private bool disposed;

        public AutofocusFolderWatcher(Func<PluginRuntimeOptions> getOptions) {
            this.getOptions = getOptions ?? throw new ArgumentNullException(nameof(getOptions));
        }

        public static string AutoFocusFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NINA", "AutoFocus");

        public void Start() {
            Stop();

            var options = getOptions();
            if (!options.Enabled) {
                Logger.Info("AutoFocusGraphs: monitoring is disabled.");
                return;
            }

            var folder = AutoFocusFolder;
            Directory.CreateDirectory(folder);

            cts = new CancellationTokenSource();
            watcher = new FileSystemWatcher(folder) {
                Filter = "*.json",
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            watcher.Created += OnCreatedOrChanged;
            watcher.Changed += OnCreatedOrChanged;
            watcher.Renamed += OnRenamed;
            watcher.EnableRaisingEvents = true;

            Logger.Info($"AutoFocusGraphs: watching {folder}");
        }

        public void Stop() {
            if (watcher != null) {
                watcher.EnableRaisingEvents = false;
                watcher.Created -= OnCreatedOrChanged;
                watcher.Changed -= OnCreatedOrChanged;
                watcher.Renamed -= OnRenamed;
                watcher.Dispose();
                watcher = null;
            }

            if (cts != null) {
                cts.Cancel();
                cts.Dispose();
                cts = null;
            }
        }

        private void OnRenamed(object sender, RenamedEventArgs e) => OnCreatedOrChanged(sender, e);

        private void OnCreatedOrChanged(object sender, FileSystemEventArgs e) {
            if (disposed) {
                return;
            }

            var options = getOptions();
            if (!options.Enabled) {
                return;
            }

            if (string.IsNullOrWhiteSpace(e.FullPath) || !e.FullPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            if (!inFlight.TryAdd(e.FullPath, 0)) {
                return;
            }

            var token = cts?.Token ?? CancellationToken.None;
            _ = Task.Run(async () => {
                try {
                    await HandleNewFileAsync(e.FullPath, token).ConfigureAwait(false);
                } catch (OperationCanceledException) {
                } catch (Exception ex) {
                    Logger.Error($"AutoFocusGraphs: failed processing {Path.GetFileName(e.FullPath)}: {ex.Message}");
                } finally {
                    await Task.Delay(1000, CancellationToken.None).ConfigureAwait(false);
                    inFlight.TryRemove(e.FullPath, out _);
                }
            }, token);
        }

        private async Task HandleNewFileAsync(string filePath, CancellationToken token) {
            var options = getOptions();

            await WaitForReadableFileAsync(filePath, token).ConfigureAwait(false);

            if (!IsPathInsideAutoFocusFolder(filePath)) {
                Logger.Warning("AutoFocusGraphs: ignored file outside AutoFocus folder.");
                return;
            }

            if (!File.Exists(filePath)) {
                Logger.Warning($"AutoFocusGraphs: file disappeared before upload: {Path.GetFileName(filePath)}");
                return;
            }

            var info = new FileInfo(filePath);
            if (info.Length <= 0) {
                await TryPostFailureAsync(options, info.Name, "Report file is empty.", token).ConfigureAwait(false);
                return;
            }

            if (info.Length > MaxReportBytes) {
                await TryPostFailureAsync(options, info.Name, $"Report file is too large ({info.Length} bytes).", token).ConfigureAwait(false);
                return;
            }

            if (!AutofocusDestinationRouter.ValidateActiveDestinations(out var destinationError)) {
                Logger.Warning($"AutoFocusGraphs: {destinationError}");
                return;
            }

            Logger.Info($"AutoFocusGraphs: processing {info.Name} ({info.Length} bytes)");

            AutofocusReport report = null;
            string failureReason = null;
            try {
                var json = await File.ReadAllTextAsync(filePath, token).ConfigureAwait(false);
                report = AutofocusReport.Parse(json, info.Name, info.FullName);
            } catch (Exception ex) {
                failureReason = ex.Message;
            }

            if (report == null) {
                await TryPostFailureAsync(options, info.Name, failureReason ?? "Unable to parse autofocus report.", token).ConfigureAwait(false);
                return;
            }

            ReportStore.Instance.AddSessionReport(report);
            AutofocusRunTracker.Instance.MarkReportReceived();

            var (minR2, maxFinalHfr, profileLabel) = FilterQualityProfiles.Resolve(
                report.Filter,
                options.FilterQualityProfiles,
                options.MinR2,
                options.MaxFinalHfr);

            var quality = QualityEvaluator.Evaluate(
                report,
                isFailure: false,
                failureReason: null,
                qualityGateEnabled: options.QualityGateEnabled,
                minR2: minR2,
                maxFinalHfr: maxFinalHfr);

            if (quality.Outcome == ReportOutcome.Warning &&
                !string.IsNullOrWhiteSpace(quality.Reason) &&
                profileLabel != "default") {
                quality = new QualityResult {
                    Outcome = quality.Outcome,
                    Reason = $"{quality.Reason} (profile: {profileLabel})"
                };
            }

            if (quality.Outcome == ReportOutcome.Warning && options.NotifyOnQualityWarning) {
                TryNotifyQualityWarning(quality.Reason);
            }

            if (!options.PostPerRun) {
                Logger.Info($"AutoFocusGraphs: stored report for session digest only ({info.Name})");
                return;
            }

            if (!AutofocusDestinationRouter.AnyActiveDestination()) {
                Logger.Info($"AutoFocusGraphs: no posting destination configured ({info.Name})");
                return;
            }

            SequenceRunPostTracker.BeginPost();
            try {
                var delaySeconds = Math.Max(0, Math.Min(options.UploadDelaySeconds, 60));
                if (delaySeconds > 0) {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token).ConfigureAwait(false);
                }

                byte[] graphPng = null;
            var wantsGraph = options.IncludePerRunGraph && options.AttachMode != AttachContentMode.EmbedOnly;
            if (wantsGraph) {
                try {
                    graphPng = AutofocusGraphGenerator.CreatePng(
                        report,
                        options.ShowHyperbolicFit,
                        options.ShowParabolicFit,
                        options.ShowTrendLines,
                        options.ShowFocusPositionLine,
                        options.ShowFilterInGraphTitle,
                        options.LabelTrendSegments,
                        options.MinimalGraphMode,
                        options.ShowMeasurePointLabels,
                        options.ShowGraphContextStrip,
                        options.ShowPreviousFocusMarker,
                        options.ShowTrendR2InLegend,
                        options.ShowInitialFocusMarker,
                        options.ShowMeasurePointErrorBars,
                        options.ShowFitDisagreementWarning,
                        options.ShowGraphAnalysisHints,
                        options.ConservativeGraphHints,
                        options.MinR2,
                        options.MaxFinalHfr);
                    Logger.Info($"AutoFocusGraphs: graph rendered ({graphPng.Length} bytes)");
                } catch (Exception ex) {
                    Logger.Error($"AutoFocusGraphs: graph generation failed, posting embed only: {ex.Message}");
                }
            }

            var attachJson = options.AttachJson;
                await AutofocusDestinationRouter.PostReportAsync(new ReportPostRequest {
                    Report = report,
                    GraphPng = graphPng,
                    MessageTemplate = options.MessageTemplate,
                    AttachJson = attachJson,
                    JsonFilePath = filePath,
                    Quality = quality,
                    SequenceName = ReportStore.Instance.GetPendingSequenceName(),
                }, token).ConfigureAwait(false);
            } finally {
                SequenceRunPostTracker.EndPost();
            }
        }

        private static void TryNotifyQualityWarning(string reason) {
            try {
                SystemSounds.Exclamation.Play();
            } catch {
                // ignore audio failures
            }
            Logger.Warning($"AutoFocusGraphs: quality warning notification: {reason}");
        }

        private static async Task WaitForReadableFileAsync(string filePath, CancellationToken token) {
            for (var attempt = 0; attempt < 20; attempt++) {
                token.ThrowIfCancellationRequested();
                try {
                    if (!File.Exists(filePath)) {
                        await Task.Delay(250, token).ConfigureAwait(false);
                        continue;
                    }

                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    if (stream.Length > 0) {
                        return;
                    }
                } catch (IOException) {
                    // NINA may still be writing the JSON file.
                }

                await Task.Delay(250, token).ConfigureAwait(false);
            }
        }

        private static async Task TryPostFailureAsync(PluginRuntimeOptions options, string fileName, string reason, CancellationToken token) {
            if (!options.PostPerRun) {
                Logger.Warning($"AutoFocusGraphs: failure not posted ({fileName}): {reason}");
                return;
            }
            if (!options.PostFailures) {
                Logger.Warning($"AutoFocusGraphs: failure not posted ({fileName}): {reason}");
                return;
            }
            if (!AutofocusDestinationRouter.AnyActiveDestination()) {
                return;
            }
            try {
                await AutofocusDestinationRouter.PostFailureAsync(new FailurePostRequest {
                    FileName = fileName,
                    Reason = reason,
                    SequenceName = ReportStore.Instance.GetPendingSequenceName(),
                }, token).ConfigureAwait(false);
            } catch (Exception ex) {
                Logger.Error($"AutoFocusGraphs: could not post failure: {ex.Message}");
            }
        }

        private static bool IsPathInsideAutoFocusFolder(string filePath) {
            try {
                var root = Path.GetFullPath(AutoFocusFolder)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                var full = Path.GetFullPath(filePath);
                return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
            } catch {
                return false;
            }
        }


        public void Dispose() {
            if (disposed) {
                return;
            }
            disposed = true;
            Stop();
        }
    }

    internal sealed class PluginRuntimeOptions {
        public bool DiscordEnabled { get; init; }
        public bool Enabled { get; init; }
        public string WebhookUrl { get; init; }
        public double UploadDelaySeconds { get; init; }
        public string MessageTemplate { get; init; }
        public bool AttachJson { get; init; }
        public bool PostPerRun { get; init; }
        public bool IncludePerRunGraph { get; init; }
        public bool QualityGateEnabled { get; init; }
        public double MinR2 { get; init; }
        public double MaxFinalHfr { get; init; }
        public string FilterQualityProfiles { get; init; }
        public bool PostFailures { get; init; }
        public bool PostLiveAfFailures { get; init; }
        public bool ShowHyperbolicFit { get; init; }
        public bool ShowParabolicFit { get; init; }
        public bool ShowTrendLines { get; init; }
        public bool ShowFocusPositionLine { get; init; }
        public bool ShowFilterInGraphTitle { get; init; }
        public bool LabelTrendSegments { get; init; }
        public bool MinimalGraphMode { get; init; }
        public bool ShowMeasurePointLabels { get; init; }
        public bool ShowGraphContextStrip { get; init; }
        public bool ShowPreviousFocusMarker { get; init; }
        public bool ShowTrendR2InLegend { get; init; }
        public bool ShowInitialFocusMarker { get; init; }
        public bool ShowMeasurePointErrorBars { get; init; }
        public bool ShowFitDisagreementWarning { get; init; }
        public bool ShowGraphAnalysisHints { get; init; }
        public bool ConservativeGraphHints { get; init; }
        public bool NotifyOnQualityWarning { get; init; }
        public AttachContentMode AttachMode { get; init; }
        public string DiscordThreadId { get; init; }
        public bool UseNightlyThreadName { get; init; }
        public EmbedDetailMode EmbedMode { get; init; }
        public int DigestTrendMaxRuns { get; init; }

        public DiscordPostOptions ToDiscordPostOptions() => new DiscordPostOptions {
            ThreadId = DiscordThreadId,
            UseNightlyThreadName = UseNightlyThreadName,
            EmbedMode = EmbedMode,
            AttachMode = AttachMode,
            DigestTrendMaxRuns = DigestTrendMaxRuns
        };

        public static PluginRuntimeOptions FromSettings() {
            var post = DiscordPostOptions.FromSettings();
            return new PluginRuntimeOptions {
                Enabled = Settings.Default.Enabled,
                DiscordEnabled = Settings.Default.DiscordEnabled,
                WebhookUrl = Settings.Default.WebhookUrl,
                UploadDelaySeconds = Settings.Default.UploadDelaySeconds,
                MessageTemplate = Settings.Default.MessageTemplate,
                AttachJson = Settings.Default.AttachJson,
                PostPerRun = Settings.Default.PostPerRun,
                IncludePerRunGraph = Settings.Default.IncludePerRunGraph,
                QualityGateEnabled = Settings.Default.QualityGateEnabled,
                MinR2 = Settings.Default.MinR2,
                MaxFinalHfr = Settings.Default.MaxFinalHfr,
                FilterQualityProfiles = Settings.Default.FilterQualityProfiles,
                PostFailures = Settings.Default.PostFailures,
                PostLiveAfFailures = Settings.Default.PostLiveAfFailures,
                ShowHyperbolicFit = Settings.Default.ShowHyperbolicFit,
                ShowParabolicFit = Settings.Default.ShowParabolicFit,
                ShowTrendLines = Settings.Default.ShowTrendLines,
                ShowFocusPositionLine = Settings.Default.ShowFocusPositionLine,
                ShowFilterInGraphTitle = Settings.Default.ShowFilterInGraphTitle,
                LabelTrendSegments = Settings.Default.LabelTrendSegments,
                MinimalGraphMode = Settings.Default.MinimalGraphMode,
                ShowMeasurePointLabels = Settings.Default.ShowMeasurePointLabels,
                ShowGraphContextStrip = Settings.Default.ShowGraphContextStrip,
                ShowPreviousFocusMarker = Settings.Default.ShowPreviousFocusMarker,
                ShowTrendR2InLegend = Settings.Default.ShowTrendR2InLegend,
                ShowInitialFocusMarker = Settings.Default.ShowInitialFocusMarker,
                ShowMeasurePointErrorBars = Settings.Default.ShowMeasurePointErrorBars,
                ShowFitDisagreementWarning = Settings.Default.ShowFitDisagreementWarning,
                ShowGraphAnalysisHints = Settings.Default.ShowGraphAnalysisHints,
                ConservativeGraphHints = Settings.Default.ConservativeGraphHints,
                NotifyOnQualityWarning = Settings.Default.NotifyOnQualityWarning,
                AttachMode = post.AttachMode,
                DiscordThreadId = post.ThreadId,
                UseNightlyThreadName = post.UseNightlyThreadName,
                EmbedMode = post.EmbedMode,
                DigestTrendMaxRuns = post.DigestTrendMaxRuns
            };
        }
    }
}
