using NINA.Core.Utility;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AutofocusGraphs {
    /// <summary>
    /// Reads filter names from the active NINA profile's filter wheel configuration.
    /// </summary>
    internal static class NinaFilterNameProvider {
        public static IReadOnlyList<string> GetActiveProfileFilterNames(IProfileService profileService) {
            try {
                var filters = profileService?.ActiveProfile?.FilterWheelSettings?.FilterWheelFilters;
                if (filters == null || filters.Count == 0) {
                    return Array.Empty<string>();
                }

                var names = new List<string>();
                foreach (var filter in filters) {
                    var name = filter?.Name?.Trim();
                    if (string.IsNullOrWhiteSpace(name)) {
                        continue;
                    }
                    if (names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase))) {
                        continue;
                    }
                    names.Add(name);
                }
                return names;
            } catch (Exception ex) {
                Logger.Warning($"AutofocusGraphs: could not read filter wheel names from NINA profile: {ex.Message}");
                return Array.Empty<string>();
            }
        }
    }
}
