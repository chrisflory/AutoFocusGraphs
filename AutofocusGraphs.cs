using AutofocusGraphs.Destinations;
using AutofocusGraphs.Properties;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Interfaces.Mediator;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Settings = AutofocusGraphs.Properties.Settings;

namespace AutofocusGraphs {
    /// <summary>
    /// Watches autofocus JSON reports and posts graphs, quality alerts, and digests to configured destinations.
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class AutofocusGraphsPlugin : PluginBase, INotifyPropertyChanged {
        private AutofocusFolderWatcher watcher;
        private readonly IProfileService profileService;
        private readonly IFocuserMediator focuserMediator;
        private readonly ISequenceMediator sequenceMediator;
        private readonly AutofocusFocuserConsumer focuserConsumer = new AutofocusFocuserConsumer();
        private SequenceDigestCoordinator sequenceDigestCoordinator;
        private string digestStatus = string.Empty;
        private bool? webhookTestResult;
        private string webhookTestToolTip = string.Empty;
        private bool? telegramTestResult;
        private string telegramTestToolTip = string.Empty;
        private ImageSource graphPreviewImage;
        private CancellationTokenSource graphPreviewRefreshCts;
        private bool suppressGraphPreviewRefresh;
        private GraphPreviewWindow expandedGraphPreviewWindow;
        private double graphPreviewDpiScale = 1.0;

        private static readonly string[] GraphOverlayPropertyNames = {
            nameof(MinimalGraphMode),
            nameof(ShowMeasurePointLabels),
            nameof(ShowHyperbolicFit),
            nameof(ShowParabolicFit),
            nameof(ShowTrendLines),
            nameof(LabelTrendSegments),
            nameof(ShowFocusPositionLine),
            nameof(ShowGraphContextStrip),
            nameof(ShowPreviousFocusMarker),
            nameof(ShowTrendR2InLegend),
            nameof(ShowInitialFocusMarker),
            nameof(ShowMeasurePointErrorBars),
            nameof(ShowGraphAnalysisHints),
        };

        private static readonly HashSet<string> GraphPreviewPropertyNames = new HashSet<string>(StringComparer.Ordinal) {
            nameof(ShowHyperbolicFit),
            nameof(ShowParabolicFit),
            nameof(ShowTrendLines),
            nameof(ShowFocusPositionLine),
            nameof(ShowFilterInGraphTitle),
            nameof(LabelTrendSegments),
            nameof(MinimalGraphMode),
            nameof(ShowMeasurePointLabels),
            nameof(ShowGraphContextStrip),
            nameof(ShowPreviousFocusMarker),
            nameof(ShowTrendR2InLegend),
            nameof(ShowInitialFocusMarker),
            nameof(ShowMeasurePointErrorBars),
            nameof(ShowFitDisagreementWarning),
            nameof(ShowGraphAnalysisHints),
            nameof(ConservativeGraphHints),
            nameof(GraphPreviewSample),
        };

        [ImportingConstructor]
        public AutofocusGraphsPlugin(
            IProfileService profileService,
            IFocuserMediator focuserMediator,
            ISequenceMediator sequenceMediator) {
            this.profileService = profileService;
            this.focuserMediator = focuserMediator;
            this.sequenceMediator = sequenceMediator;
            if (Settings.Default.UpdateSettings) {
                Settings.Default.Upgrade();
                Settings.Default.UpdateSettings = false;
                CoreUtil.SaveSettings(Settings.Default);
            }

            const string oldMessageTemplate = "New autofocus report: **{filename}** ({filter})";
            var messageTemplate = (Settings.Default.MessageTemplate ?? string.Empty).Trim();
            if (string.Equals(messageTemplate, oldMessageTemplate, StringComparison.Ordinal)) {
                Settings.Default.MessageTemplate = "New autofocus report: **{shortfilename}** ({filter})";
                CoreUtil.SaveSettings(Settings.Default);
            }

            TestWebhookCommand = new RelayCommand(async _ => await TestDestinationAsync("Discord"));
            TestTelegramCommand = new RelayCommand(async _ => await TestDestinationAsync("Telegram"));
            PostDigestNowCommand = new RelayCommand(async _ => await PostDigestNowAsync());
            GraphOverlaysAllOnCommand = new RelayCommand(_ => SetAllGraphOverlays(allOn: true));
            GraphOverlaysAllOffCommand = new RelayCommand(_ => SetAllGraphOverlays(allOn: false));
            ExpandGraphPreviewCommand = new RelayCommand(_ => ShowExpandedGraphPreview());
            AddFilterProfileCommand = new RelayCommand(_ => AddFilterProfile());
            RemoveFilterProfileCommand = new RelayCommand(p => RemoveFilterProfile(p as FilterProfileRow));
            LoadFilterProfileRows();
            RefreshKnownFilterNames();
            // Reports arrive on watcher threads; KnownFilterNames is bound to WPF, so marshal to the UI thread.
            ReportStore.Instance.Changed += (_, _) => {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess()) {
                    dispatcher.BeginInvoke(new Action(RefreshKnownFilterNames));
                } else {
                    RefreshKnownFilterNames();
                }
            };
            PostStatusTracker.Changed += (_, _) => RaisePropertyChanged(nameof(LastPostStatus));
            try {
                this.focuserMediator?.RegisterConsumer(focuserConsumer);
            } catch (Exception ex) {
                Logger.Warning($"AutofocusGraphs: could not register focuser consumer: {ex.Message}");
            }
            watcher = new AutofocusFolderWatcher(PluginRuntimeOptions.FromSettings);
            watcher.Start();
            sequenceDigestCoordinator = new SequenceDigestCoordinator(sequenceMediator);
            sequenceDigestCoordinator.Start();
        }

