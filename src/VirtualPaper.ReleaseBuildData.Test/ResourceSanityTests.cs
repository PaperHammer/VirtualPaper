using System.Resources;
using System.Runtime.Loader;
using System.Text.Json;
using System.Xml.Linq;

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
        [DynamicData(nameof(SupportedLocales))]
        public void Plugin_UI_LiveTranslations_MatchTheirSourceStrings(string locale) {
            string path = Path.Combine(PluginsDir, "UI", "VirtualPaper.UIComponent", "Strings", locale, "Resources.resw");
            using var stream = File.OpenRead(path);
            var entries = ReadUiStrings(stream);
            var expected = ReadSourceStrings(locale);
            const string generalTabKey = "AppSettings_SelBarItem1_General";
            Assert.AreEqual(expected[generalTabKey],
                VirtualPaper.TestSupport.LocalizedTestStrings.Read(Path.Combine(PluginsDir, "UI"), locale, generalTabKey),
                $"Installed acceptance must use the requested language: {locale}");
            // Embed the build's expected mappings in the test assembly; runtime
            // packages need only the generated resw files, not build metadata.
            using var mappingStream = typeof(ResourceSanityTests).Assembly.GetManifestResourceStream("VirtualPaper.XamlResources.xml");
            Assert.IsNotNull(mappingStream, "The test assembly is missing XAML resource mappings.");
            foreach (var mapping in XDocument.Load(mappingStream).Root!.Elements("Resource")) {
                expected.Add((string)mapping.Attribute("Name")!, expected[(string)mapping.Attribute("Source")!]);
            }
            AssertLocalizedStrings(entries, expected, path);
        }

        [TestMethod]
        [TestCategory("ReleaseBuild")]
        [DynamicData(nameof(RuntimeResources))]
        public void Runtime_Translations_MatchTheirSourceStrings(string locale, string target, string path, string resourceName) {
            var source = ReadSourceStrings(locale);
            using var mappingStream = typeof(ResourceSanityTests).Assembly.GetManifestResourceStream("VirtualPaper.ResourceMappings.xml");
            Assert.IsNotNull(mappingStream, "The test assembly is missing Core/Common resource mappings.");
            var expected = XDocument.Load(mappingStream).Root!.Elements("Resource")
                .Where(mapping => (string?)mapping.Attribute("Target") == target)
                .ToDictionary(mapping => (string)mapping.Attribute("Name")!,
                    mapping => source[(string)mapping.Attribute("Source")!], StringComparer.Ordinal);
            Assert.IsGreaterThan(0, expected.Count, $"No embedded mappings for {target}.");
            string fullPath = Path.Combine(ReleaseBuildSanityTests.ReleaseDir, path);
            AssertLocalizedStrings(ReadAssemblyStrings(fullPath, resourceName), expected, $"{locale}/{path}");
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
            using var catalog = JsonDocument.Parse(stream);
            return catalog.RootElement.GetProperty("Languages").EnumerateArray()
                .Select(language => new object[] { language.GetProperty("Code").GetString()! }).ToArray();
        }

        public static IEnumerable<object[]> RuntimeResources() {
            using var stream = typeof(ResourceSanityTests).Assembly.GetManifestResourceStream("VirtualPaper.LanguageCatalog.json")!;
            using var catalog = JsonDocument.Parse(stream);
            string defaultLanguage = catalog.RootElement.GetProperty("DefaultLanguage").GetString()!;
            var owners = new[] {
                (Target: "Core", Directory: "", Assembly: "VirtualPaper", Resource: "VirtualPaper.Properties.lang"),
                (Target: "Common", Directory: "", Assembly: "VirtualPaper.Common", Resource: "VirtualPaper.Common.Resources.FileAccess"),
                (Target: "Common", Directory: "Plugins/UI", Assembly: "VirtualPaper.Common", Resource: "VirtualPaper.Common.Resources.FileAccess")
            };
            foreach (var language in catalog.RootElement.GetProperty("Languages").EnumerateArray()) {
                string locale = language.GetProperty("Code").GetString()!;
                bool isNeutral = locale == defaultLanguage;
                foreach (var owner in owners) {
                    string path = isNeutral ? Path.Combine(owner.Directory, owner.Assembly + ".dll")
                        : Path.Combine(owner.Directory, locale, owner.Assembly + ".resources.dll");
                    yield return [locale, owner.Target, path, owner.Resource + (isNeutral ? "" : "." + locale) + ".resources"];
                }
            }
        }

        private static Dictionary<string, string> ReadSourceStrings(string locale) {
            var assembly = typeof(ResourceSanityTests).Assembly;
            string name = assembly.GetManifestResourceNames().Single(name =>
                name.StartsWith("VirtualPaper.Localization.Source.", StringComparison.Ordinal)
                && name.Replace('\\', '/').EndsWith($"{locale}/Resources.resw", StringComparison.Ordinal));
            using var stream = assembly.GetManifestResourceStream(name)!;
            return ReadUiStrings(stream);
        }

        private static Dictionary<string, string> ReadUiStrings(Stream stream) {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in XDocument.Load(stream).Root!.Elements("data")) {
                string? key = (string?)entry.Attribute("name"), value = (string?)entry.Element("value");
                if (string.IsNullOrEmpty(key) || value == null) throw new InvalidDataException("Malformed UI translation.");
                values.Add(key, value);
            }
            return values;
        }

        private static Dictionary<string, string> ReadAssemblyStrings(string path, string resourceName) {
            // Inspect each physical copy independently, without assembly-name reuse or file locks.
            var context = new AssemblyLoadContext($"Localization-{Guid.NewGuid():N}", isCollectible: true);
            try {
                using var image = File.OpenRead(path);
                var assembly = context.LoadFromStream(image);
                using var stream = assembly.GetManifestResourceStream(resourceName);
                Assert.IsNotNull(stream, $"Missing packaged resource: {path}/{resourceName}");
                using var reader = new ResourceReader(stream);
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                var entries = reader.GetEnumerator();
                while (entries.MoveNext()) values.Add((string)entries.Key, (string)entries.Value!);
                return values;
            }
            finally { context.Unload(); }
        }

        private static void AssertLocalizedStrings(Dictionary<string, string> actual, Dictionary<string, string> expected, string description) {
            Assert.HasCount(expected.Count, actual, $"Localization key count differs: {description}");
            foreach (var (key, value) in expected) {
                Assert.IsTrue(actual.TryGetValue(key, out var text), $"Missing packaged translation: {description}/{key}");
                Assert.AreEqual(value.Replace("\r\n", "\n"), text!.Replace("\r\n", "\n"), $"Stale packaged translation: {description}/{key}");
            }
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
