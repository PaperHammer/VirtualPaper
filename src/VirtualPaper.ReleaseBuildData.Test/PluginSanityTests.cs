namespace VirtualPaper.ReleaseBuildData.Test {

    /// <summary>
    /// Plugin 子目录完整性检测。
    /// 验证各 Plugin 的主 exe / dll 以及关键资源文件存在且非空。
    ///
    /// Plugin 目录约定（来自 VirtualPaper.csproj CopyPluginsToOutput target）：
    ///   Plugins/UI/         ← VirtualPaper.UI（WinUI3 主界面）
    ///   Plugins/PlayerWeb/  ← VirtualPaper.PlayerWeb（网页壁纸播放器）
    ///   Plugins/ScrSaver/   ← VirtualPaper.ScreenSaver（屏保模块）
    /// </summary>
    [TestClass]
    public class PluginSanityTests {

        private static string PluginsDir => ReleaseBuildSanityTests.PluginsDir;

        [TestInitialize]
        public void SkipIfReleaseDirMissing() {
            if (!Directory.Exists(ReleaseBuildSanityTests.ReleaseDir))
                Assert.Inconclusive(
                    $"Release build directory not found: {ReleaseBuildSanityTests.ReleaseDir}\n" +
                    "Run a Release build first, or set the RELEASE_BIN_DIR environment variable.");
        }

        // ── UI Plugin（VirtualPaper.UI，WinUI3）──────────────────────────────

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void Plugin_UI_Dir_Exists() {
            ReleaseBuildSanityTests.AssertDirExists(
                Path.Combine(PluginsDir, "UI"), "Plugins/UI");
        }

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void Plugin_UI_Exe_Exists() {
            ReleaseBuildSanityTests.AssertFileExists(
                Path.Combine(PluginsDir, "UI", "VirtualPaper.UI.exe"), "UI plugin exe");
        }

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void Plugin_UI_NlogConfig_Exists() {
            // UI plugin 有自己的 Nlog.config
            ReleaseBuildSanityTests.AssertFileExists(
                Path.Combine(PluginsDir, "UI", "Nlog.config"), "UI plugin Nlog.config");
        }

        // ── UI Plugin 内嵌 Panel dll ─────────────────────────────────────────

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        [DataRow("VirtualPaper.AppSettingsPanel.dll")]
        [DataRow("VirtualPaper.WpSettingsPanel.dll")]
        [DataRow("VirtualPaper.DraftPanel.dll")]
        [DataRow("VirtualPaper.IntelligentPanel.dll")]
        public void Plugin_UI_PanelDll_Exists(string dllName) {
            ReleaseBuildSanityTests.AssertFileExists(
                Path.Combine(PluginsDir, "UI", dllName), $"Panel dll [{dllName}]");
        }

        // ── UI Plugin 内嵌 ML dll（推理模块作为 dll 加载，无独立 exe）──────────

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void Plugin_UI_MlDll_Exists() {
            ReleaseBuildSanityTests.AssertFileExists(
                Path.Combine(PluginsDir, "UI", "VirtualPaper.ML.dll"), "ML dll");
        }

        // ── UI Plugin StaticImg 子目录 ────────────────────────────────────────

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void Plugin_UI_StaticImgDir_Exists() {
            ReleaseBuildSanityTests.AssertDirExists(
                Path.Combine(PluginsDir, "UI", "StaticImg"), "Plugins/UI/StaticImg");
        }

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void Plugin_UI_StaticImgDll_Exists() {
            ReleaseBuildSanityTests.AssertFileExists(
                Path.Combine(PluginsDir, "UI", "StaticImg.dll"), "StaticImg dll");
        }

        // ── PlayerWeb Plugin ─────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void Plugin_PlayerWeb_Dir_Exists() {
            ReleaseBuildSanityTests.AssertDirExists(
                Path.Combine(PluginsDir, "PlayerWeb"), "Plugins/PlayerWeb");
        }

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void Plugin_PlayerWeb_Exe_Exists() {
            ReleaseBuildSanityTests.AssertFileExists(
                Path.Combine(PluginsDir, "PlayerWeb", "VirtualPaper.PlayerWeb.exe"),
                "PlayerWeb plugin exe");
        }

        // ── ScreenSaver Plugin ────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void Plugin_ScrSaver_Dir_Exists() {
            ReleaseBuildSanityTests.AssertDirExists(
                Path.Combine(PluginsDir, "ScrSaver"), "Plugins/ScrSaver");
        }

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void Plugin_ScrSaver_Exe_Exists() {
            ReleaseBuildSanityTests.AssertFileExists(
                Path.Combine(PluginsDir, "ScrSaver", "VirtualPaper.ScreenSaver.exe"),
                "ScreenSaver plugin exe");
        }

        // ── ML 模型目录（ai_models 由 VirtualPaper.ML.csproj CopyToVirtualPaper 复制）

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void Plugin_ML_StyleTransfer_ModelsDir_Exists() {
            ReleaseBuildSanityTests.AssertDirExists(
                Path.Combine(PluginsDir, "ML", "StyleTransfer", "ai_models"),
                "ML/StyleTransfer/ai_models");
        }

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void Plugin_ML_SuperResolution_ModelsDir_Exists() {
            ReleaseBuildSanityTests.AssertDirExists(
                Path.Combine(PluginsDir, "ML", "SuperResolution", "ai_models"),
                "ML/SuperResolution/ai_models");
        }

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void Plugin_ML_DepthEstimate_ModelsDir_Exists() {
            ReleaseBuildSanityTests.AssertDirExists(
                Path.Combine(PluginsDir, "ML", "DepthEstimate", "ai_models"),
                "ML/DepthEstimate/ai_models");
        }
    }
}
