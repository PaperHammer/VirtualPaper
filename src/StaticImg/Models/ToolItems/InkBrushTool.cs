using BuiltIn.Events;
using BuiltIn.InkSystem.Core.Rendering;
using BuiltIn.InkSystem.Core.Services;
using BuiltIn.InkSystem.Extensions;
using Workloads.Creation.StaticImg.Models.Specific;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
    sealed partial class InkBrushTool : CanvasPlotter {
        public InkBrushTool(InkCanvasData data) {
            _data = data;
        }
        
        protected override void InitCurrentStroke(CanvasPointerEventArgs e) {
            var color = e.Pointer.Properties.IsRightButtonPressed ?
                           _data.BackgroundColor : _data.ForegroundColor;

            var brushArgs = new BrushGenerateArgs(
                BrushColor: color, 
                Type: _data.SelectedBrush.Type, 
                Thickness: (float)_data.BrushThickness, 
                Opacity: (float)(_data.BrushOpacity / 100f));
            CurrentStroke = new LineStroke(brushArgs);
            CurrentStroke.InitInkBrush(MainPage.Instance.SharedDevice);
        }

        private readonly InkCanvasData _data;

    }
}
