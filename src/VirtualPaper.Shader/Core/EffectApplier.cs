using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using System.Numerics;
using VirtualPaper.Shader.Models;

namespace VirtualPaper.Shader.Core {
    /// <summary>
    /// Core engine: apply <see cref="ShaderType"/> + <see cref="EffectParams"/> to an <see cref="ICanvasImage"/>.
    /// </summary>
    public static class EffectApplier {
        public static ICanvasImage Apply(ShaderType type, EffectParams p, ICanvasImage source) {
            return type switch {
                // Adjustment
                ShaderType.Grayscale => new GrayscaleEffect { Source = source },
                ShaderType.Invert => new InvertEffect { Source = source },
                ShaderType.Exposure => new ExposureEffect { Exposure = p.Value / 100f, Source = source },
                ShaderType.Brightness => BuildBrightness(p, source),
                ShaderType.Saturation => new SaturationEffect { Saturation = p.Value / 100f, Source = source },
                ShaderType.HueRotation => new HueRotationEffect { Angle = p.Value * MathF.PI / 180f, Source = source },
                ShaderType.Contrast => new ContrastEffect { Contrast = Clamp(p.Value, -1, 1), Source = source },
                ShaderType.TemperatureAndTint => new TemperatureAndTintEffect { Temperature = Clamp(p.Value, -1, 1), Tint = Clamp(p.Value2, -1, 1), Source = source },
                ShaderType.HighlightsAndShadows => new HighlightsAndShadowsEffect { Shadows = Clamp(p.Value, -1, 1), Highlights = Clamp(p.Value2, -1, 1), Clarity = Clamp(p.Value3, -1, 1), MaskBlurAmount = Clamp(p.Value4, 0, 10), Source = source },

                // Adjustment2
                ShaderType.GammaTransfer => BuildGammaTransfer(p, source),
                ShaderType.Vignette => new VignetteEffect { Amount = Clamp(p.Value, 0, 1), Color = ColorFromVector(p.Color1), Source = source },

                // Effect1
                ShaderType.GaussianBlur => new GaussianBlurEffect { BlurAmount = p.Value / 10f, Source = source },
                ShaderType.DirectionalBlur => new DirectionalBlurEffect { BlurAmount = p.Value / 10f, Angle = p.Value2 * MathF.PI / 180f, Source = source },
                ShaderType.Sharpen => new SharpenEffect { Amount = p.Value / 10f, Source = source },
                ShaderType.Shadow => BuildShadow(p, source),
                ShaderType.Glow => BuildGlow(p, source),
                ShaderType.EdgeDetection => new EdgeDetectionEffect { Amount = Clamp(p.Value, 0, 1), BlurAmount = Clamp(p.Value2, 0, 10), OverlayEdges = p.Flag, Source = source },
                ShaderType.Morphology => new MorphologyEffect { Mode = p.Mode == 0 ? MorphologyEffectMode.Dilate : MorphologyEffectMode.Erode, Width = (int)p.Value, Height = (int)p.Value2, Source = source },
                ShaderType.Emboss => new EmbossEffect { Amount = Clamp(p.Value, 0, 10), Angle = p.Value2 * MathF.PI / 180f, Source = source },
                ShaderType.Straighten => new StraightenEffect { Angle = p.Value * MathF.PI / 180f, MaintainSize = true, Source = source },

                // Effect2
                ShaderType.Sepia => new SepiaEffect { Source = source },
                ShaderType.Posterize => new PosterizeEffect { RedValueCount = (int)p.Value, GreenValueCount = (int)p.Value2, BlueValueCount = (int)p.Value3, Source = source },
                ShaderType.LuminanceToAlpha => BuildLuminanceToAlpha(p, source),
                ShaderType.ChromaKey => new ChromaKeyEffect { Color = ColorFromVector(p.Color1), Tolerance = Clamp(p.Value, 0, 1), InvertAlpha = p.Flag, Feather = p.Mode == 1, Source = source },
                ShaderType.Colouring => new HueRotationEffect { Angle = p.Value * MathF.PI / 180f, Source = new SepiaEffect { Source = source } },

                // Effect3
                ShaderType.Fog => BuildFog(p, source),
                ShaderType.Glass => BuildGlass(p, source),
                ShaderType.Noise => BuildNoise(p, source),
                ShaderType.Bloom => new ArithmeticCompositeEffect { Source1 = source, Source2 = new GaussianBlurEffect { BlurAmount = p.Value / 5f, Source = new ExposureEffect { Exposure = 0.5f, Source = source } }, MultiplyAmount = 0, Source1Amount = 1, Source2Amount = p.Value / 100f },

                // Blend modes
                ShaderType.BlendMultiply => BuildBlend(p, source, BlendEffectMode.Multiply),
                ShaderType.BlendScreen => BuildBlend(p, source, BlendEffectMode.Screen),
                ShaderType.BlendOverlay => BuildBlend(p, source, BlendEffectMode.Overlay),
                ShaderType.BlendSoftLight => BuildBlend(p, source, BlendEffectMode.SoftLight),

                // Other
                ShaderType.HSB => BuildHSB(p, source),

                // Custom Shaders
                ShaderType.ThresholdEffect => BuildThresholdShader(p, source),
                ShaderType.RippleEffect => BuildRippleShader(p, source),
                ShaderType.DisplacementLiquefactionEffect => BuildDisplacementLiquefactionShader(p, source),

                _ => source,
            };
        }

        private static float Clamp(float v, float min, float max) => Math.Clamp(v / 100f, min, max);

        private static Windows.UI.Color ColorFromVector(Vector4 v) =>
            Windows.UI.Color.FromArgb((byte)(v.W * 255), (byte)(v.X * 255), (byte)(v.Y * 255), (byte)(v.Z * 255));

