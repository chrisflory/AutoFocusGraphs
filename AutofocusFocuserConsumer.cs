using NINA.Equipment.Equipment.MyFocuser;
using NINA.Equipment.Interfaces.Mediator;
using OxyPlot;

namespace AutofocusGraphs {
    /// <summary>
    /// Receives live autofocus lifecycle callbacks from NINA's focuser mediator.
    /// </summary>
    internal sealed class AutofocusFocuserConsumer : IFocuserConsumer {
        public void AutoFocusRunStarting() {
            AutofocusRunTracker.Instance.MarkRunStarting();
        }

        public void NewAutoFocusPoint(DataPoint point) {
        }

        public void UpdateUserFocused(FocuserInfo info) {
        }

        public void UpdateEndAutoFocusRun(AutoFocusInfo info) {
            AutofocusLiveFailureMonitor.ScheduleMissingReportCheck(info);
        }

        public void UpdateDeviceInfo(FocuserInfo deviceInfo) {
        }

        public void Dispose() {
        }
    }
}
