using VirtualPaper.Shader;

namespace Workloads.Creation.StaticImg.Utils {
    public static class EffectMap {
        public static ShaderType ToShaderType(string effectId) => effectId switch {
            "adjust_grayscale"   => ShaderType.Grayscale,
            "adjust_invert"      => ShaderType.Invert,
            "adjust_exposure"    => ShaderType.Exposure,
            "adjust_brightness"  => ShaderType.Brightness,
            "adjust_saturation"  => ShaderType.Saturation,
            "adjust_hue"         => ShaderType.HueRotation,
            "adjust_contrast"    => ShaderType.Contrast,
            "adjust_temperature" => ShaderType.TemperatureAndTint,
            "adjust_highlights"  => ShaderType.HighlightsAndShadows,

            "color_sepia"   => ShaderType.Sepia,
            "color_duotone" => ShaderType.GradientMappingEffect,
            "color_lut"     => ShaderType.DiscreteTransfer,
            "color_tint"    => ShaderType.Tint,

            "art_emboss" => ShaderType.Emboss,
            "art_pixelate" => ShaderType.Posterize,
            "art_sepia" => ShaderType.Sepia,

            "fx_blur"      => ShaderType.GaussianBlur,
            "fx_sharpen"   => ShaderType.Sharpen,
            "fx_vignette"  => ShaderType.Vignette,
            "fx_glow"      => ShaderType.Shadow,
            "fx_distort"   => ShaderType.RippleEffect,
            "fx_straighten" => ShaderType.Straighten,
            "fx_edge"      => ShaderType.EdgeDetection,
            "fx_morphology" => ShaderType.Morphology,

            "adv_gamma"    => ShaderType.GammaTransfer,
            "adv_hsb"      => ShaderType.HSB,
            "adv_luma_alpha" => ShaderType.LuminanceToAlpha,
            "adv_chroma_key" => ShaderType.ChromaKey,
            "adv_fog"      => ShaderType.Fog,
            "adv_glass"    => ShaderType.Glass,
            "adv_colouring" => ShaderType.Colouring,

            _ => ShaderType.None,
        };

