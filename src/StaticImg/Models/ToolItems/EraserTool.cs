using BuiltIn.Events;
using BuiltIn.InkSystem.Core.Rendering;
using BuiltIn.InkSystem.Core.Services;
using BuiltIn.InkSystem.Extensions;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using VirtualPaper.Shader;
using Windows.Foundation;
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

        //protected override void Merge(Rect bounds) {            
        //    using (var ds = RenderTarget.CreateDrawingSession()) {
        //        ds.Blend = CanvasBlend.Copy;
        //        ds.DrawImage(TempRenderTarget, bounds, bounds);
        //    }
        //}
    }
}