        public override Task Initialize() {
            sequenceDigestCoordinator?.Start();
            return base.Initialize();
        }

        public ICommand TestWebhookCommand { get; }
        public ICommand TestTelegramCommand { get; }
        public ICommand PostDigestNowCommand { get; }
        public ICommand GraphOverlaysAllOnCommand { get; }
        public ICommand GraphOverlaysAllOffCommand { get; }
        public ICommand ExpandGraphPreviewCommand { get; }
        public ICommand AddFilterProfileCommand { get; }
        public ICommand RemoveFilterProfileCommand { get; }

        public ObservableCollection<FilterProfileRow> FilterProfileRows { get; } = new ObservableCollection<FilterProfileRow>();

        /// <summary>
        /// Filter names from the NINA profile filter wheel, AF reports, and saved profile rows.
        /// </summary>
        public ObservableCollection<string> KnownFilterNames { get; } = new ObservableCollection<string>();

        private static readonly string[] LegacyPresetNames = {
            "L", "R", "G", "B", "Ha", "OIII", "SII", "Clear", "Lum", "LP"
        };

        public string[] EmbedModeOptions { get; } = { "Detailed", "Compact" };
        public string[] GraphPreviewSampleOptions { get; } = {
            GraphPreviewService.SampleNormal,
            GraphPreviewService.SampleBacklashTest
        };
        public string[] AttachModeOptions { get; } = { "Both", "GraphOnly", "EmbedOnly" };

        public Visibility WebhookTestSuccessVisible =>
            webhookTestResult == true ? Visibility.Visible : Visibility.Collapsed;

        public Visibility WebhookTestFailureVisible =>
            webhookTestResult == false ? Visibility.Visible : Visibility.Collapsed;

        public string WebhookTestToolTip => webhookTestToolTip;

        public Visibility TelegramTestSuccessVisible =>
            telegramTestResult == true ? Visibility.Visible : Visibility.Collapsed;

        public Visibility TelegramTestFailureVisible =>
            telegramTestResult == false ? Visibility.Visible : Visibility.Collapsed;

        public string TelegramTestToolTip => telegramTestToolTip;

        public string LastPostStatus => PostStatusTracker.LastPostStatus;

        public string DigestStatus {
            get => digestStatus;
            private set {
                digestStatus = value ?? string.Empty;
                RaisePropertyChanged();
            }
        }

