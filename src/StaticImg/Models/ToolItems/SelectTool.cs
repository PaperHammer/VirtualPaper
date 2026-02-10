using Workloads.Creation.StaticImg.Core.Rendering;
using Workloads.Creation.StaticImg.Models.Specific;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
    sealed partial class SelectionTool : CanvasAreaSelector {
        public SelectionTool(InkCanvasData data) {
            _data = data;
            OnSelectRectChanged += SelectionTool_OnSelectRectChanged;
        }

        private void SelectionTool_OnSelectRectChanged(object? sender, Windows.Foundation.Rect e) {
            _data.SelectionRect = e;
        }

        private readonly InkCanvasData _data;
    }
}
