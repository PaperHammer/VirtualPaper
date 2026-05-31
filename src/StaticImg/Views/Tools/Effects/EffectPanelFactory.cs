using VirtualPaper.Shader;
using Workloads.Creation.StaticImg.Utils;

namespace Workloads.Creation.StaticImg.Views.Tools.Effects {
    public static class EffectPanelFactory {
        public static EffectPanelBase Create(ShaderType type) {
            return type switch {
                // Adjust
                ShaderType.Exposure => new ExposureEffectPanel(),
                ShaderType.Brightness => new BrightnessEffectPanel(),
                ShaderType.Saturation => new SaturationEffectPanel(),
                ShaderType.HueRotation => new HueRotationEffectPanel(),
                ShaderType.Contrast => new ContrastEffectPanel(),
                ShaderType.TemperatureAndTint => new TemperatureTintEffectPanel(),
                ShaderType.HighlightsAndShadows => new HighlightsShadowsEffectPanel(),

                // Filter / art
                ShaderType.GaussianBlur => new BlurEffectPanel(),
                ShaderType.DirectionalBlur => new DirectionalBlurEffectPanel(),
                ShaderType.Sharpen => new SharpenEffectPanel(),
                ShaderType.Vignette => new VignetteEffectPanel(),
                ShaderType.Emboss => new EmbossEffectPanel(),
                ShaderType.Posterize => new PosterizeEffectPanel(),
                ShaderType.Shadow => new ShadowEffectPanel(),

                // Custom shader
                ShaderType.ThresholdEffect => new ThresholdEffectPanel(),
                ShaderType.RippleEffect => new RippleEffectPanel(),
                ShaderType.DisplacementLiquefactionEffect => new DisplacementLiquefactionEffectPanel(),

                _ => CreateBySliderConfig(type),
            };
        }

        private static EffectPanelBase CreateBySliderConfig(ShaderType type) {
            var cfg = EffectMap.GetSliderConfig(type);
            if (cfg.HasSlider2) return new DoubleSliderEffectPanel(cfg);
            if (cfg.HasSlider1) return new SingleSliderEffectPanel(cfg);
            return new EmptyEffectPanel();
        }
    }
}
