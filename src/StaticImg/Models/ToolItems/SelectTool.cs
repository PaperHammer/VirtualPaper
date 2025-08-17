using BuiltIn.InkSystem.Core.Rendering;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
    sealed partial class SelectionTool : CanvasAreaSelector {
        public SelectionTool(InkCanvasConfigData data) {
            _data = data;
            OnSelectRectChanged += SelectionTool_OnSelectRectChanged;
        }

        private void SelectionTool_OnSelectRectChanged(object? sender, Windows.Foundation.Rect e) {
            _data.SelectionRect = e;
        }

        private readonly InkCanvasConfigData _data;
    }
}
