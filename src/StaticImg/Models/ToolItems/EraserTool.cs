using BuiltIn.Events;
using BuiltIn.InkSystem.Core.Rendering;
using BuiltIn.InkSystem.Core.Services;
using BuiltIn.InkSystem.Extensions;
using Microsoft.UI;
using Workloads.Creation.StaticImg.Models.Specific;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
    sealed partial class EraserTool : CanvasPlotter {
        public EraserTool(InkCanvasData data) {
            _data = data;
        }

        protected override void InitCurrentStroke(CanvasPointerEventArgs e) {
            var brushArgs = new BrushGenerateArgs(
                BrushColor: Colors.Black,
                Type: _data.SelectedBrush.Type,
                Thickness: (float)_data.EraserSize,
                Opacity: (float)(_data.EraserOpacity / 100f));
            CurrentStroke = new EraserStroke(brushArgs);
            CurrentStroke.InitInkBrush(MainPage.Instance.SharedDevice);
        }

        private readonly InkCanvasData _data;
    }
}
