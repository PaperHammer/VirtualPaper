namespace VirtualPaper.Shader {
    public static class ShaderTypeManager {
        /// <summary>
        /// 获取 Shader 文件名（仅自定义像素着色器有值，内置 Win2D 效果返回空）
        /// </summary>
        public static string GetShaderName(ShaderType type) {
            return type switch {
                // ── 自定义像素着色器（需要 .bin 文件） ──
                ShaderType.ThresholdEffect => "ThresholdEffect.bin",
                ShaderType.GradientMappingEffect => "GradientMappingEffect.bin",
                ShaderType.RippleEffect => "RippleEffect.bin",
                ShaderType.DisplacementLiquefactionEffect => "DisplacementLiquefactionEffect.bin",
                ShaderType.GeometryAlphaEraseEffect => "GeometryAlphaEraseEffect.bin",

                // ── 内置 Win2D 效果（无 .bin 文件） ──
                ShaderType.None => string.Empty,
                _ => string.Empty,
            };
        }

        /// <summary>
        /// 该类型是否需要加载自定义 .bin 着色器文件
        /// </summary>
        public static bool IsCustomShader(ShaderType type) {
            return !string.IsNullOrEmpty(GetShaderName(type));
        }
    }

    /// <summary>
    /// 效果类型枚举
    /// <para>
    /// 内置 Win2D 效果（无 .bin 文件）直接在 C# 中实例化对应的
    /// <see cref="Microsoft.Graphics.Canvas.Effects"/> 类；
    /// 自定义像素着色器（有 .bin 文件）通过
    /// <see cref="ShaderLoader.GetShader"/> 加载后用作 <c>PixelShaderEffect</c>。
    /// </para>
    /// </summary>
    public enum ShaderType {
        None = 0,

        // 基本色彩调整
        /// <summary>灰度 — <see cref="GrayscaleEffect"/></summary>
        Grayscale,
        /// <summary>反相 — <see cref="InvertEffect"/></summary>
        Invert,
        /// <summary>曝光 — <see cref="ExposureEffect"/></summary>
        Exposure,
        /// <summary>亮度 — <see cref="BrightnessEffect"/></summary>
        Brightness,
        /// <summary>饱和度 — <see cref="SaturationEffect"/></summary>
        Saturation,
        /// <summary>色相旋转 — <see cref="HueRotationEffect"/></summary>
        HueRotation,
        /// <summary>对比度 — <see cref="ContrastEffect"/></summary>
        Contrast,
        /// <summary>色温与色调 — <see cref="TemperatureAndTintEffect"/></summary>
        TemperatureAndTint,
        /// <summary>高光与阴影 — <see cref="HighlightsAndShadowsEffect"/></summary>
        HighlightsAndShadows,

        // 高级色彩调整
        /// <summary>伽马传递曲线 — <see cref="GammaTransferEffect"/></summary>
        GammaTransfer,
        /// <summary>暗角 — <see cref="VignetteEffect"/></summary>
        Vignette,
        /// <summary>色彩矩阵 — <see cref="ColorMatrixEffect"/></summary>
        ColorMatrix,
        /// <summary>色彩匹配 — <see cref="ColorMatrixEffect"/> + ColorMatch 矩阵</summary>
        ColorMatch,

        // 滤镜效果
        /// <summary>高斯模糊 — <see cref="GaussianBlurEffect"/></summary>
        GaussianBlur,
        /// <summary>方向模糊 — <see cref="DirectionalBlurEffect"/></summary>
        DirectionalBlur,
        /// <summary>锐化 — <see cref="SharpenEffect"/></summary>
        Sharpen,
        /// <summary>阴影 — 组合 <see cref="ShadowEffect"/> + <see cref="Transform2DEffect"/> + <see cref="OpacityEffect"/> + <see cref="CompositeEffect"/></summary>
        Shadow,
        /// <summary>边缘检测 — <see cref="EdgeDetectionEffect"/></summary>
        EdgeDetection,
        /// <summary>形态学 — <see cref="MorphologyEffect"/></summary>
        Morphology,
        /// <summary>浮雕 — <see cref="EmbossEffect"/></summary>
        Emboss,
        /// <summary>拉直 — <see cref="StraightenEffect"/></summary>
        Straighten,

        // 艺术效果
        /// <summary>复古棕褐色 — <see cref="SepiaEffect"/></summary>
        Sepia,
        /// <summary>色调分离 — <see cref="PosterizeEffect"/></summary>
        Posterize,
        /// <summary>亮度转透明度 — <see cref="LuminanceToAlphaEffect"/></summary>
        LuminanceToAlpha,
        /// <summary>色度抠图 — <see cref="ChromaKeyEffect"/></summary>
        ChromaKey,
        /// <summary>图像边框 — <see cref="CropEffect"/> + <see cref="BorderEffect"/></summary>
        Border,
        /// <summary>着色 — <see cref="HueRotationEffect"/> × <see cref="SepiaEffect"/></summary>
        Colouring,
        /// <summary>色调 — <see cref="TintEffect"/></summary>
        Tint,
        /// <summary>离散传递曲线 — <see cref="DiscreteTransferEffect"/></summary>
        DiscreteTransfer,
        /// <summary>油画 — <see cref="PaintTransitionsEffect"/> 或自定义实现</summary>
        OilPaint,
        /// <summary>素描 — <see cref="EdgeDetectionEffect"/> + <see cref="GrayscaleEffect"/></summary>
        Sketch,
        /// <summary>水彩 — 模糊 + 饱和度调整</summary>
        WaterColor,
        /// <summary>点画 — 噪声 + .posterize</summary>
        Pointillism,
        /// <summary>交叉阴影线 — 自定义实现</summary>
        Crosshatch,
        /// <summary>卡通 — 边缘检测 + 颜色量化</summary>
        Cartoon,

        // 特效
        /// <summary>噪声 — <see cref="TurbulenceEffect"/></summary>
        Noise,
        /// <summary> blooms — 模糊 + 亮度阈值</summary>
        Bloom,
        /// <summary>色差 — 通道偏移</summary>
        Chromatic,

        // 混合模式
        /// <summary>正片叠底 — <see cref="BlendEffect"/> Multiply</summary>
        BlendMultiply,
        /// <summary>滤色 — <see cref="BlendEffect"/> Screen</summary>
        BlendScreen,
        /// <summary>叠加 — <see cref="BlendEffect"/> Overlay</summary>
        BlendOverlay,
        /// <summary>柔光 — <see cref="BlendEffect"/> SoftLight</summary>
        BlendSoftLight,

        // 高级复合效果
        /// <summary>光照 — <see cref="SpotDiffuseEffect"/> + <see cref="SpotSpecularEffect"/> + <see cref="ArithmeticCompositeEffect"/></summary>
        Lighting,
        /// <summary>雾气 — <see cref="ArithmeticCompositeEffect"/> + 湍流噪声</summary>
        Fog,
        /// <summary>玻璃 — <see cref="DisplacementMapEffect"/> + 湍流噪声</summary>
        Glass,

        // Other — 变换与自定义着色器
        /// <summary>HSB 调整 — <see cref="ColorMatrixEffect"/> + HSB 矩阵</summary>
        HSB,

        // 自定义像素着色器（需要 .bin）
        /// <summary>阈值 — 自定义 PixelShader</summary>
        ThresholdEffect,
        /// <summary>渐变映射 — 自定义 PixelShader</summary>
        GradientMappingEffect,
        /// <summary>涟漪 — 自定义 PixelShader</summary>
        RippleEffect,
        /// <summary>液化 — 自定义 PixelShader</summary>
        DisplacementLiquefactionEffect,
        /// <summary>几何体 Alpha 擦除 — 自定义 PixelShader</summary>
        GeometryAlphaEraseEffect,
    }
}
