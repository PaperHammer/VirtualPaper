namespace VirtualPaper.Shader {
    public static class ShaderTypeManager {
        public static string GetShaderName(ShaderType type) {
            return type switch {
                ShaderType.BrushEdgeHardness => "BrushEdgeHardness.bin",
                _ => string.Empty,
            };
        }
    }

    public enum ShaderType {
        None,
        BrushEdgeHardness,
    }
}
