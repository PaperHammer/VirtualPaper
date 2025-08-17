using BuiltIn.Events;
using BuiltIn.InkSystem.Core.Rendering;
using BuiltIn.InkSystem.Core.Services;
using BuiltIn.InkSystem.Extensions;
using Microsoft.UI;
using VirtualPaper.Common.Utils.DI;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
    sealed partial class EraserTool : InteractControl {
        public EraserTool(InkCanvasConfigData data) {
            _data = data;
            OnInitSegement += EraserTool_OnInitSegement;
        }

        private void EraserTool_OnInitSegement(object? sender, CanvasPointerEventArgs e) {
            if (RenderTarget == null) return;

            _curStroke = DomainFactory<EraserStroke>.GetTool(MainPage.Instance);
            _curStroke.Reset((float)_data.EraserSize, BrushShape.Rectangle);
            _curStroke.InkBrush = BrushManager.GetBrush(
                new BrushGenerateArgs(
                    Color: e.Pointer.Properties.IsRightButtonPressed ?
                           _data.BackgroundColor : Colors.Transparent,
                    Type: BrushType.General,
                    Shape: _curStroke.Shape
                ),
                RenderTarget.Device
            );
        }

        private readonly InkCanvasConfigData _data;
    }
}
