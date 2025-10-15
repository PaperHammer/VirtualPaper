using BuiltIn.Events;
using BuiltIn.InkSystem.Core.Rendering;
using BuiltIn.InkSystem.Core.Services;
using BuiltIn.InkSystem.Extensions;
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