        private void SetWebhookTestResult(bool? result, string toolTip = null) {
            webhookTestResult = result;
            webhookTestToolTip = toolTip ?? string.Empty;
            RaisePropertyChanged(nameof(WebhookTestSuccessVisible));
            RaisePropertyChanged(nameof(WebhookTestFailureVisible));
            RaisePropertyChanged(nameof(WebhookTestToolTip));
        }

        private void SetTelegramTestResult(bool? result, string toolTip = null) {
            telegramTestResult = result;
            telegramTestToolTip = toolTip ?? string.Empty;
            RaisePropertyChanged(nameof(TelegramTestSuccessVisible));
            RaisePropertyChanged(nameof(TelegramTestFailureVisible));
            RaisePropertyChanged(nameof(TelegramTestToolTip));
        }

        public override async Task Teardown() {
            sequenceDigestCoordinator?.Stop();
            sequenceDigestCoordinator = null;

            try {
                if (Settings.Default.PostDigestOnShutdown &&
                    AutofocusDestinationRouter.AnyActiveDestination()) {
                    await SessionDigestService.PostShutdownDigestAsync().ConfigureAwait(false);
                }
            } catch (Exception ex) {
                Logger.Error($"AutofocusGraphs: shutdown digest failed: {ex.Message}");
            }

            watcher?.Dispose();
            watcher = null;
            try {
                focuserMediator?.RemoveConsumer(focuserConsumer);
            } catch (Exception ex) {
                Logger.Warning($"AutofocusGraphs: could not remove focuser consumer: {ex.Message}");
            }
            await base.Teardown().ConfigureAwait(false);
        }

        private async Task TestDestinationAsync(string destinationName) {
            try {
                if (string.Equals(destinationName, "Discord", StringComparison.OrdinalIgnoreCase)) {
                    if (!DiscordEnabled) {
                        SetWebhookTestResult(false, "Enable Discord posting first.");
                        return;
                    }
                    SetWebhookTestResult(null);
                } else if (string.Equals(destinationName, "Telegram", StringComparison.OrdinalIgnoreCase)) {
                    if (!TelegramEnabled) {
                        SetTelegramTestResult(false, "Enable Telegram posting first.");
                        return;
                    }
                    SetTelegramTestResult(null);
                }

                await AutofocusDestinationRouter.TestDestinationAsync(destinationName, CancellationToken.None).ConfigureAwait(true);
                if (string.Equals(destinationName, "Discord", StringComparison.OrdinalIgnoreCase)) {
                    SetWebhookTestResult(true, "Test message posted to Discord.");
                } else {
                    SetTelegramTestResult(true, "Test message posted to Telegram.");
                }
                RaisePropertyChanged(nameof(StatusText));
                RaisePropertyChanged(nameof(LastPostStatus));
            } catch (Exception ex) {
                if (string.Equals(destinationName, "Discord", StringComparison.OrdinalIgnoreCase)) {
                    SetWebhookTestResult(false, $"Test failed: {ex.Message}");
                } else {
                    SetTelegramTestResult(false, $"Test failed: {ex.Message}");
                }
                RaisePropertyChanged(nameof(LastPostStatus));
                Logger.Error($"AutofocusGraphs: {destinationName} test failed: {ex.Message}");
            }
        }

