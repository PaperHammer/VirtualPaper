using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using System.Numerics;

namespace VirtualPaper.Shader.Core {
    /// <summary>
    /// Core engine: apply <see cref="ShaderType"/> + <see cref="EffectParams"/> to an <see cref="ICanvasImage"/>.
    /// </summary>
    public static class EffectApplier {
        public static ICanvasImage Apply(ShaderType type, EffectParams p, ICanvasImage source) {
            return type switch {
                // ═══ Adjustment ═══
                ShaderType.Grayscale => new GrayscaleEffect { Source = source },
                ShaderType.Invert => new InvertEffect { Source = source },
                ShaderType.Exposure => new ExposureEffect { Exposure = p.Value, Source = source },
                ShaderType.Brightness => new ColorMatrixEffect { ColorMatrix = Matrix5x4Extension.Brightness(Clamp(p.Value, -1, 1)), Source = source },
                ShaderType.Saturation => new SaturationEffect { Saturation = p.Value, Source = source },
                ShaderType.HueRotation => new HueRotationEffect { Angle = p.Value * MathF.PI / 180f, Source = source },
                ShaderType.Contrast => new ContrastEffect { Contrast = Clamp(p.Value, -1, 1), Source = source },
                ShaderType.TemperatureAndTint => new TemperatureAndTintEffect { Temperature = Clamp(p.Value, -1, 1), Tint = Clamp(p.Value2, -1, 1), Source = source },
                ShaderType.HighlightsAndShadows => new HighlightsAndShadowsEffect { Shadows = Clamp(p.Value, -1, 1), Highlights = Clamp(p.Value2, -1, 1), Clarity = Clamp(p.Value3, -1, 1), MaskBlurAmount = Clamp(p.Value4, 0, 10), Source = source },

                // ═══ Adjustment2 ═══
                ShaderType.GammaTransfer => BuildGammaTransfer(p, source),
                ShaderType.Vignette => new VignetteEffect { Amount = Clamp(p.Value, 0, 1), Color = ColorFromVector(p.Color1), Source = source },
                ShaderType.ColorMatrix => new ColorMatrixEffect { Source = source },
                ShaderType.ColorMatch => new ColorMatrixEffect { ColorMatrix = ColorMatchMatrix(p.Color1, p.Color2), Source = source },

                // ═══ Effect1 ═══
                ShaderType.GaussianBlur => new GaussianBlurEffect { BlurAmount = p.Value, Source = source },
                ShaderType.DirectionalBlur => new DirectionalBlurEffect { BlurAmount = p.Value, Angle = p.Value2 * MathF.PI / 180f, Source = source },
                ShaderType.Sharpen => new SharpenEffect { Amount = Clamp(p.Value, 0, 10), Source = source },
                ShaderType.Shadow => BuildShadow(p, source),
                ShaderType.EdgeDetection => new EdgeDetectionEffect { Amount = Clamp(p.Value, 0, 1), BlurAmount = Clamp(p.Value2, 0, 10), OverlayEdges = p.Flag, Source = source },
                ShaderType.Morphology => new MorphologyEffect { Mode = p.Mode == 0 ? MorphologyEffectMode.Dilate : MorphologyEffectMode.Erode, Width = (int)p.Value, Height = (int)p.Value2, Source = source },
                ShaderType.Emboss => new EmbossEffect { Amount = Clamp(p.Value, 0, 10), Angle = p.Value2 * MathF.PI / 180f, Source = source },
                ShaderType.Straighten => new StraightenEffect { Angle = p.Value * MathF.PI / 180f, MaintainSize = true, Source = source },

                // ═══ Effect2 ═══
                ShaderType.Sepia => new SepiaEffect { Source = source },
                ShaderType.Posterize => new PosterizeEffect { RedValueCount = (int)p.Value, GreenValueCount = (int)p.Value2, BlueValueCount = (int)p.Value3, Source = source },
                ShaderType.LuminanceToAlpha => BuildLuminanceToAlpha(p, source),
                ShaderType.ChromaKey => new ChromaKeyEffect { Color = ColorFromVector(p.Color1), Tolerance = Clamp(p.Value, 0, 1), InvertAlpha = p.Flag, Feather = p.Mode == 1, Source = source },
                ShaderType.Border => BuildBorder(p, source),
                ShaderType.Colouring => new HueRotationEffect { Angle = p.Value * MathF.PI / 180f, Source = new SepiaEffect { Source = source } },
                ShaderType.Tint => new TintEffect { ColorHdr = p.Color1, Source = source },
                ShaderType.DiscreteTransfer => BuildDiscreteTransfer(p, source),

                // ═══ Effect3 ═══
                ShaderType.Lighting => BuildLighting(p, source),
                ShaderType.Fog => BuildFog(p, source),
                ShaderType.Glass => BuildGlass(p, source),

                // ═══ Other ═══
                ShaderType.HSB => BuildHSB(p, source),

                // ═══ Custom Shaders ═══
                ShaderType.ThresholdEffect => BuildThresholdShader(p, source),
                ShaderType.GradientMappingEffect => BuildGradientMappingShader(p, source),
                ShaderType.RippleEffect => BuildRippleShader(p, source),
                ShaderType.DisplacementLiquefactionEffect => BuildDisplacementLiquefactionShader(p, source),

                _ => source,
            };
        }

