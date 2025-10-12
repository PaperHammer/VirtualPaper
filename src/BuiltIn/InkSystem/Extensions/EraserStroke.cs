using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Media.Effects;
using BuiltIn.InkSystem.Core.Brushes;
using BuiltIn.InkSystem.Core.Services;
using BuiltIn.InkSystem.Tool;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI;
using VirtualPaper.Shader;
using Windows.Foundation;
using Windows.UI;

namespace BuiltIn.InkSystem.Extensions {
    public sealed partial record EraserStroke : StrokeBase {
        private byte[] _alphaFadeEraseBytes;

        public override bool IsEraser => true;

        public EraserStroke(BrushGenerateArgs args) : base(args) {
            //IsAddEffectWithCopy = true;
            //IsOnlyEffect = true;
            EffectMode = StrokeMode.OnlyEffect | StrokeMode.AddEffectWithCopy;
        }

        public override async void InitInkBrush(CanvasDevice device) {
            if (BrushArgs == null) return;

            //EffectBrush = BrushManager.GetBrush(BrushArgs, device);
            _alphaFadeEraseBytes = await ShaderUtil.LoadShaderAsync("EraserFadeEffect.hlsl");
        }

        public override void ApplyEffect(CanvasDrawingSession ds, CanvasGeometry geometry, Rect dirty, bool isSinglePoint = false) {
            
        }

        public override void ApplyEffectWithCopy(CanvasRenderTarget source, CanvasDrawingSession ds, CanvasGeometry geometry, Rect dirty, bool isSinglePoint = false) {
            var shaderEffect = new PixelShaderEffect(_alphaFadeEraseBytes) {
                Source1 = source,
                Properties = {
                    ["fadeStrength"] = 0.2f,
                }
            };

            ds.Units = CanvasUnits.Pixels;
            ds.DrawImage(new CropEffect {
                SourceRectangle = dirty,
                Source = shaderEffect
            });

            //ds.Blend = CanvasBlend.SourceOver;
            //if (isSinglePoint)
            //    ds.FillGeometry(geometry, EffectBrush);
            //else
            //    ds.DrawGeometry(geometry, EffectBrush, BrushArgs.Thickness, Style);
        }
    }
}
