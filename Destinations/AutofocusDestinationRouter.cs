using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AutoFocusGraphs.Destinations {
    internal static class AutofocusDestinationRouter {
        private static readonly IAutofocusDestination[] Destinations = {
            new DiscordDestination(),
            new TelegramDestination(),
            new SlackDestination(),
            new EmailDestination(),
        };

        public static IReadOnlyList<IAutofocusDestination> All => Destinations;

        public static IEnumerable<IAutofocusDestination> GetActiveDestinations() =>
            Destinations.Where(d => d.IsEnabled && d.IsConfigured);

        public static bool AnyActiveDestination() => GetActiveDestinations().Any();

        public static bool ValidateActiveDestinations(out string firstError) {
            firstError = null;
            foreach (var destination in Destinations.Where(d => d.IsEnabled)) {
                if (!destination.TryValidate(out firstError)) {
                    return false;
                }
            }

            return true;
        }

        public static Task PostReportAsync(ReportPostRequest request, CancellationToken token) =>
            PostToAllEnabledBestEffortAsync((d, ct) => d.PostReportAsync(request, ct), token);

        public static Task PostFailureAsync(FailurePostRequest request, CancellationToken token) =>
            PostToAllEnabledBestEffortAsync((d, ct) => d.PostFailureAsync(request, ct), token);

        public static Task PostDigestAsync(DigestPostRequest request, CancellationToken token) =>
            PostToAllEnabledBestEffortAsync((d, ct) => d.PostDigestAsync(request, ct), token);

        public static async Task TestDestinationAsync(string destinationName, CancellationToken token) {
            var destination = Destinations.FirstOrDefault(d =>
                string.Equals(d.Name, destinationName, StringComparison.OrdinalIgnoreCase));
            if (destination == null) {
                throw new InvalidOperationException($"Unknown destination: {destinationName}");
            }

            if (!destination.IsEnabled) {
                throw new InvalidOperationException($"{destination.Name} posting is disabled.");
            }

            if (!destination.TryValidate(out var error)) {
                throw new InvalidOperationException(error);
            }

            await destination.TestConnectionAsync(token).ConfigureAwait(false);
        }

        public static Task PostToAllEnabledAsync(
            Func<IAutofocusDestination, CancellationToken, Task> action,
            CancellationToken token) =>
            PostToAllEnabledBestEffortAsync(action, token, throwOnAnyFailure: true);

        private static async Task PostToAllEnabledBestEffortAsync(
            Func<IAutofocusDestination, CancellationToken, Task> action,
            CancellationToken token,
            bool throwOnAnyFailure = false) {
            var failures = new List<string>();
            foreach (var destination in GetActiveDestinations()) {
                try {
                    await action(destination, token).ConfigureAwait(false);
                } catch (Exception ex) {
                    failures.Add($"{destination.Name}: {ex.Message}");
                    Logger.Warning($"AutoFocusGraphs: {destination.Name} post failed: {ex.Message}");
                }
            }

            if (failures.Count > 0) {
                var summary = string.Join("; ", failures);
                if (throwOnAnyFailure) {
                    throw new InvalidOperationException(summary);
                }

                Logger.Warning($"AutoFocusGraphs: one or more destinations failed: {summary}");
            }
        }
    }
}
