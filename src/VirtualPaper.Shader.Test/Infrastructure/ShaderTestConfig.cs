namespace VirtualPaper.Shader.Test.Infrastructure {
    /// <summary>
    /// 统一管理测试资源路径与环境变量
    /// </summary>
    internal static class ShaderTestConfig {
        public static string FxcPath =>
            Path.Combine(ShaderProjDir, "Tools", "fxc", "fxc.exe");
        public static bool IsFxcAvailable() =>
            File.Exists(FxcPath);

        public static string ShaderIncludeDir =>
            Path.Combine(ShaderProjDir, "Tools", "include");
        
        public static string ShaderSourceDir =>
            Path.Combine(ShaderProjDir, "Shaders");

        /// <summary>
        /// HLSL 源文件目录（可通过环境变量 SHADER_SOURCE_DIR 覆盖）
        /// </summary>
        public static string ShaderProjDir {
            get {
                // 从 bin/Debug/net8.0-windows.../  向上找项目根
                var baseDir = AppContext.BaseDirectory;
                var projectRoot = Path.GetFullPath(
                    Path.Combine(baseDir, @"..\..\..\..\"));
                return Path.Combine(projectRoot, "VirtualPaper.Shader");
            }
        }

        /// <summary>
        /// 编译输出临时目录
        /// </summary>
        public static string CompileOutputDir =>
            Path.Combine(Path.GetTempPath(), "VirtualPaper_ShaderTest");

        /// <summary>
        /// 判断 ShaderSourceDir 是否存在
        /// </summary>
        public static bool IsShaderSourceDirAvailable() =>
            Directory.Exists(ShaderSourceDir);
    }
}
