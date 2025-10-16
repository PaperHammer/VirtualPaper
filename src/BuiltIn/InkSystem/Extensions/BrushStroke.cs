using BuiltIn.InkSystem.Core.Brushes;
using BuiltIn.InkSystem.Core.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Windows.Foundation;

namespace BuiltIn.InkSystem.Extensions {
    public sealed partial record BrushStroke : StrokeBase {
        public BrushStroke(BrushGenerateArgs args) : base(args) { }

        public override void InitInkBrush(CanvasDevice device) {
            InkBrush = BrushManager.GetBrush(BrushArgs, device);
        }

        //public override void Render(CanvasDrawingSession dsTemp, CanvasGeometry? geometry, Rect? bounds, CanvasRenderTarget? snapshot, CanvasRenderTarget? temp) {
        //    if (InkBrush == null) return;

        //    dsTemp.Blend = CanvasBlend.SourceOver;
        //    dsTemp.Units = CanvasUnits.Pixels;
        //    if (IsSinglePoint) {
        //        dsTemp.FillGeometry(geometry, InkBrush);
        //    }
        //    else {
        //        dsTemp.DrawGeometry(geometry, InkBrush, BrushArgs.Thickness, Style);
        //    }
        //}

        // BrushStroke.RenderIncrement: 绘制颜色到 TempRT
        public override void RenderIncrement(CanvasDrawingSession dsTemp, CanvasGeometry geometry) {
            if (InkBrush == null) return;

            dsTemp.Units = CanvasUnits.Pixels;
            dsTemp.Blend = CanvasBlend.SourceOver;
            if (IsSinglePoint) {
                dsTemp.FillGeometry(geometry, InkBrush);
            }
            else {
                dsTemp.DrawGeometry(geometry, InkBrush, BrushArgs.Thickness, Style);
            }
        }

        // BrushStroke.MergeImages: 简单混合 (SourceOver)
        public override ICanvasImage MergeImages(
            CanvasRenderTarget foreaground,
            CanvasRenderTarget background,
            CanvasDevice device
        ) {
            return new CompositeEffect {
                Mode = CanvasComposite.SourceOver,                
                Sources = { background, foreaground }
            };
        }
    }
}
