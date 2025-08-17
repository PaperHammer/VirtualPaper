using Workloads.Creation.StaticImg.Utils.History.Models;

namespace Workloads.Creation.StaticImg.Utils.History.Implements {
    public sealed partial class ArrangeHistory : ICanvasHistory {
        public string Title { get; set; } = string.Empty;
        public HistoryMode Mode => HistoryMode.Arrange;
        public HistoryPropertyMode PropertyMode => HistoryPropertyMode.None;

        public Layerage[] UndoParameter { get; }
        public Layerage[] RedoParameter { get; }

        public ArrangeHistory(Layerage[] undoParameter, Layerage[] redoParameter) {
            this.UndoParameter = undoParameter;
            this.RedoParameter = redoParameter;
        }

        public void Dispose() {
            foreach (Layerage item in this.UndoParameter) {
                item.Dispose();
            }
            foreach (Layerage item in this.RedoParameter) {
                item.Dispose();
            }
        }

        public override string ToString() => this.Title;
    }
}
