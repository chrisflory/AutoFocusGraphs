using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AutoFocusGraphs {
    /// <summary>
    /// Larger graph preview for overlay tuning (updates while open).
    /// </summary>
    internal sealed class GraphPreviewWindow : Window {
        private readonly Image previewImage;

        public GraphPreviewWindow(ImageSource source, string title = null) {
            Title = string.IsNullOrWhiteSpace(title)
                ? "AutoFocusGraphs — graph preview"
                : title;
            Background = new SolidColorBrush(Color.FromRgb(54, 57, 63));
            Width = 1240;
            Height = 780;
            MinWidth = 640;
            MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            previewImage = new Image {
                Source = source,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(8),
            };
            RenderOptions.SetBitmapScalingMode(previewImage, BitmapScalingMode.HighQuality);
            previewImage.SnapsToDevicePixels = true;
            previewImage.UseLayoutRounding = true;
            Content = previewImage;
        }

        public void UpdateImage(ImageSource source) {
            previewImage.Source = source;
        }
    }
}
