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
        // These are framework satellite directories. App translations are verified separately below.

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
        [DynamicData(nameof(SupportedLocales), DynamicDataSourceType.Method)]
        public void Plugin_UI_LiveTranslations_MatchTheirSourceStrings(string locale) {
            string path = Path.Combine(PluginsDir, "UI", "VirtualPaper.UIComponent", "Strings", locale, "Resources.resw");
            var entries = System.Xml.Linq.XDocument.Load(path).Root!.Elements("data")
                .ToDictionary(element => (string)element.Attribute("name")!, element => element);
            var assembly = typeof(ResourceSanityTests).Assembly;
            string sourceName = assembly.GetManifestResourceNames().Single(name =>
                name.StartsWith("VirtualPaper.Localization.Source.", StringComparison.Ordinal)
                && name.Replace('\\', '/').EndsWith($"{locale}/Resources.resw", StringComparison.Ordinal));
            using var sourceStream = assembly.GetManifestResourceStream(sourceName)!;
            var sources = System.Xml.Linq.XDocument.Load(sourceStream).Root!.Elements("data");
            const string generalTabKey = "AppSettings_SelBarItem1_General";
            Assert.AreEqual((string?)sources.Single(source => (string?)source.Attribute("name") == generalTabKey).Element("value"),
                VirtualPaper.TestSupport.LocalizedTestStrings.Read(Path.Combine(PluginsDir, "UI"), locale, generalTabKey),
                $"Installed acceptance must use the requested language: {locale}");
            foreach (var source in sources) {
                string key = (string)source.Attribute("name")!;
                Assert.IsTrue(entries.TryGetValue(key, out var actual), $"Missing packaged source string: {locale}/{key}");
                Assert.AreEqual((string?)source.Element("value"), (string?)actual!.Element("value"), $"Stale packaged translation: {locale}/{key}");
            }
            // Embed the build's expected mappings in the test assembly; runtime
            // packages need only the generated resw files, not build metadata.
            using var mappingStream = typeof(ResourceSanityTests).Assembly.GetManifestResourceStream("VirtualPaper.XamlResources.xml");
            Assert.IsNotNull(mappingStream, "The test assembly is missing XAML resource mappings.");
            var mappings = System.Xml.Linq.XDocument.Load(mappingStream).Root!.Elements("Resource")
                .ToDictionary(element => (string)element.Attribute("Name")!, element => (string)element.Attribute("Source")!);
            var translatedProperties = entries.Where(pair => pair.Key.StartsWith("Ui_", StringComparison.Ordinal)).ToArray();
            Assert.IsGreaterThan(0, translatedProperties.Length, "Live XAML translations are missing from the package.");
            Assert.HasCount(mappings.Count, translatedProperties, "Generated XAML translations do not match the mapping count.");
            foreach (var (name, source) in mappings) {
                Assert.IsTrue(entries.TryGetValue(name, out var entry), $"Generated XAML translation is missing: {name}");
                Assert.IsTrue(entries.TryGetValue(source, out var original), $"Missing source string for {name}: {source}");
                Assert.AreEqual((string?)original!.Element("value"), (string?)entry!.Element("value"),
                    $"Live and programmatic translations differ: {name}");
            }
        }

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        public void Plugin_UI_EditPanelConfigs_Exists() {
            ReleaseBuildSanityTests.AssertDirExists(
                Path.Combine(PluginsDir, "UI", "EditPanelConfigs"),
                "EditPanelConfigs directory");
        }

        public static IEnumerable<object[]> SupportedLocales() {
            using var stream = typeof(ResourceSanityTests).Assembly.GetManifestResourceStream("VirtualPaper.LanguageCatalog.json")!;
            using var catalog = System.Text.Json.JsonDocument.Parse(stream);
            return catalog.RootElement.GetProperty("Languages").EnumerateArray()
                .Select(language => new object[] { language.GetProperty("Code").GetString()! }).ToArray();
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
                Path.Combine(PluginsDir, "Shaders"),
                "Shaders directory");
        }
    }
}
