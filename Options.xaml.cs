using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;

namespace AutofocusGraphs {
    [Export(typeof(ResourceDictionary))]
    public partial class Options : ResourceDictionary {
        public Options() {
            InitializeComponent();
        }

        private void OptionsPanel_Loaded(object sender, RoutedEventArgs e) {
            if (sender is FrameworkElement element && element.DataContext is AutofocusGraphsPlugin plugin) {
                plugin.UpdateGraphPreviewDpiScale(element);
                plugin.RefreshFilterList();
                plugin.ScheduleGraphPreviewRefresh();
            }
        }
    }
}