        private static ICanvasImage BuildGammaTransfer(EffectParams p, ICanvasImage source) {
            var gt = new GammaTransferEffect { Source = source };
            gt.AlphaAmplitude = p.Value; gt.AlphaExponent = p.Value2; gt.AlphaOffset = p.Value3;
            gt.RedAmplitude = p.Value; gt.RedExponent = p.Value2; gt.RedOffset = p.Value3;
            gt.GreenAmplitude = p.Value; gt.GreenExponent = p.Value2; gt.GreenOffset = p.Value3;
            gt.BlueAmplitude = p.Value; gt.BlueExponent = p.Value2; gt.BlueOffset = p.Value3;
            gt.AlphaDisable = false;
            return gt;
        }

        private static ICanvasImage BuildBrightness(EffectParams p, ICanvasImage source) {
            // 使用 ColorMatrix 实现亮度调整
            // brightness = (blackPoint + whitePoint - 255) / 255
            float brightness = (p.Value + p.Value2 - 255f) / 255f;
            
            return new ColorMatrixEffect {
                Source = source,
                ColorMatrix = new Matrix5x4 {
                    M11 = 1, M22 = 1, M33 = 1, M44 = 1,
                    M51 = brightness, M52 = brightness, M53 = brightness, M54 = 0
                }
            };
        }

        private static ICanvasImage BuildShadow(EffectParams p, ICanvasImage source) {
            var shadow = new ShadowEffect { BlurAmount = p.Value, Source = source };
            var transformed = new Transform2DEffect { Source = shadow, TransformMatrix = Matrix3x2.CreateTranslation(p.Value2, p.Value3) };
            var opacity = new OpacityEffect { Source = transformed, Opacity = Clamp(p.Value4, 0, 1) };
            return new CompositeEffect { Sources = { source, opacity }, Mode = CanvasComposite.SourceOver };
        }

        private static ICanvasImage BuildNoise(EffectParams p, ICanvasImage source) {
            float amount = p.Value;
            
            if (amount == 0) {
                return source; // No effect
            }
            
            if (amount > 0) {
                // Add noise: blend noise on top of the image
                var turbulence = new TurbulenceEffect {
                    Frequency = new Vector2(0.05f),
                    Octaves = 8,
                    Size = new Vector2(1000, 1000)
                };
                var tiledNoise = new BorderEffect {
                    Source = turbulence,
                    ExtendX = CanvasEdgeBehavior.Wrap,
                    ExtendY = CanvasEdgeBehavior.Wrap
                };
                return new ArithmeticCompositeEffect {
                    Source1 = source,
                    Source2 = tiledNoise,
                    MultiplyAmount = 0,
                    Source1Amount = 1,
                    Source2Amount = amount / 100f
                };
            } else {
                // Remove noise: apply blur to reduce noise
                float blurAmount = -amount * 0.5f; // Scale the blur amount
                return new GaussianBlurEffect {
                    Source = source,
                    BlurAmount = blurAmount
                };
            }
        }

        private static ICanvasImage BuildGlow(EffectParams p, ICanvasImage source) {
            // Glow: increase exposure, blur, then screen blend with original
            var brightened = new ExposureEffect { Source = source, Exposure = 0.3f };
            var blurred = new GaussianBlurEffect { BlurAmount = p.Value * 2f, Source = brightened };
            var opacity = new OpacityEffect { Source = blurred, Opacity = Clamp(p.Value4, 0, 1) };
            return new BlendEffect { Foreground = opacity, Background = source, Mode = BlendEffectMode.Screen };
        }

        private static ICanvasImage BuildBlend(EffectParams p, ICanvasImage source, BlendEffectMode mode) {
            // Blend effect: blend image with modified version, controlled by opacity
            // p.Value: 0-100 (opacity)
            float opacity = p.Value / 100f;
            
            // Create modified version based on blend mode
            ICanvasImage modified = mode switch {
                BlendEffectMode.Multiply => new InvertEffect { Source = source },
                BlendEffectMode.Screen => new InvertEffect { Source = new GrayscaleEffect { Source = source } },
                BlendEffectMode.Overlay => new GrayscaleEffect { Source = source },
                BlendEffectMode.SoftLight => new GrayscaleEffect { Source = source },
                _ => source
            };
            
            // Apply opacity to modified version
            var modifiedWithOpacity = new OpacityEffect {
                Source = modified,
                Opacity = opacity
            };
            
            // Apply blend mode
            return new BlendEffect {
                Foreground = modifiedWithOpacity,
                Background = source,
                Mode = mode
            };
        }

        private static ICanvasImage BuildLuminanceToAlpha(EffectParams p, ICanvasImage source) {
            var l2a = new LuminanceToAlphaEffect { Source = source };
            return p.Mode switch {
                1 => new BlendEffect { Foreground = l2a, Background = new InvertEffect { Source = source }, Mode = BlendEffectMode.Multiply },
                2 => new BlendEffect { Foreground = new InvertEffect { Source = l2a }, Background = new InvertEffect { Source = source }, Mode = BlendEffectMode.Multiply },
                _ => new BlendEffect { Foreground = l2a, Background = source, Mode = BlendEffectMode.Multiply },
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

        private static ICanvasImage BuildRippleShader(EffectParams p, ICanvasImage source) {
            var bytes = ShaderLoader.GetShader(ShaderType.RippleEffect);
            return new PixelShaderEffect(bytes) {
                Source1 = source,
                Source1BorderMode = EffectBorderMode.Soft,
                Source1Mapping = SamplerCoordinateMapping.Offset,               
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
