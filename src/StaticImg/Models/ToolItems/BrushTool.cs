using Workloads.Creation.StaticImg.Core.Rendering;
using Workloads.Creation.StaticImg.Core.Utils;
using Workloads.Creation.StaticImg.Events;
using Workloads.Creation.StaticImg.Extensions;
using Workloads.Creation.StaticImg.Models.Specific;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
    sealed partial class BrushTool(InkCanvasData data) : CanvasPathDrawer {
        protected override void InitCurrentStroke(CanvasPointerEventArgs e) {
            var color = e.Pointer.Properties.IsRightButtonPressed ?
                           data.BackgroundColor : data.ForegroundColor;
            var brushArgs = new BrushGenerateArgs(
                BrushColor: color, 
                Type: data.SelectedBrush.Type, 
                Thickness: (float)data.BrushThickness, 
                Opacity: (float)(data.BrushOpacity / 100f));
            CurrentStroke = new BrushStroke(brushArgs);
            CurrentStroke.InitInkBrush(MainPage.Instance.SharedDevice);
        }
    }
}
