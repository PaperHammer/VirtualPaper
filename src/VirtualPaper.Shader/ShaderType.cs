namespace VirtualPaper.Shader {
    public static class ShaderTypeManager {
        public static string GetShaderName(ShaderType type) {
            return type switch {
                ShaderType.None => string.Empty,
                _ => $"{type}.bin",
            };
        }
    }

    public enum ShaderType {
        None,
        GeometryAlphaEraseEffect,
    }
}
