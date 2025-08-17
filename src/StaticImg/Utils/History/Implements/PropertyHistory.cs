using Workloads.Creation.StaticImg.Utils.History.Models;

namespace Workloads.Creation.StaticImg.Utils.History {
    public sealed partial class PropertyHistory : ICanvasHistory {
        public string Title { get; set; } = string.Empty;
        public HistoryMode Mode => HistoryMode.Property;
        public HistoryPropertyMode PropertyMode { get; }

        public string Id { get; }
        public object UndoParameter { get; }
        public object RedoParameter { get; }

        public PropertyHistory(HistoryPropertyMode propertyMode, string id, object undoParameter, object redoParameter) {
            this.PropertyMode = propertyMode;
            this.Id = id;
            this.UndoParameter = undoParameter;
            this.RedoParameter = redoParameter;
        }

        public void Dispose() {
        }

        public override string ToString() => this.Title;
    }
}
