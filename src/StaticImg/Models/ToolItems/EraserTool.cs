using Microsoft.UI;
using VirtualPaper.Shader;
using Workloads.Creation.StaticImg.Core.Rendering;
using Workloads.Creation.StaticImg.Core.Utils;
using Workloads.Creation.StaticImg.Events;
using Workloads.Creation.StaticImg.Extensions;
using Workloads.Creation.StaticImg.Models.Specific;

namespace Workloads.Creation.StaticImg.Models.ToolItems {
    sealed partial class EraserTool(InkCanvasData data) : CanvasPathDrawer {
        protected override void InitCurrentStroke(CanvasPointerEventArgs e) {
            var brushArgs = new BrushGenerateArgs(
                BrushColor: Colors.Black,
                Type: data.SelectedBrush.Type,
                Thickness: (float)data.EraserSize,
                Opacity: (float)(data.EraserOpacity / 100f));
            CurrentStroke = new EffectWithCopyStroke(brushArgs);
            CurrentStroke.InitPixelsEffect(ShaderType.GeometryAlphaEraseEffect);
        }
    }
}
