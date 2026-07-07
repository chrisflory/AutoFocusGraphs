using System.Threading;
using System.Threading.Tasks;

namespace AutofocusGraphs.Destinations {
    /// <summary>
    /// Delivers autofocus graphs and summaries to an external channel (Discord, Telegram, etc.).
    /// </summary>
    internal interface IAutofocusDestination {
        string Name { get; }

        bool IsEnabled { get; }

        bool IsConfigured { get; }

        bool TryValidate(out string error);

        Task PostReportAsync(ReportPostRequest request, CancellationToken token);

        Task PostFailureAsync(FailurePostRequest request, CancellationToken token);

        Task PostDigestAsync(DigestPostRequest request, CancellationToken token);

        Task TestConnectionAsync(CancellationToken token);
    }
}