        private static float Clamp(float v, float min, float max) => Math.Clamp(v / 100f, min, max);

        private static Windows.UI.Color ColorFromVector(Vector4 v) =>
            Windows.UI.Color.FromArgb((byte)(v.W * 255), (byte)(v.X * 255), (byte)(v.Y * 255), (byte)(v.Z * 255));

        private static Matrix5x4 ColorMatchMatrix(Vector4 src, Vector4 dst) => new() {
            M11 = src.X,
            M22 = src.Y,
            M33 = src.Z,
            M44 = src.W,
            M51 = dst.X,
            M52 = dst.Y,
            M53 = dst.Z,
            M54 = dst.W,
        };

        private static ICanvasImage BuildGammaTransfer(EffectParams p, ICanvasImage source) {
            var gt = new GammaTransferEffect { Source = source };
            gt.AlphaAmplitude = p.Value; gt.AlphaExponent = p.Value2; gt.AlphaOffset = p.Value3;
            gt.RedAmplitude = p.Value; gt.RedExponent = p.Value2; gt.RedOffset = p.Value3;
            gt.GreenAmplitude = p.Value; gt.GreenExponent = p.Value2; gt.GreenOffset = p.Value3;
            gt.BlueAmplitude = p.Value; gt.BlueExponent = p.Value2; gt.BlueOffset = p.Value3;
            gt.AlphaDisable = false;
            return gt;
        }

        private static ICanvasImage BuildShadow(EffectParams p, ICanvasImage source) {
            var shadow = new ShadowEffect { BlurAmount = p.Value, Source = source };
            var transformed = new Transform2DEffect { Source = shadow, TransformMatrix = Matrix3x2.CreateTranslation(p.Value2, p.Value3) };
            var opacity = new OpacityEffect { Source = transformed, Opacity = Clamp(p.Value4, 0, 1) };
            return new CompositeEffect { Sources = { source, opacity }, Mode = CanvasComposite.SourceOver };
        }

        private static ICanvasImage BuildLuminanceToAlpha(EffectParams p, ICanvasImage source) {
            var l2a = new LuminanceToAlphaEffect { Source = source };
            return p.Mode switch {
                1 => new BlendEffect { Foreground = l2a, Background = new InvertEffect { Source = source }, Mode = BlendEffectMode.Multiply },
                2 => new BlendEffect { Foreground = new InvertEffect { Source = l2a }, Background = new InvertEffect { Source = source }, Mode = BlendEffectMode.Multiply },
                _ => new BlendEffect { Foreground = l2a, Background = source, Mode = BlendEffectMode.Multiply },
            };
        }

        private static ICanvasImage BuildBorder(EffectParams p, ICanvasImage source) {
            var cropRect = new Windows.Foundation.Rect(p.Point1.X, p.Point1.Y, p.Point2.X, p.Point2.Y);
            var border = new BorderEffect {
                ExtendX = (CanvasEdgeBehavior)p.Mode,
                ExtendY = (CanvasEdgeBehavior)p.Mode,
                Source = new CropEffect { SourceRectangle = cropRect, Source = source },
            };
            return new CropEffect { SourceRectangle = cropRect, Source = border };
        }

        private static ICanvasImage BuildDiscreteTransfer(EffectParams p, ICanvasImage source) {
            return new DiscreteTransferEffect {
                Source = source,
                AlphaTable = p.AlphaTable ?? [0, 1],
                RedTable = p.RedTable ?? [0, 1],
                GreenTable = p.GreenTable ?? [0, 1],
                BlueTable = p.BlueTable ?? [0, 1],
            };
        }