        private async Task PostDigestNowAsync() {
            try {
                if (!AutofocusDestinationRouter.AnyActiveDestination()) {
                    DigestStatus = "No posting destination is enabled and configured.";
                    return;
                }
                if (!AutofocusDestinationRouter.ValidateActiveDestinations(out var error)) {
                    DigestStatus = error;
                    return;
                }

                var sequenceReports = SessionDigestService.GetSequenceDigestReports();
                if (sequenceReports.Count > 0) {
                    DigestStatus = $"Posting sequence digest ({sequenceReports.Count} run(s))…";
                    await SessionDigestService.PostSequenceDigestAsync().ConfigureAwait(true);
                    DigestStatus = $"Posted sequence digest ({sequenceReports.Count} run(s)).";
                } else {
                    var sessionReports = SessionDigestService.GetDigestReports();
                    if (sessionReports.Count == 0) {
                        DigestStatus = "No autofocus reports found for a digest.";
                        return;
                    }

                    DigestStatus = $"Posting session digest ({sessionReports.Count} run(s))…";
                    await SessionDigestService.PostDigestAsync().ConfigureAwait(true);
                    DigestStatus = $"Posted session digest ({sessionReports.Count} run(s)).";
                }
                RaisePropertyChanged(nameof(StatusText));
                RaisePropertyChanged(nameof(LastPostStatus));
            } catch (Exception ex) {
                DigestStatus = $"Digest failed: {ex.Message}";
                RaisePropertyChanged(nameof(LastPostStatus));
                Logger.Error($"AutofocusGraphs: digest failed: {ex.Message}");
            }
        }


        private void Save() => CoreUtil.SaveSettings(Settings.Default);

