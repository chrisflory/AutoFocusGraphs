using System.ComponentModel.Composition;
using System.Windows;

namespace AutoFocusGraphs.SequenceItems {
    [Export(typeof(ResourceDictionary))]
    public partial class AutoFocusGraphsSequenceTemplates : ResourceDictionary {
        public AutoFocusGraphsSequenceTemplates() {
            InitializeComponent();
        }
    }
}
