using BuiltIn.Events;
using BuiltIn.InkSystem.Core.Rendering;
using BuiltIn.InkSystem.Core.Services;
using BuiltIn.InkSystem.Extensions;
using VirtualPaper.Common.Utils.DI;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
    sealed partial class InkBrushTool : InteractControl {
        public InkBrushTool(InkCanvasConfigData data) {
            _data = data;
            OnInitSegement += InkBrushTool_OnInitSegement;
        }

        private void InkBrushTool_OnInitSegement(object? sender, CanvasPointerEventArgs e) {
            if (RenderTarget == null) return;

            _curStroke = DomainFactory<LineStroke>.GetTool(MainPage.Instance);
            _curStroke.Reset((float)_data.BrushThickness, BrushShape.Circle);
            _curStroke.InkBrush = BrushManager.GetBrush(
                new BrushGenerateArgs(
                    Color: e.Pointer.Properties.IsRightButtonPressed ?
                           _data.BackgroundColor : _data.ForegroundColor,
                    Type: _data.SelectedBrush.Type,
                    Shape: _curStroke.Shape
                ),
                RenderTarget.Device
            );
        }

        private readonly InkCanvasConfigData _data;
    }
}
