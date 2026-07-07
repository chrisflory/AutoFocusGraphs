using NINA.Sequencer.Interfaces.Mediator;
using System;
using System.IO;

namespace AutofocusGraphs {
    internal static class SequenceNameResolver {
        public static string Resolve(ISequenceMediator sequenceMediator) {
            if (sequenceMediator == null) {
                return "Unnamed sequence";
            }

            try {
                var path = sequenceMediator.GetAdvancedSequencerSavePath();
                if (!string.IsNullOrWhiteSpace(path)) {
                    var name = Path.GetFileNameWithoutExtension(path.Trim());
                    if (!string.IsNullOrWhiteSpace(name)) {
                        return name;
                    }
                }
            } catch {
                // fall through
            }

            return "Unnamed sequence";
        }
    }
}
