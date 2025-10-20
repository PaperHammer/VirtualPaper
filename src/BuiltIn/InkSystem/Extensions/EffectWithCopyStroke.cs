using BuiltIn.InkSystem.Core.Brushes;
using BuiltIn.InkSystem.Core.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI;

namespace BuiltIn.InkSystem.Extensions {
    public sealed partial record EffectWithCopyStroke : StrokeBase {
        public override bool IsEraser => true;

        public EffectWithCopyStroke(BrushGenerateArgs args) : base(args) {
            EffectMode = StrokeMode.Copy;
        }

        // EffectWithCopyStroke.RenderIncrement: 绘制遮罩到 TempRT
        public override void RenderIncrement(CanvasDrawingSession dsTemp, CanvasGeometry geometry) {
            dsTemp.Units = CanvasUnits.Pixels;
            dsTemp.Blend = CanvasBlend.SourceOver;
            if (IsSinglePoint) {
                dsTemp.FillGeometry(geometry, Colors.White);
            }
            else {
                dsTemp.DrawGeometry(geometry, Colors.White, BrushArgs.Thickness, Style);
            }
        }

        // EffectWithCopyStroke.MergeImages: 复杂混合 (Shader)
        public override ICanvasImage MergeImages(
            CanvasRenderTarget foreground,
            CanvasRenderTarget background,
            CanvasDevice device
        ) {
            // 使用 PixelShaderEffect 混合 SnapshotRT (原图) 和 TempRT (遮罩)
            return new PixelShaderEffect(PixelsEffectBytes) {
                Source1 = background,
                Source2 = foreground,
                Properties = { ["eraseAmount"] = BrushArgs.Opacity, }
            };
        }
    }
}
