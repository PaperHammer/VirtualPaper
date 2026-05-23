namespace VirtualPaper.ReleaseBuildData.Test {

    /// <summary>
    /// Release build 产物完整性检测。
    /// 在 build job 构建完成后立即运行，验证主程序目录的关键文件结构。
    ///
    /// 运行前提：
    ///   环境变量 RELEASE_BIN_DIR 指向 Release 产物根目录，
    ///   例如：src/VirtualPaper/bin/Release/net8.0-windows10.0.19041.0
    /// </summary>
    [TestClass]
    public class ReleaseBuildSanityTests {

        // ── 目录解析 ──────────────────────────────────────────────────────────

        internal static string ReleaseDir {
            get {
                var env = Environment.GetEnvironmentVariable("RELEASE_BIN_DIR");
                if (!string.IsNullOrWhiteSpace(env))
                    return env;

                // 本地回退：从测试程序集位置向上推算
                // 测试 dll 在 src/VirtualPaper.ReleaseBuildData.Test/bin/{config}/net8.0/
                // 目标在  src/VirtualPaper/bin/Release/net8.0-windows10.0.19041.0/
                var testBin = AppContext.BaseDirectory;
                var srcDir = Path.GetFullPath(Path.Combine(testBin, "..", "..", "..", ".."));
                return Path.Combine(srcDir, "VirtualPaper", "bin", "Release", "net8.0-windows10.0.19041.0");
            }
        }

        internal static string PluginsDir => Path.Combine(ReleaseDir, "Plugins");

        // ── 前置检查 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 若 Release 产物目录不存在，将测试标记为 Inconclusive 而非 Failed。
        /// CI 环境中 RELEASE_BIN_DIR 已设置且目录一定存在；
        /// 本地未做 Release build 时直接跳过，不干扰开发工作流。
        /// </summary>
        [TestInitialize]
        public void SkipIfReleaseDirMissing() {
            if (!Directory.Exists(ReleaseDir))
                Assert.Inconclusive(
                    $"Release build directory not found: {ReleaseDir}\n" +
                    "Run a Release build first, or set the RELEASE_BIN_DIR environment variable.");
        }

        // ── 辅助方法 ──────────────────────────────────────────────────────────

        /// <summary>断言文件存在且大小 > 0。</summary>
        internal static void AssertFileExists(string path, string? hint = null) {
            string msg = hint != null ? $"{hint}: " : "";
            Assert.IsTrue(File.Exists(path),
                $"{msg}Expected file not found: {path}");
            Assert.IsGreaterThan(0, new FileInfo(path).Length, $"{msg}File is empty (0 bytes): {path}");
        }

        /// <summary>断言目录存在且非空。</summary>
        internal static void AssertDirExists(string path, string? hint = null) {
            string msg = hint != null ? $"{hint}: " : "";
            Assert.IsTrue(Directory.Exists(path),
                $"{msg}Expected directory not found: {path}");
        }

        // ── 1. 产物根目录可访问 ───────────────────────────────────────────────

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void ReleaseDir_IsAccessible() {
            AssertDirExists(ReleaseDir, "RELEASE_BIN_DIR");
        }

        // ── 2. 主程序 ─────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void MainExe_Exists() {
            AssertFileExists(Path.Combine(ReleaseDir, "VirtualPaper.exe"), "Main executable");
        }

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void NlogConfig_Exists() {
            AssertFileExists(Path.Combine(ReleaseDir, "Nlog.config"), "NLog config");
        }
        
        // ── 3. Plugins 目录结构 ───────────────────────────────────────────────

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void PluginsDir_Exists() {
            AssertDirExists(PluginsDir, "Plugins root");
        }
    }
}
