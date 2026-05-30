using System.Numerics;

namespace VirtualPaper.Shader {
    /// <summary>效果参数载体，覆盖所有 ShaderType 需要的参数。</summary>
    public struct EffectParams {
        // Slider
        public float Value;
        public float Value2;
        public float Value3;
        public float Value4;

        // Color
        public Vector4 Color1;
        public Vector4 Color2;

        // Mode / Flag
        public int Mode;
        public bool Flag;

        // Curve tables (DiscreteTransfer / GammaTransfer)
        public float[]? RedTable;
        public float[]? GreenTable;
        public float[]? BlueTable;
        public float[]? AlphaTable;

        // Interactive points (Lighting, Ripple, DisplacementLiquefaction)
        public Vector2 Point1;
        public Vector2 Point2;

        // Misc
        public float Dpi;
        public float Amount;

        public static readonly EffectParams Default = new() { Value = 0.5f, Value2 = 0.5f, Value3 = 0.5f, Value4 = 0.5f, Dpi = 96f };
    }
}