        public bool Enabled {
            get => Settings.Default.Enabled;
            set {
                Settings.Default.Enabled = value;
                Save();
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(StatusText));
                RestartWatcher();
            }
        }

        public bool DiscordEnabled {
            get => Settings.Default.DiscordEnabled;
            set {
                Settings.Default.DiscordEnabled = value;
                Save();
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(StatusText));
            }
        }

        public bool TelegramEnabled {
            get => Settings.Default.TelegramEnabled;
            set {
                Settings.Default.TelegramEnabled = value;
                Save();
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(StatusText));
            }
        }

        public string TelegramBotToken {
            get => Settings.Default.TelegramBotToken;
            set {
                Settings.Default.TelegramBotToken = (value ?? string.Empty).Trim();
                Save();
                RaisePropertyChanged();
                SetTelegramTestResult(null);
            }
        }

        public string TelegramChatId {
            get => Settings.Default.TelegramChatId;
            set {
                Settings.Default.TelegramChatId = (value ?? string.Empty).Trim();
                Save();
                RaisePropertyChanged();
                SetTelegramTestResult(null);
            }
        }

        public string WebhookUrl {
            get => Settings.Default.WebhookUrl;
            set {
                Settings.Default.WebhookUrl = (value ?? string.Empty).Trim();
                Save();
                RaisePropertyChanged();
                SetWebhookTestResult(null);
            }
        }

        public string DiscordThreadId {
            get => Settings.Default.DiscordThreadId;
            set {
                var text = (value ?? string.Empty).Trim();
                Settings.Default.DiscordThreadId = text;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool UseNightlyThreadName {
            get => Settings.Default.UseNightlyThreadName;
            set {
                Settings.Default.UseNightlyThreadName = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public string EmbedMode {
            get => Settings.Default.EmbedMode;
            set {
                Settings.Default.EmbedMode = string.Equals(value, "Compact", StringComparison.OrdinalIgnoreCase)
                    ? "Compact"
                    : "Detailed";
                Save();
                RaisePropertyChanged();
            }
        }

        public string AttachMode {
            get => Settings.Default.AttachMode;
            set {
                if (string.Equals(value, "GraphOnly", StringComparison.OrdinalIgnoreCase)) {
                    Settings.Default.AttachMode = "GraphOnly";
                } else if (string.Equals(value, "EmbedOnly", StringComparison.OrdinalIgnoreCase)) {
                    Settings.Default.AttachMode = "EmbedOnly";
                } else {
                    Settings.Default.AttachMode = "Both";
                }
                Save();
                RaisePropertyChanged();
            }
        }

        public double UploadDelaySeconds {
            get => Settings.Default.UploadDelaySeconds;
            set {
                Settings.Default.UploadDelaySeconds = value < 0 ? 0 : (value > 60 ? 60 : value);
                Save();
                RaisePropertyChanged();
            }
        }

        public string MessageTemplate {
            get => Settings.Default.MessageTemplate;
            set {
                var text = string.IsNullOrWhiteSpace(value)
                    ? "New autofocus report: **{shortfilename}** ({filter})"
                    : value.Trim();
                if (text.Length > 500) {
                    text = text.Substring(0, 500);
                }
                Settings.Default.MessageTemplate = text;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool AttachJson {
            get => Settings.Default.AttachJson;
            set {
                Settings.Default.AttachJson = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool PostPerRun {
            get => Settings.Default.PostPerRun;
            set {
                Settings.Default.PostPerRun = value;
                Save();
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(StatusText));
            }
        }

        public bool IncludePerRunGraph {
            get => Settings.Default.IncludePerRunGraph;
            set {
                Settings.Default.IncludePerRunGraph = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool QualityGateEnabled {
            get => Settings.Default.QualityGateEnabled;
            set {
                Settings.Default.QualityGateEnabled = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public double MinR2 {
            get => Settings.Default.MinR2;
            set {
                Settings.Default.MinR2 = value < 0 ? 0 : (value > 1 ? 1 : value);
                Save();
                RaisePropertyChanged();
            }
        }

        public double MaxFinalHfr {
            get => Settings.Default.MaxFinalHfr;
            set {
                Settings.Default.MaxFinalHfr = value < 0.1 ? 0.1 : value;
                Save();
                RaisePropertyChanged();
            }
        }

        private void LoadFilterProfileRows() {
            FilterProfileRows.Clear();
            foreach (var row in FilterQualityProfiles.ToRows(Settings.Default.FilterQualityProfiles, SaveFilterProfileRows)) {
                FilterProfileRows.Add(row);
            }
        }

        private void RefreshKnownFilterNames() {
            // Only add/move/remove carefully — never Clear(). Clearing resets editable ComboBox text
            // on other profile rows and was wiping filter names when adding another row.
            var desired = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Consider(string filter) {
                filter = filter?.Trim();
                if (string.IsNullOrWhiteSpace(filter) || filter == "N/A" || !seen.Add(filter)) {
                    return;
                }
                desired.Add(filter);
            }

            foreach (var name in NinaFilterNameProvider.GetActiveProfileFilterNames(profileService)) {
                Consider(name);
            }

            foreach (var report in ReportStore.Instance.SessionReports) {
                Consider(report?.Filter);
            }

            try {
                foreach (var report in ReportStore.Instance.LoadFromDisk(100)) {
                    Consider(report?.Filter);
                }
            } catch {
                // ignore disk errors while building suggestions
            }

            foreach (var row in FilterProfileRows) {
                Consider(row?.Filter);
            }

            var rowFilters = new HashSet<string>(
                FilterProfileRows
                    .Select(r => r?.Filter?.Trim())
                    .Where(n => !string.IsNullOrWhiteSpace(n)),
                StringComparer.OrdinalIgnoreCase);

            foreach (var name in desired) {
                var existingIndex = IndexOfFilterName(name);
                if (existingIndex < 0) {
                    KnownFilterNames.Add(name);
                }
            }

            for (var i = KnownFilterNames.Count - 1; i >= 0; i--) {
                var existing = KnownFilterNames[i];
                var keep = desired.Any(d => string.Equals(d, existing, StringComparison.OrdinalIgnoreCase))
                           || rowFilters.Contains(existing);
                if (!keep && IsLegacyPreset(existing)) {
                    KnownFilterNames.RemoveAt(i);
                }
            }

            for (var targetIndex = 0; targetIndex < desired.Count; targetIndex++) {
                var name = desired[targetIndex];
                var currentIndex = IndexOfFilterName(name);
                if (currentIndex >= 0 && currentIndex != targetIndex) {
                    KnownFilterNames.Move(currentIndex, targetIndex);
                }
            }
        }

        private int IndexOfFilterName(string name) {
            for (var i = 0; i < KnownFilterNames.Count; i++) {
                if (string.Equals(KnownFilterNames[i], name, StringComparison.OrdinalIgnoreCase)) {
                    return i;
                }
            }
            return -1;
        }

        private static bool IsLegacyPreset(string name) {
            foreach (var preset in LegacyPresetNames) {
                if (string.Equals(preset, name, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }

        private void AddFilterProfile() {
            FilterProfileRows.Add(new FilterProfileRow(
                SaveFilterProfileRows,
                filter: string.Empty,
                minR2: Settings.Default.MinR2,
                maxFinalHfr: Settings.Default.MaxFinalHfr));
            // Do not refresh known filters here — that used to clear ComboBox text on other rows.
        }

        private void RemoveFilterProfile(FilterProfileRow row) {
            if (row == null) {
                return;
            }
            FilterProfileRows.Remove(row);
            SaveFilterProfileRows();
        }

        private void SaveFilterProfileRows() {
            Settings.Default.FilterQualityProfiles = FilterQualityProfiles.Serialize(FilterProfileRows);
            Save();
            RefreshKnownFilterNames();
        }

        public bool PostFailures {
            get => Settings.Default.PostFailures;
            set {
                Settings.Default.PostFailures = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool PostDigestOnShutdown {
            get => Settings.Default.PostDigestOnShutdown;
            set {
                Settings.Default.PostDigestOnShutdown = value;
                Save();
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(StatusText));
            }
        }

        public bool PostDigestOnSequenceEnd {
            get => Settings.Default.PostDigestOnSequenceEnd;
            set {
                Settings.Default.PostDigestOnSequenceEnd = value;
                Save();
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(StatusText));
            }
        }

        public bool IncludeDigestTrendChart {
            get => Settings.Default.IncludeDigestTrendChart;
            set {
                Settings.Default.IncludeDigestTrendChart = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool DigestIncludeTodayFromDisk {
            get => Settings.Default.DigestIncludeTodayFromDisk;
            set {
                Settings.Default.DigestIncludeTodayFromDisk = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public int DigestTrendMaxRuns {
            get => Settings.Default.DigestTrendMaxRuns;
            set {
                Settings.Default.DigestTrendMaxRuns = value < 5 ? 5 : (value > 100 ? 100 : value);
                Save();
                RaisePropertyChanged();
            }
        }

        public bool ShowHyperbolicFit {
            get => Settings.Default.ShowHyperbolicFit;
            set {
                Settings.Default.ShowHyperbolicFit = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool ShowParabolicFit {
            get => Settings.Default.ShowParabolicFit;
            set {
                Settings.Default.ShowParabolicFit = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool ShowTrendLines {
            get => Settings.Default.ShowTrendLines;
            set {
                Settings.Default.ShowTrendLines = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool ShowFocusPositionLine {
            get => Settings.Default.ShowFocusPositionLine;
            set {
                Settings.Default.ShowFocusPositionLine = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool ShowFilterInGraphTitle {
            get => Settings.Default.ShowFilterInGraphTitle;
            set {
                Settings.Default.ShowFilterInGraphTitle = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool LabelTrendSegments {
            get => Settings.Default.LabelTrendSegments;
            set {
                Settings.Default.LabelTrendSegments = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool MinimalGraphMode {
            get => Settings.Default.MinimalGraphMode;
            set {
                Settings.Default.MinimalGraphMode = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool ShowMeasurePointLabels {
            get => Settings.Default.ShowMeasurePointLabels;
            set {
                Settings.Default.ShowMeasurePointLabels = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool ShowGraphContextStrip {
            get => Settings.Default.ShowGraphContextStrip;
            set {
                Settings.Default.ShowGraphContextStrip = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool ShowPreviousFocusMarker {
            get => Settings.Default.ShowPreviousFocusMarker;
            set {
                Settings.Default.ShowPreviousFocusMarker = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool ShowTrendR2InLegend {
            get => Settings.Default.ShowTrendR2InLegend;
            set {
                Settings.Default.ShowTrendR2InLegend = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool ShowInitialFocusMarker {
            get => Settings.Default.ShowInitialFocusMarker;
            set {
                Settings.Default.ShowInitialFocusMarker = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool ShowMeasurePointErrorBars {
            get => Settings.Default.ShowMeasurePointErrorBars;
            set {
                Settings.Default.ShowMeasurePointErrorBars = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool ShowFitDisagreementWarning {
            get => Settings.Default.ShowFitDisagreementWarning;
            set {
                Settings.Default.ShowFitDisagreementWarning = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool ShowGraphAnalysisHints {
            get => Settings.Default.ShowGraphAnalysisHints;
            set {
                Settings.Default.ShowGraphAnalysisHints = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool ConservativeGraphHints {
            get => Settings.Default.ConservativeGraphHints;
            set {
                Settings.Default.ConservativeGraphHints = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public string GraphPreviewSample {
            get {
                var value = Settings.Default.GraphPreviewSample;
                return string.IsNullOrWhiteSpace(value) ? GraphPreviewService.SampleNormal : value;
            }
            set {
                var normalized = string.IsNullOrWhiteSpace(value) ? GraphPreviewService.SampleNormal : value;
                if (Array.IndexOf(GraphPreviewSampleOptions, normalized) < 0) {
                    normalized = GraphPreviewService.SampleNormal;
                }
                Settings.Default.GraphPreviewSample = normalized;
                Save();
                GraphPreviewService.InvalidateSampleCache();
                RaisePropertyChanged();
                ScheduleGraphPreviewRefresh();
            }
        }

        public ImageSource GraphPreviewImage {
            get => graphPreviewImage;
            private set {
                graphPreviewImage = value;
                RaisePropertyChanged();
            }
        }

        public void UpdateGraphPreviewDpiScale(Visual visual) {
            if (visual == null) {
                return;
            }

            var source = PresentationSource.FromVisual(visual);
            var scale = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            var normalized = GraphPreviewService.NormalizeDpiScale(scale);
            if (Math.Abs(normalized - graphPreviewDpiScale) < 0.01) {
                return;
            }

            graphPreviewDpiScale = normalized;
            ScheduleGraphPreviewRefresh();
        }

        public void ScheduleGraphPreviewRefresh() {
            graphPreviewRefreshCts?.Cancel();
            graphPreviewRefreshCts = new CancellationTokenSource();
            var token = graphPreviewRefreshCts.Token;
            var dpiScale = graphPreviewDpiScale;

            Task.Run(async () => {
                try {
                    await Task.Delay(150, token).ConfigureAwait(false);
                    var png = GraphPreviewService.RenderPreviewPng(dpiScale);
                    token.ThrowIfCancellationRequested();
                    var image = GraphPreviewService.PngBytesToImageSource(png);
                    var dispatcher = Application.Current?.Dispatcher;
                    if (dispatcher == null) {
                        return;
                    }

                    _ = dispatcher.BeginInvoke(new Action(() => {
                        if (!token.IsCancellationRequested) {
                            GraphPreviewImage = image;
                            expandedGraphPreviewWindow?.UpdateImage(image);
                        }
                    }));
                } catch (OperationCanceledException) {
                    // superseded by a newer preview request
                } catch (Exception ex) {
                    Logger.Warning($"AutofocusGraphs: graph preview failed: {ex.Message}");
                }
            }, token);
        }

        private void SetAllGraphOverlays(bool allOn) {
            Settings.Default.MinimalGraphMode = !allOn;
            Settings.Default.ShowMeasurePointLabels = allOn;
            Settings.Default.ShowHyperbolicFit = allOn;
            Settings.Default.ShowParabolicFit = allOn;
            Settings.Default.ShowTrendLines = allOn;
            Settings.Default.LabelTrendSegments = allOn;
            Settings.Default.ShowFocusPositionLine = allOn;
            Settings.Default.ShowGraphContextStrip = allOn;
            Settings.Default.ShowPreviousFocusMarker = allOn;
            Settings.Default.ShowTrendR2InLegend = allOn;
            Settings.Default.ShowInitialFocusMarker = allOn;
            Settings.Default.ShowMeasurePointErrorBars = allOn;
            Settings.Default.ShowGraphAnalysisHints = allOn;
            Save();

            suppressGraphPreviewRefresh = true;
            try {
                foreach (var name in GraphOverlayPropertyNames) {
                    RaisePropertyChanged(name);
                }
            } finally {
                suppressGraphPreviewRefresh = false;
            }

            ScheduleGraphPreviewRefresh();
        }

        private void ShowExpandedGraphPreview() {
            if (GraphPreviewImage == null) {
                ScheduleGraphPreviewRefresh();
            }

            if (expandedGraphPreviewWindow != null) {
                expandedGraphPreviewWindow.UpdateImage(GraphPreviewImage);
                expandedGraphPreviewWindow.Activate();
                return;
            }

            expandedGraphPreviewWindow = new GraphPreviewWindow(GraphPreviewImage);
            expandedGraphPreviewWindow.Loaded += (_, _) => UpdateGraphPreviewDpiScale(expandedGraphPreviewWindow);
            expandedGraphPreviewWindow.Closed += (_, _) => expandedGraphPreviewWindow = null;
            expandedGraphPreviewWindow.Show();
        }

        public bool NotifyOnQualityWarning {
            get => Settings.Default.NotifyOnQualityWarning;
            set {
                Settings.Default.NotifyOnQualityWarning = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public string DiscordAlertRoleId {
            get => Settings.Default.DiscordAlertRoleId;
            set {
                Settings.Default.DiscordAlertRoleId = (value ?? string.Empty).Trim();
                Save();
                RaisePropertyChanged();
            }
        }

        public bool PingRoleOnWarning {
            get => Settings.Default.PingRoleOnWarning;
            set {
                Settings.Default.PingRoleOnWarning = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool PingRoleOnFailure {
            get => Settings.Default.PingRoleOnFailure;
            set {
                Settings.Default.PingRoleOnFailure = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public bool PostLiveAfFailures {
            get => Settings.Default.PostLiveAfFailures;
            set {
                Settings.Default.PostLiveAfFailures = value;
                Save();
                RaisePropertyChanged();
            }
        }

        public string WatchFolder => AutofocusFolderWatcher.AutoFocusFolder;

        /// <summary>Called when the options panel is shown so filter wheel names stay current.</summary>
        public void RefreshFilterList() => RefreshKnownFilterNames();

        public string StatusText {
            get {
                if (!Enabled) {
                    return "Disabled.";
                }

                if (!AutofocusDestinationRouter.AnyActiveDestination()) {
                    if (!AutofocusDestinationRouter.ValidateActiveDestinations(out var configError)) {
                        return configError;
                    }

                    return "Enabled, but no posting destination is configured.";
                }

                var mode = !PostPerRun && (PostDigestOnSequenceEnd || PostDigestOnShutdown)
                    ? "digest-only"
                    : !PostPerRun
                        ? "collecting only"
                        : "watching";
                var seqCount = ReportStore.Instance.SequenceReports.Count;
                var sessionCount = ReportStore.Instance.SessionReports.Count;
                var destinations = string.Join(" + ", AutofocusDestinationRouter.GetActiveDestinations().Select(d => d.Name));
                return $"{char.ToUpper(mode[0])}{mode.Substring(1)} {WatchFolder} via {destinations} ({seqCount} this sequence · {sessionCount} session)";
            }
        }

        private void RestartWatcher() {
            try {
                watcher?.Start();
                RaisePropertyChanged(nameof(StatusText));
            } catch (Exception ex) {
                Logger.Error(ex);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (!suppressGraphPreviewRefresh &&
                propertyName != null &&
                GraphPreviewPropertyNames.Contains(propertyName)) {
                ScheduleGraphPreviewRefresh();
            }
            if (propertyName == nameof(Enabled) || propertyName == nameof(WebhookUrl) ||
                propertyName == nameof(DiscordEnabled) || propertyName == nameof(TelegramEnabled) ||
                propertyName == nameof(TelegramBotToken) || propertyName == nameof(TelegramChatId) ||
                propertyName == nameof(PostPerRun) || propertyName == nameof(PostDigestOnShutdown) ||
                propertyName == nameof(PostDigestOnSequenceEnd)) {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
            }
        }
    }
}
