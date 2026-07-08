using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.SequenceItem;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace AutoFocusGraphs.SequenceItems {
    [ExportMetadata("Name", "Post AF sequence digest")]
    [ExportMetadata("Description", "Posts the AutoFocusGraphs sequence digest (stats and run list for this sequence) to enabled destinations.")]
    [ExportMetadata("Icon", "Discord_SVG")]
    [ExportMetadata("Category", "AutoFocusGraphs")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class PostSessionDigestInstruction : SequenceItem {
        [ImportingConstructor]
        public PostSessionDigestInstruction() {
        }

        private PostSessionDigestInstruction(PostSessionDigestInstruction copyMe) {
            CopyMetaData(copyMe);
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            progress?.Report(new ApplicationStatus {
                Status = "Posting AutoFocusGraphs sequence digest…"
            });

            return SessionDigestService.PostSequenceDigestAsync(token);
        }

        public override TimeSpan GetEstimatedDuration() => TimeSpan.FromSeconds(5);

        public override object Clone() => new PostSessionDigestInstruction(this);

        public override string ToString() =>
            $"Category: {Category}, Item: {nameof(PostSessionDigestInstruction)}";
    }
}