        /// <summary>效果 → 滑块配置（Value/Value2）</summary>
        public static EffectSliderConfig GetSliderConfig(ShaderType type) => type switch {
            // 无参数效果
            ShaderType.Grayscale or ShaderType.Invert or
            ShaderType.Sepia
                => new EffectSliderConfig(),

            // 单滑块 (Value)
            ShaderType.Exposure
                => new EffectSliderConfig { Min = -200, Max = 200, Default = 0, Label = "曝光" },
            ShaderType.Brightness
                => new EffectSliderConfig { Min = -100, Max = 100, Default = 0, Label = "亮度" },
            ShaderType.Saturation
                => new EffectSliderConfig { Min = 0, Max = 200, Default = 100, Label = "饱和度" },
            ShaderType.Contrast
                => new EffectSliderConfig { Min = -100, Max = 100, Default = 0, Label = "对比度" },
            ShaderType.Vignette
                => new EffectSliderConfig { Min = 0, Max = 100, Default = 0, Label = "暗角" },
            ShaderType.GaussianBlur
                => new EffectSliderConfig { Min = 0, Max = 100, Default = 0, Label = "模糊" },
            ShaderType.Sharpen
                => new EffectSliderConfig { Min = 0, Max = 100, Default = 0, Label = "锐化" },
            ShaderType.Tint
                => new EffectSliderConfig { Min = 0, Max = 100, Default = 50, Label = "强度" },

            // 双滑块 (Value + Value2)
            ShaderType.HueRotation
                => new EffectSliderConfig { Min = -180, Max = 180, Default = 0, Label = "色相",
                    Min2 = 0, Max2 = 100, Default2 = 100, Label2 = "强度" },
            ShaderType.TemperatureAndTint
                => new EffectSliderConfig { Min = -100, Max = 100, Default = 0, Label = "色温",
                    Min2 = -100, Max2 = 100, Default2 = 0, Label2 = "色调" },
            ShaderType.Emboss
                => new EffectSliderConfig { Min = 0, Max = 100, Default = 0, Label = "强度",
                    Min2 = 0, Max2 = 360, Default2 = 45, Label2 = "角度" },
            ShaderType.DirectionalBlur
                => new EffectSliderConfig { Min = 0, Max = 100, Default = 0, Label = "模糊",
                    Min2 = 0, Max2 = 360, Default2 = 0, Label2 = "角度" },
            ShaderType.EdgeDetection
                => new EffectSliderConfig { Min = 0, Max = 100, Default = 50, Label = "强度",
                    Min2 = 0, Max2 = 100, Default2 = 0, Label2 = "模糊" },
            ShaderType.Morphology
                => new EffectSliderConfig { Min = 1, Max = 20, Default = 3, Label = "宽度",
                    Min2 = 1, Max2 = 20, Default2 = 3, Label2 = "高度" },
            ShaderType.Posterize
                => new EffectSliderConfig { Min = 2, Max = 256, Default = 4, Label = "色阶",
                    Min2 = 2, Max2 = 256, Default2 = 4, Label2 = "色阶G" },
            ShaderType.Shadow
                => new EffectSliderConfig { Min = 0, Max = 100, Default = 10, Label = "模糊",
                    Min2 = -200, Max2 = 200, Default2 = 5, Label2 = "偏移X" },
            ShaderType.Straighten
                => new EffectSliderConfig { Min = -45, Max = 45, Default = 0, Label = "角度" },
            ShaderType.GammaTransfer
                => new EffectSliderConfig { Min = 0, Max = 100, Default = 100, Label = "振幅",
                    Min2 = 1, Max2 = 500, Default2 = 100, Label2 = "指数" },
            ShaderType.HighlightsAndShadows
                => new EffectSliderConfig { Min = -100, Max = 100, Default = 0, Label = "阴影",
                    Min2 = -100, Max2 = 100, Default2 = 0, Label2 = "高光" },

            // 自定义着色器
            ShaderType.ThresholdEffect
                => new EffectSliderConfig { Min = 0, Max = 300, Default = 150, Label = "阈值" },
            ShaderType.RippleEffect
                => new EffectSliderConfig { Min = 0, Max = 200, Default = 140, Label = "频率",
                    Min2 = -200, Max2 = 200, Default2 = 0, Label2 = "相位" },
            ShaderType.DisplacementLiquefactionEffect
                => new EffectSliderConfig { Min = 1, Max = 100, Default = 20, Label = "半径",
                    Min2 = 1, Max2 = 100, Default2 = 50, Label2 = "压力" },

            // 新增效果
            ShaderType.HSB
                => new EffectSliderConfig { Min = 0, Max = 360, Default = 0, Label = "色相",
                    Min2 = 0, Max2 = 400, Default2 = 100, Label2 = "饱和度" },
            ShaderType.Fog
                => new EffectSliderConfig { Min = 0, Max = 100, Default = 50, Label = "浓度" },
            ShaderType.Glass
                => new EffectSliderConfig { Min = 0, Max = 100, Default = 50, Label = "强度" },
            ShaderType.ChromaKey
                => new EffectSliderConfig { Min = 0, Max = 100, Default = 50, Label = "容差" },

            _ => new EffectSliderConfig { Min = 0, Max = 100, Default = 50, Label = "强度" },
        };
    }

    /// <summary>滑块参数配置</summary>
    public readonly struct EffectSliderConfig {
        // Slider 1
        public float Min { get; init; }
        public float Max { get; init; }
        public float Default { get; init; }
        public string Label { get; init; }

        // Slider 2
        public float Min2 { get; init; }
        public float Max2 { get; init; }
        public float Default2 { get; init; }
        public string Label2 { get; init; }

        public bool HasSlider1 => Max > Min;
        public bool HasSlider2 => Max2 > Min2;
    }
}
