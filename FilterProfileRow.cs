using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace AutoFocusGraphs {
    /// <summary>
    /// One editable per-filter quality row in the options UI.
    /// </summary>
    public sealed class FilterProfileRow : INotifyPropertyChanged {
        private readonly Action onChanged;
        private string filter = string.Empty;
        private string minR2Text = "0.90";
        private string maxFinalHfrText = "3.0";

        public FilterProfileRow(Action onChanged) {
            this.onChanged = onChanged;
        }

        public FilterProfileRow(Action onChanged, string filter, double minR2, double maxFinalHfr)
            : this(onChanged) {
            this.filter = filter ?? string.Empty;
            minR2Text = minR2.ToString("0.##", CultureInfo.InvariantCulture);
            maxFinalHfrText = maxFinalHfr.ToString("0.##", CultureInfo.InvariantCulture);
        }

        public string Filter {
            get => filter;
            set {
                filter = value ?? string.Empty;
                OnPropertyChanged();
                onChanged?.Invoke();
            }
        }

        public string MinR2Text {
            get => minR2Text;
            set {
                minR2Text = value ?? string.Empty;
                OnPropertyChanged();
                onChanged?.Invoke();
            }
        }

        public string MaxFinalHfrText {
            get => maxFinalHfrText;
            set {
                maxFinalHfrText = value ?? string.Empty;
                OnPropertyChanged();
                onChanged?.Invoke();
            }
        }

        public bool TryGetValues(out string name, out double minR2, out double maxFinalHfr) {
            name = (filter ?? string.Empty).Trim();
            minR2 = 0;
            maxFinalHfr = 0;
            if (name.Length == 0) {
                return false;
            }
            if (!double.TryParse(minR2Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out minR2) &&
                !double.TryParse(minR2Text?.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out minR2)) {
                return false;
            }
            if (!double.TryParse(maxFinalHfrText?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out maxFinalHfr) &&
                !double.TryParse(maxFinalHfrText?.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out maxFinalHfr)) {
                return false;
            }
            minR2 = Math.Clamp(minR2, 0, 1);
            maxFinalHfr = Math.Max(0.1, maxFinalHfr);
            return true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
