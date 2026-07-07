using System.ComponentModel.Composition;
using System.Windows;

namespace AutofocusGraphs.SequenceItems {
    [Export(typeof(ResourceDictionary))]
    public partial class AutofocusGraphsSequenceTemplates : ResourceDictionary {
        public AutofocusGraphsSequenceTemplates() {
            InitializeComponent();
        }
    }
}
