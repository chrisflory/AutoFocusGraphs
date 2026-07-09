using AutoFocusGraphs.Properties;

using NINA.Core.Utility;

using System;

using System.Collections.Generic;

using System.IO;

using System.Linq;

using System.Windows.Media;

using System.Windows.Media.Imaging;

using Settings = AutoFocusGraphs.Properties.Settings;



namespace AutoFocusGraphs {

    /// <summary>

    /// Renders the options-panel live graph preview from embedded sample autofocus JSON.

    /// </summary>

    internal static class GraphPreviewService {

        public const string SampleNormal = "Normal";

        public const string SampleBacklashTest = "Backlash test";

        public const string SampleLowR2 = "Low R² (erratic curve)";

        public const string SampleHighHfr = "Poor seeing (high HFR)";

        public const string SampleEdgeMinimum = "Minimum near edge";

        public const string SampleOutlier = "Outlier point";

        public const string SampleOvershootBoth = "Overshoot IN+OUT";

        public const string SampleNoisy = "Noisy / wind gusts";

        public const string SampleCoarseStep = "Step too large";

        public const string SampleStepSmall = "Step too small";

        public const string SampleZigzag = "Zigzag / seesaw";

        public const string SampleApproachPlateau = "Flat approach wing";

        public const string SampleMeasurementCliff = "Outer measurement cliff";

        public const string SampleFlatCurve = "Flat scan (seeing)";



        private static readonly IReadOnlyList<(string Key, string ResourceSuffix)> SampleCatalog = new[] {

            (SampleNormal, "graph-preview-sample.json"),

            (SampleBacklashTest, "graph-preview-backlash-sample.json"),

            (SampleLowR2, "graph-preview-low-r2-sample.json"),

            (SampleHighHfr, "graph-preview-high-hfr-sample.json"),

            (SampleEdgeMinimum, "graph-preview-edge-min-sample.json"),

            (SampleOutlier, "graph-preview-outlier-sample.json"),

            (SampleOvershootBoth, "graph-preview-overshoot-both-sample.json"),

            (SampleNoisy, "graph-preview-noisy-sample.json"),

            (SampleCoarseStep, "graph-preview-coarse-step-sample.json"),

            (SampleStepSmall, "graph-preview-step-small-sample.json"),

            (SampleZigzag, "graph-preview-zigzag-sample.json"),

            (SampleApproachPlateau, "graph-preview-approach-plateau-sample.json"),

            (SampleMeasurementCliff, "graph-preview-measurement-cliff-sample.json"),

            (SampleFlatCurve, "graph-preview-flat-curve-sample.json"),

        };



        public static IReadOnlyList<string> AllSampleKeys { get; } =

            SampleCatalog.Select(s => s.Key).ToList();



        private const int LogicalPreviewWidth = 1200;

        private const int LogicalPreviewHeight = 720;

        private const double MaxDpiScale = 2.5;



        private static AutofocusReport cachedSample;

        private static string cachedSampleKey;



        public static void InvalidateSampleCache() {

            cachedSample = null;

            cachedSampleKey = null;

        }



        public static double NormalizeDpiScale(double dpiScale) {

            if (double.IsNaN(dpiScale) || dpiScale < 1.0) {

                return 1.0;

            }



            return Math.Min(dpiScale, MaxDpiScale);

        }



        public static byte[] RenderPreviewPng(double dpiScale = 1.0) {

            var scale = NormalizeDpiScale(dpiScale);

            var width = (int)Math.Round(LogicalPreviewWidth * scale);

            var height = (int)Math.Round(LogicalPreviewHeight * scale);

            var settings = Settings.Default;

            return AutofocusGraphGenerator.CreatePng(

                GetSampleReport(),

                settings.ShowHyperbolicFit,

                settings.ShowParabolicFit,

                settings.ShowTrendLines,

                settings.ShowFocusPositionLine,

                settings.ShowFilterInGraphTitle,

                settings.LabelTrendSegments,

                settings.MinimalGraphMode,

                settings.ShowMeasurePointLabels,

                settings.ShowGraphContextStrip,

                settings.ShowPreviousFocusMarker,

                settings.ShowTrendR2InLegend,

                settings.ShowInitialFocusMarker,

                settings.ShowMeasurePointErrorBars,

                settings.ShowFitDisagreementWarning,

                settings.ShowGraphAnalysisHints,

                settings.ConservativeGraphHints,

                settings.MinR2,

                settings.MaxFinalHfr,

                pixelWidth: width,

                pixelHeight: height);

        }



        public static ImageSource PngBytesToImageSource(byte[] png) {

            if (png == null || png.Length == 0) {

                return null;

            }



            using var ms = new MemoryStream(png);

            var bitmap = new BitmapImage();

            bitmap.BeginInit();

            bitmap.CacheOption = BitmapCacheOption.OnLoad;

            bitmap.StreamSource = ms;

            bitmap.EndInit();

            bitmap.Freeze();

            return bitmap;

        }



        private static AutofocusReport GetSampleReport() {

            var sampleKey = Settings.Default.GraphPreviewSample ?? SampleNormal;

            if (cachedSample != null && string.Equals(cachedSampleKey, sampleKey, StringComparison.Ordinal)) {

                return cachedSample;

            }



            var resourceSuffix = ResolveResourceSuffix(sampleKey);

            var fileName = Path.GetFileName(resourceSuffix);



            var json = LoadEmbeddedSampleJson(resourceSuffix);

            if (string.IsNullOrEmpty(json)) {

                throw new InvalidOperationException($"Embedded graph preview sample {fileName} was not found.");

            }



            cachedSampleKey = sampleKey;

            cachedSample = AutofocusReport.Parse(json, fileName, $"preview:{fileName}");

            return cachedSample;

        }



        private static string ResolveResourceSuffix(string sampleKey) {

            foreach (var entry in SampleCatalog) {

                if (string.Equals(entry.Key, sampleKey, StringComparison.Ordinal)) {

                    return entry.ResourceSuffix;

                }

            }



            Logger.Warning($"AutoFocusGraphs: unknown graph preview sample '{sampleKey}', using Normal.");

            return SampleCatalog[0].ResourceSuffix;

        }



        private static string LoadEmbeddedSampleJson(string resourceSuffix) {

            var asm = typeof(GraphPreviewService).Assembly;

            var name = asm.GetManifestResourceNames()

                .FirstOrDefault(n => n.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));

            if (name == null) {

                Logger.Warning($"AutoFocusGraphs: embedded {resourceSuffix} not found.");

                return null;

            }



            using var stream = asm.GetManifestResourceStream(name);

            if (stream == null) {

                return null;

            }



            using var reader = new StreamReader(stream);

            return reader.ReadToEnd();

        }

    }

}


