using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AutoFocusGraphs {
    /// <summary>
    /// Larger preview window (graph overlays or flowchart). Optional Ctrl+wheel zoom with scroll pan.
    /// </summary>
    internal sealed class GraphPreviewWindow : Window {
        private readonly Image previewImage;
        private readonly bool enableZoom;
        private readonly ScaleTransform scaleTransform;
        private readonly Grid host;
        private readonly ScrollViewer scrollViewer;
        private double zoom = 1.0;

        public GraphPreviewWindow(ImageSource source, string title = null, bool enableZoom = false) {
            Title = string.IsNullOrWhiteSpace(title)
                ? "AutoFocusGraphs — graph preview"
                : title;
            Background = new SolidColorBrush(Color.FromRgb(54, 57, 63));
            Width = enableZoom ? 1280 : 1240;
            Height = enableZoom ? 720 : 780;
            MinWidth = 640;
            MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.enableZoom = enableZoom;

            previewImage = new Image {
                Source = source,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(8),
            };
            RenderOptions.SetBitmapScalingMode(previewImage, BitmapScalingMode.HighQuality);
            previewImage.SnapsToDevicePixels = true;
            previewImage.UseLayoutRounding = true;

            if (!enableZoom) {
                Content = previewImage;
                return;
            }

            scaleTransform = new ScaleTransform(1.0, 1.0);
            host = new Grid {
                LayoutTransform = scaleTransform,
                ClipToBounds = false,
            };
            host.Children.Add(previewImage);

            scrollViewer = new ScrollViewer {
                Content = host,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(0),
            };
            scrollViewer.SizeChanged += (_, _) => FitHostToViewport();
            scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel;
            Content = scrollViewer;
            ToolTip = "Resize window to scale · Ctrl+mouse wheel to zoom · scroll to pan when zoomed";
            Loaded += (_, _) => FitHostToViewport();
        }

        public void UpdateImage(ImageSource source) {
            previewImage.Source = source;
            if (enableZoom) {
                FitHostToViewport();
            }
        }

        private void FitHostToViewport() {
            if (!enableZoom || scrollViewer == null || host == null) {
                return;
            }

            // Layout box tracks the viewport so Stretch.Uniform fills the window.
            // LayoutTransform zoom then enlarges that fitted result (scrollbars appear when zoom > 1).
            var w = Math.Max(32, scrollViewer.ViewportWidth);
            var h = Math.Max(32, scrollViewer.ViewportHeight);
            if (scrollViewer.ViewportWidth <= 0 || scrollViewer.ViewportHeight <= 0) {
                w = Math.Max(32, scrollViewer.ActualWidth);
                h = Math.Max(32, scrollViewer.ActualHeight);
            }

            host.Width = w;
            host.Height = h;
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            if (!enableZoom || (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) {
                return;
            }

            e.Handled = true;
            var factor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
            zoom = Math.Max(0.5, Math.Min(4.0, zoom * factor));
            scaleTransform.ScaleX = zoom;
            scaleTransform.ScaleY = zoom;
            FitHostToViewport();
        }
    }
}
