using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AutoFocusGraphs {
    [Export(typeof(ResourceDictionary))]
    public partial class Options : ResourceDictionary {
        private bool emailPasswordInitializing;

        public Options() {
            InitializeComponent();
        }

        private void OptionsPanel_Loaded(object sender, RoutedEventArgs e) {
            if (sender is FrameworkElement element && element.DataContext is AutoFocusGraphsPlugin plugin) {
                plugin.UpdateGraphPreviewDpiScale(element);
                plugin.RefreshFilterList();
                plugin.ScheduleGraphPreviewRefresh();
                plugin.EnsureDriftChartPreview();
                plugin.ScheduleDriftChartPreviewRefresh();
            }
        }

        private void FlowchartImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (sender is FrameworkElement element && element.DataContext is AutoFocusGraphsPlugin plugin) {
                if (plugin.ExpandFlowchartCommand?.CanExecute(null) == true) {
                    plugin.ExpandFlowchartCommand.Execute(null);
                }
            }
        }

        private void EmailPassword_Loaded(object sender, RoutedEventArgs e) {
            if (sender is not PasswordBox box) {
                return;
            }

            if (box.DataContext is not AutoFocusGraphsPlugin plugin) {
                return;
            }

            emailPasswordInitializing = true;
            try {
                box.Password = plugin.EmailPassword ?? string.Empty;
            } finally {
                emailPasswordInitializing = false;
            }
        }

        private void EmailPassword_Changed(object sender, RoutedEventArgs e) {
            if (emailPasswordInitializing || sender is not PasswordBox box) {
                return;
            }

            if (box.DataContext is AutoFocusGraphsPlugin plugin) {
                plugin.EmailPassword = box.Password;
            }
        }

        private async void TestEmail_Click(object sender, RoutedEventArgs e) {
            if (sender is not FrameworkElement element) {
                return;
            }

            if (element.DataContext is not AutoFocusGraphsPlugin plugin) {
                return;
            }

            await plugin.TestEmailAsync().ConfigureAwait(true);
        }
    }
}
