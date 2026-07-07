namespace AutofocusGraphs {
    internal enum ReportOutcome {
        Success,
        Warning,
        Failure
    }

    internal sealed class QualityResult {
        public ReportOutcome Outcome { get; init; }
        public string Reason { get; init; }

        public int EmbedColor => Outcome switch {
            ReportOutcome.Failure => 0xE74C3C, // red
            ReportOutcome.Warning => 0xF1C40F, // yellow
            _ => 0x7289DA // discord blurple
        };

        public string ContentPrefix => Outcome switch {
            ReportOutcome.Failure => "Autofocus **failed** or incomplete",
            ReportOutcome.Warning => "Autofocus **warning**",
            _ => "New autofocus report"
        };
    }
}
