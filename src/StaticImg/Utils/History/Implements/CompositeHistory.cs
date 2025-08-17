using Workloads.Creation.StaticImg.Utils.History.Models;

namespace Workloads.Creation.StaticImg.Utils.History.Implements {
    public sealed partial class CompositeHistory : ICanvasHistory {
        public string Title { get; set; } = string.Empty;
        public HistoryMode Mode => HistoryMode.Composite;
        public HistoryPropertyMode PropertyMode => HistoryPropertyMode.None;

        public ICanvasHistory[] Histories { get; }

        public CompositeHistory(params ICanvasHistory[] histories) {
            this.Histories = histories;
        }

        public void Dispose() {
            foreach (ICanvasHistory item in this.Histories) {
                item.Dispose();
            }
        }

        public override string ToString() => this.Title;
    }
}
