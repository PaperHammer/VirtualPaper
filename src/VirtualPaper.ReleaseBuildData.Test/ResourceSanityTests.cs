namespace VirtualPaper.ReleaseBuildData.Test {

    /// <summary>
    /// 语言资源与 PlayerWeb 内嵌静态资源完整性检测。
    /// </summary>
    [TestClass]
    public class ResourceSanityTests {

        private static string PluginsDir => ReleaseBuildSanityTests.PluginsDir;

        [TestInitialize]
        public void SkipIfReleaseDirMissing() {
            if (!Directory.Exists(ReleaseBuildSanityTests.ReleaseDir))
                Assert.Inconclusive(
                    $"Release build directory not found: {ReleaseBuildSanityTests.ReleaseDir}\n" +
                    "Run a Release build first, or set the RELEASE_BIN_DIR environment variable.");
        }

        // ── UI Plugin 语言目录 ────────────────────────────────────────────────
        // VirtualPaper.UI 是主语言化入口，语言目录在 Plugins/UI/ 下
        // 检查最核心的两个 locale，全量语言由 InnoSetup smoke test 负责

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        [DataRow("zh-CN")]
        [DataRow("en-us")]
        [DataRow("ja-JP")]
        [DataRow("ko-KR")]
        [DataRow("de-DE")]
        [DataRow("fr-FR")]
        public void Plugin_UI_LocaleDir_Exists(string locale) {
            ReleaseBuildSanityTests.AssertDirExists(
                Path.Combine(PluginsDir, "UI", locale),
                $"UI locale dir [{locale}]");
        }

        // ── UI Plugin 资源文件 ────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void Plugin_UI_ResourcesPri_Exists() {
            // resources.pri 是 WinUI3/WinAppSDK 运行必需的资源索引
            ReleaseBuildSanityTests.AssertFileExists(
                Path.Combine(PluginsDir, "UI", "resources.pri"),
                "WinUI3 resources.pri");
        }

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void Plugin_UI_DraftPanelConfigs_Exists() {
            ReleaseBuildSanityTests.AssertDirExists(
                Path.Combine(PluginsDir, "UI", "DraftPanelConfigs"),
                "DraftPanelConfigs directory");
        }

        // ── PlayerWeb 内嵌 Web 资源 ───────────────────────────────────────────

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void Plugin_PlayerWeb_WebDir_Exists() {
            // PlayerWeb 需要 PLAYER_Web 目录内的 HTML/JS 资源
            ReleaseBuildSanityTests.AssertDirExists(
                Path.Combine(PluginsDir, "UI", "PLAYER_Web"),
                "PLAYER_Web directory");
        }

        // ── Shader 资源 ───────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void Plugin_UI_ShadersDir_Exists() {
            ReleaseBuildSanityTests.AssertDirExists(
                Path.Combine(PluginsDir, "UI", "Shaders"),
                "Shaders directory");
        }
    }
}