        private static ICanvasImage BuildLighting(EffectParams p, ICanvasImage source) {
            float ambient = Clamp(p.Value3, 0, 1);
            Vector3 lightPos = new(p.Point1, p.Value);
            Vector3 lightTarget = new(p.Point2, 0);
            var heightMap = new LuminanceToAlphaEffect { Source = source };
            var diffuse = new SpotDiffuseEffect {
                Source = heightMap,
                LightPosition = lightPos,
                LightTarget = lightTarget,
                Focus = 100,
                LimitingConeAngle = p.Value2 * MathF.PI / 180f,
                DiffuseAmount = 1 - ambient,
            };
            var specular = new SpotSpecularEffect {
                Source = heightMap,
                LightPosition = lightPos,
                LightTarget = lightTarget,
                Focus = 100,
                LimitingConeAngle = p.Value2 * MathF.PI / 180f,
                SpecularAmount = ambient,
                SpecularExponent = 16,
            };
            return new ArithmeticCompositeEffect {
                Source1 = diffuse,
                Source2 = specular,
                MultiplyAmount = 0,
                Source1Amount = 1,
                Source2Amount = 1,
                Offset = ambient - 1,
            };
        }

        private static ICanvasImage BuildFog(EffectParams p, ICanvasImage source) {
            float fog = Clamp(p.Value, 0, 1);
            var turbulence = new TurbulenceEffect {
                Frequency = new Vector2(0.01f * (1 << p.Mode)),
                Octaves = 4,
                Seed = 123,
            };
            var scaledTurb = new ScaleEffect { Source = turbulence, Scale = new Vector2(1 << p.Mode) };
            return new ArithmeticCompositeEffect {
                Source1 = source,
                Source2 = scaledTurb,
                MultiplyAmount = fog,
                Source1Amount = 1 - fog,
                Source2Amount = 0,
            };
        }

        private static ICanvasImage BuildGlass(EffectParams p, ICanvasImage source) {
            var turbulence = new TurbulenceEffect {
                Frequency = new Vector2(0.01f * (1 << p.Mode)),
                Octaves = 4,
                Seed = 123,
            };
            var scaledTurb = new ScaleEffect { Source = turbulence, Scale = new Vector2(1 << p.Mode) };
            return new DisplacementMapEffect {
                Source = source,
                Displacement = scaledTurb,
                Amount = Clamp(p.Value, 0, 100),
                XChannelSelect = EffectChannelSelect.Red,
                YChannelSelect = EffectChannelSelect.Green,
            };
        }

        private static ICanvasImage BuildHSB(EffectParams p, ICanvasImage source) {
            return new ColorMatrixEffect { Source = source, ColorMatrix = Matrix5x4Extension.HSB(p.Value, p.Value2, p.Value3) };
        }

        // Custom Pixel Shader builders
        private static ICanvasImage BuildThresholdShader(EffectParams p, ICanvasImage source) {
            var bytes = ShaderLoader.GetShader(ShaderType.ThresholdEffect);
            return new PixelShaderEffect(bytes) {
                Source1 = source,
                Properties = { ["threshold"] = p.Value, ["color0"] = p.Color1, ["color1"] = p.Color2 },
            };
        }

        private static ICanvasImage BuildGradientMappingShader(EffectParams p, ICanvasImage source) {
            var bytes = ShaderLoader.GetShader(ShaderType.GradientMappingEffect);
            return new PixelShaderEffect(bytes) { Source1 = source, Source2 = source, MaxSamplerOffset = 1 };
        }

        private static ICanvasImage BuildRippleShader(EffectParams p, ICanvasImage source) {
            var bytes = ShaderLoader.GetShader(ShaderType.RippleEffect);
            return new PixelShaderEffect(bytes) {
                Source1 = source,
                Source1BorderMode = EffectBorderMode.Soft,
                MaxSamplerOffset = 500,
                Properties = {
                    ["frequency"] = p.Value, ["phase"] = p.Value2, ["amplitude"] = p.Value3,
                    ["spread"] = p.Value4, ["center"] = p.Point1, ["dpi"] = p.Dpi,
                },
            };
        }

        private static ICanvasImage BuildDisplacementLiquefactionShader(EffectParams p, ICanvasImage source) {
            var bytes = ShaderLoader.GetShader(ShaderType.DisplacementLiquefactionEffect);
            return new PixelShaderEffect(bytes) {
                Source1 = source,
                Source1BorderMode = EffectBorderMode.Soft,
                Properties = {
                    ["mode"] = p.Mode, ["amount"] = p.Amount, ["pressure"] = p.Value,
                    ["radius"] = p.Value2, ["position"] = p.Point1, ["targetPosition"] = p.Point2,
                },
            };
        }
    }
}
