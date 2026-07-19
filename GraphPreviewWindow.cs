using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AutoFocusGraphs {
    /// <summary>
    /// Larger preview window (graph overlays or flowchart). Optional scroll + mouse-wheel zoom.
    /// </summary>
    internal sealed class GraphPreviewWindow : Window {
        private readonly Image previewImage;
        private readonly ScaleTransform scaleTransform;
        private readonly bool enableZoom;
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
            previewImage.LayoutTransform = scaleTransform;
            previewImage.Stretch = Stretch.None;
            previewImage.HorizontalAlignment = HorizontalAlignment.Left;
            previewImage.VerticalAlignment = VerticalAlignment.Top;

            var scroll = new ScrollViewer {
                Content = previewImage,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(4),
            };
            scroll.PreviewMouseWheel += OnPreviewMouseWheel;
            Content = scroll;
            ToolTip = "Scroll to pan · Ctrl+mouse wheel to zoom";
        }

        public void UpdateImage(ImageSource source) {
            previewImage.Source = source;
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
        }
    }
}
