using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AutofocusGraphs.Destinations {
    internal static class AutofocusDestinationRouter {
        private static readonly IAutofocusDestination[] Destinations = {
            new DiscordDestination(),
            new TelegramDestination(),
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

        public static async Task PostReportAsync(ReportPostRequest request, CancellationToken token) {
            foreach (var destination in GetActiveDestinations()) {
                await destination.PostReportAsync(request, token).ConfigureAwait(false);
            }
        }

        public static async Task PostFailureAsync(FailurePostRequest request, CancellationToken token) {
            foreach (var destination in GetActiveDestinations()) {
                await destination.PostFailureAsync(request, token).ConfigureAwait(false);
            }
        }

        public static async Task PostDigestAsync(DigestPostRequest request, CancellationToken token) {
            foreach (var destination in GetActiveDestinations()) {
                await destination.PostDigestAsync(request, token).ConfigureAwait(false);
            }
        }

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

        public static async Task PostToAllEnabledAsync(
            Func<IAutofocusDestination, CancellationToken, Task> action,
            CancellationToken token) {
            var failures = new List<string>();
            foreach (var destination in GetActiveDestinations()) {
                try {
                    await action(destination, token).ConfigureAwait(false);
                } catch (Exception ex) {
                    failures.Add($"{destination.Name}: {ex.Message}");
                    Logger.Warning($"AutofocusGraphs: {destination.Name} post failed: {ex.Message}");
                }
            }

            if (failures.Count > 0) {
                throw new InvalidOperationException(string.Join("; ", failures));
            }
        }
    }
}
