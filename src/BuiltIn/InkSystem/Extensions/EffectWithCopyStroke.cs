using BuiltIn.InkSystem.Core.Brushes;
using BuiltIn.InkSystem.Core.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Windows.Foundation;

namespace BuiltIn.InkSystem.Extensions {
    public sealed partial record EffectWithCopyStroke : StrokeBase {
        public override bool IsEraser => true;

        public EffectWithCopyStroke(BrushGenerateArgs args) : base(args) {
            EffectMode = StrokeMode.Copy;
        }

        public override void Render(CanvasDrawingSession dsTemp, CanvasGeometry geometry, Rect? bounds, CanvasRenderTarget? snapshotRenderTarget) {
            if (snapshotRenderTarget == null || bounds == null) return;

            using (dsTemp.CreateLayer(1f, bounds.Value)) {
                dsTemp.Units = CanvasUnits.Pixels;
                dsTemp.Blend = CanvasBlend.Copy;
                foreach (var point in Points) {
                    var shaderEffect = new PixelShaderEffect(PixelsEffectBytes) {
                        Source1 = snapshotRenderTarget,
                        Properties =
                        {
                            ["soft"] = 0,
                            ["eraseAmount"] = BrushArgs.Opacity,
                            ["radius"] = BrushArgs.Thickness / 2f,
                            ["targetPosition"] = point,
                        }
                    };
                    dsTemp.DrawImage(shaderEffect);
                }
            }
        }
    }
}
