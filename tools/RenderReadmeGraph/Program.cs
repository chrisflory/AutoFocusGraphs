using AutoFocusGraphs;
using System;
using System.IO;

namespace RenderReadmeGraph {
    internal static class Program {
        private static int Main(string[] args) {
            try {
                var repoRoot = FindRepoRoot();
                var jsonPath = Path.Combine(repoRoot, "assets", "graph-preview-sample.json");
                var outputPath = args.Length > 0
                    ? Path.GetFullPath(args[0])
                    : Path.Combine(repoRoot, "assets", "_readme-graph-temp.png");

                var json = File.ReadAllText(jsonPath);
                var report = AutofocusReport.Parse(json, "graph-preview-sample.json");
                var png = AutofocusGraphGenerator.CreatePng(
                    report,
                    showHyperbolicFit: true,
                    showParabolicFit: true,
                    showTrendLines: true,
                    showFocusPositionLine: true,
                    showFilterInTitle: true,
                    labelTrendSegments: true,
                    minimalMode: false,
                    showMeasurePointLabels: true,
                    showGraphContextStrip: true,
                    showPreviousFocusMarker: true,
                    showTrendR2InLegend: true,
                    showInitialFocusMarker: true,
                    showMeasurePointErrorBars: true,
                    showFitDisagreementWarning: true,
                    showGraphAnalysisHints: true,
                    pixelWidth: 1200,
                    pixelHeight: 720);

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? repoRoot);
                File.WriteAllBytes(outputPath, png);
                Console.WriteLine(outputPath);
                return 0;
            } catch (Exception ex) {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static string FindRepoRoot() {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null) {
                if (File.Exists(Path.Combine(dir.FullName, "AutoFocusGraphs.csproj"))) {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException("Could not locate AutoFocusGraphs repository root.");
        }
    }
}
