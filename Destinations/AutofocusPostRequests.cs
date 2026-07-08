using System.Collections.Generic;

namespace AutoFocusGraphs.Destinations {
    internal sealed class ReportPostRequest {
        public AutofocusReport Report { get; init; }
        public byte[] GraphPng { get; init; }
        public string MessageTemplate { get; init; }
        public bool AttachJson { get; init; }
        public string JsonFilePath { get; init; }
        public QualityResult Quality { get; init; }
        public string SequenceName { get; init; }
    }

    internal sealed class FailurePostRequest {
        public string FileName { get; init; }
        public string Reason { get; init; }
        public string SequenceName { get; init; }
    }

    internal sealed class DigestPostRequest {
        public IReadOnlyList<AutofocusReport> Reports { get; init; }
        public string DigestLabel { get; init; }
        public string SequenceName { get; init; }
    }
}
