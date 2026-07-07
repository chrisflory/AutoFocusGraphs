namespace AutofocusGraphs.Properties {

    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "17.0.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {

        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));

        public static Settings Default {
            get { return defaultInstance; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool UpdateSettings {
            get { return ((bool)(this["UpdateSettings"])); }
            set { this["UpdateSettings"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool Enabled {
            get { return ((bool)(this["Enabled"])); }
            set { this["Enabled"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string WebhookUrl {
            get { return ((string)(this["WebhookUrl"])); }
            set { this["WebhookUrl"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("2")]
        public double UploadDelaySeconds {
            get { return ((double)(this["UploadDelaySeconds"])); }
            set { this["UploadDelaySeconds"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("New autofocus report: **{shortfilename}** ({filter})")]
        public string MessageTemplate {
            get { return ((string)(this["MessageTemplate"])); }
            set { this["MessageTemplate"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool AttachJson {
            get { return ((bool)(this["AttachJson"])); }
            set { this["AttachJson"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool PostPerRun {
            get { return ((bool)(this["PostPerRun"])); }
            set { this["PostPerRun"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool IncludePerRunGraph {
            get { return ((bool)(this["IncludePerRunGraph"])); }
            set { this["IncludePerRunGraph"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool IncludeDigestTrendChart {
            get { return ((bool)(this["IncludeDigestTrendChart"])); }
            set { this["IncludeDigestTrendChart"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool QualityGateEnabled {
            get { return ((bool)(this["QualityGateEnabled"])); }
            set { this["QualityGateEnabled"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("0.90")]
        public double MinR2 {
            get { return ((double)(this["MinR2"])); }
            set { this["MinR2"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("3.0")]
        public double MaxFinalHfr {
            get { return ((double)(this["MaxFinalHfr"])); }
            set { this["MaxFinalHfr"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string FilterQualityProfiles {
            get { return ((string)(this["FilterQualityProfiles"])); }
            set { this["FilterQualityProfiles"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool PostFailures {
            get { return ((bool)(this["PostFailures"])); }
            set { this["PostFailures"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool PostDigestOnShutdown {
            get { return ((bool)(this["PostDigestOnShutdown"])); }
            set { this["PostDigestOnShutdown"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool PostDigestOnSequenceEnd {
            get { return ((bool)(this["PostDigestOnSequenceEnd"])); }
            set { this["PostDigestOnSequenceEnd"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool DigestIncludeTodayFromDisk {
            get { return ((bool)(this["DigestIncludeTodayFromDisk"])); }
            set { this["DigestIncludeTodayFromDisk"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("20")]
        public int DigestTrendMaxRuns {
            get { return ((int)(this["DigestTrendMaxRuns"])); }
            set { this["DigestTrendMaxRuns"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool ShowHyperbolicFit {
            get { return ((bool)(this["ShowHyperbolicFit"])); }
            set { this["ShowHyperbolicFit"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool ShowParabolicFit {
            get { return ((bool)(this["ShowParabolicFit"])); }
            set { this["ShowParabolicFit"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool ShowTrendLines {
            get { return ((bool)(this["ShowTrendLines"])); }
            set { this["ShowTrendLines"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool ShowFocusPositionLine {
            get { return ((bool)(this["ShowFocusPositionLine"])); }
            set { this["ShowFocusPositionLine"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool ShowFilterInGraphTitle {
            get { return ((bool)(this["ShowFilterInGraphTitle"])); }
            set { this["ShowFilterInGraphTitle"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool LabelTrendSegments {
            get { return ((bool)(this["LabelTrendSegments"])); }
            set { this["LabelTrendSegments"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool MinimalGraphMode {
            get { return ((bool)(this["MinimalGraphMode"])); }
            set { this["MinimalGraphMode"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool ShowMeasurePointLabels {
            get { return ((bool)(this["ShowMeasurePointLabels"])); }
            set { this["ShowMeasurePointLabels"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool ShowGraphContextStrip {
            get { return ((bool)(this["ShowGraphContextStrip"])); }
            set { this["ShowGraphContextStrip"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool ShowPreviousFocusMarker {
            get { return ((bool)(this["ShowPreviousFocusMarker"])); }
            set { this["ShowPreviousFocusMarker"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool ShowTrendR2InLegend {
            get { return ((bool)(this["ShowTrendR2InLegend"])); }
            set { this["ShowTrendR2InLegend"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool ShowInitialFocusMarker {
            get { return ((bool)(this["ShowInitialFocusMarker"])); }
            set { this["ShowInitialFocusMarker"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool ShowMeasurePointErrorBars {
            get { return ((bool)(this["ShowMeasurePointErrorBars"])); }
            set { this["ShowMeasurePointErrorBars"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool ShowFitDisagreementWarning {
            get { return ((bool)(this["ShowFitDisagreementWarning"])); }
            set { this["ShowFitDisagreementWarning"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool ShowGraphAnalysisHints {
            get { return ((bool)(this["ShowGraphAnalysisHints"])); }
            set { this["ShowGraphAnalysisHints"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool ConservativeGraphHints {
            get { return ((bool)(this["ConservativeGraphHints"])); }
            set { this["ConservativeGraphHints"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Normal")]
        public string GraphPreviewSample {
            get { return ((string)(this["GraphPreviewSample"])); }
            set { this["GraphPreviewSample"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string DiscordThreadId {
            get { return ((string)(this["DiscordThreadId"])); }
            set { this["DiscordThreadId"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool UseNightlyThreadName {
            get { return ((bool)(this["UseNightlyThreadName"])); }
            set { this["UseNightlyThreadName"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Detailed")]
        public string EmbedMode {
            get { return ((string)(this["EmbedMode"])); }
            set { this["EmbedMode"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Both")]
        public string AttachMode {
            get { return ((string)(this["AttachMode"])); }
            set { this["AttachMode"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool NotifyOnQualityWarning {
            get { return ((bool)(this["NotifyOnQualityWarning"])); }
            set { this["NotifyOnQualityWarning"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string DiscordAlertRoleId {
            get { return ((string)(this["DiscordAlertRoleId"])); }
            set { this["DiscordAlertRoleId"] = value ?? string.Empty; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool PingRoleOnWarning {
            get { return ((bool)(this["PingRoleOnWarning"])); }
            set { this["PingRoleOnWarning"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool PingRoleOnFailure {
            get { return ((bool)(this["PingRoleOnFailure"])); }
            set { this["PingRoleOnFailure"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool PostLiveAfFailures {
            get { return ((bool)(this["PostLiveAfFailures"])); }
            set { this["PostLiveAfFailures"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool DiscordEnabled {
            get { return ((bool)(this["DiscordEnabled"])); }
            set { this["DiscordEnabled"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool TelegramEnabled {
            get { return ((bool)(this["TelegramEnabled"])); }
            set { this["TelegramEnabled"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string TelegramBotToken {
            get { return ((string)(this["TelegramBotToken"])); }
            set { this["TelegramBotToken"] = value; }
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string TelegramChatId {
            get { return ((string)(this["TelegramChatId"])); }
            set { this["TelegramChatId"] = value; }
        }
    }
}
