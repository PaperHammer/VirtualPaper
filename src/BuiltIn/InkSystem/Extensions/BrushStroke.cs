using BuiltIn.InkSystem.Core.Brushes;
using BuiltIn.InkSystem.Core.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Windows.Foundation;

namespace BuiltIn.InkSystem.Extensions {
    public sealed partial record BrushStroke : StrokeBase {
        public BrushStroke(BrushGenerateArgs args) : base(args) { }

        public override void InitInkBrush(CanvasDevice device) {
            InkBrush = BrushManager.GetBrush(BrushArgs, device);
        }

        public override void Render(CanvasDrawingSession dsTemp, CanvasGeometry geometry, Rect? bounds, CanvasRenderTarget? snapshotRenderTarget) {
            if (InkBrush == null) return;

            dsTemp.Blend = CanvasBlend.SourceOver;
            dsTemp.Units = CanvasUnits.Pixels;
            if (IsSinglePoint) {
                dsTemp.FillGeometry(geometry, InkBrush);
            }
            else {
                dsTemp.DrawGeometry(geometry, InkBrush, BrushArgs.Thickness, Style);
            }
        }
    }
}
