using VirtualPaper.AppSettingsPanel.Utils;
using VirtualPaper.Common;

namespace VirtualPaper.UI.Test.T_AppSettings;

[TestClass]
public class LegalDocumentsTests {
    private string _root = null!;

    [TestInitialize]
    public void SetUp() {
        _root = Path.Combine(Path.GetTempPath(), "VPLegal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "en-US"));
        File.WriteAllText(Path.Combine(_root, "en-US", "README.md"), "# English\nVersion 1\n\n"
            + "<a id=\"privacy-policy\"></a>\n## Privacy\nEnglish privacy.\n"
            + "<a id=\"ai-processing-and-rights\"></a>\n## AI\nAI rights.\n"
            + "<a id=\"copyright-and-licenses\"></a>\n## Copyright\nCopyright terms.");
    }

    [TestCleanup]
    public void CleanUp() => Directory.Delete(_root, recursive: true);

    [TestMethod]
    public async Task ReadSection_UsesMatchedWritingSystemAndIncludesDocumentVersion() {
        Directory.CreateDirectory(Path.Combine(_root, "zh-TW"));
        File.WriteAllText(Path.Combine(_root, "zh-TW", "README.md"), "# 繁體中文\nVersion 2\n\n"
            + "<a id=\"privacy-policy\"></a>\n繁體隱私\n<a id=\"ai-processing-and-rights\"></a>\n其他章節");
        string text = await LegalDocuments.ReadSectionAsync(_root, "zh-HK", "privacy-policy");
        Assert.Contains("繁體隱私", text);
        Assert.Contains("Version 2", text);
        Assert.DoesNotContain("其他章節", text);
        Assert.DoesNotContain("English privacy", text);
    }

    [TestMethod]
    [DataRow("fr-FR")]
    [DataRow("unsupported")]
    [DataRow("../../outside")]
    public async Task ReadSection_MissingOrUnknownLocaleFallsBackToEnglish(string language) {
        string text = await LegalDocuments.ReadSectionAsync(_root, language, "ai-processing-and-rights");
        Assert.Contains("AI rights.", text);
        Assert.DoesNotContain("English privacy.", text);
        Assert.DoesNotContain("Copyright terms.", text);
    }

    [TestMethod]
    [DataRow("privacy-policy")]
    [DataRow("ai-processing-and-rights")]
    [DataRow("copyright-and-licenses")]
    public async Task ReadSection_FollowsLanguageSwitchesAndAliasesForEverySection(string section) {
        string[] sections = ["privacy-policy", "ai-processing-and-rights", "copyright-and-licenses"];
        foreach (var language in SupportedLanguages.Languages) {
            string code = language.Codes[0];
            string folder = Path.Combine(_root, code);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "README.md"), "# " + code + "\nVersion 1\n\n"
                + string.Join('\n', sections.Select(id => $"<a id=\"{id}\"></a>\n## {id}\n{code}/{id} content.")));
        }
        // Revisit the first language after traversing all aliases to expose stale selections.
        foreach (var language in SupportedLanguages.Languages.Append(SupportedLanguages.Languages[0])) {
            string code = language.Codes[0];
            foreach (string alias in language.Codes) {
                Assert.AreEqual(Path.Combine(_root, code, "README.md"), LegalDocuments.GetPath(_root, alias));
                string text = await LegalDocuments.ReadSectionAsync(_root, alias, section);
                Assert.Contains($"{code}/{section} content.", text, alias);
                foreach (string other in sections.Where(id => id != section))
                    Assert.DoesNotContain($"{code}/{other} content.", text, alias);
            }
        }
    }

    [TestMethod]
    public async Task ReadSection_LastSectionRunsToEndWithoutLosingContent() {
        string text = await LegalDocuments.ReadSectionAsync(_root, "en-US", "copyright-and-licenses");
        Assert.Contains("Copyright terms.", text);
        Assert.DoesNotContain("AI rights.", text);
    }

    [TestMethod]
    public async Task ReadSection_MissingSourceOrSectionDoesNotReturnAnEmptyPolicy() {
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => LegalDocuments.ReadSectionAsync(_root, "en-US", "other"));
        File.WriteAllText(Path.Combine(_root, "en-US", "README.md"), "No sections");
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => LegalDocuments.ReadSectionAsync(_root, "en-US", "privacy-policy"));
        File.Delete(Path.Combine(_root, "en-US", "README.md"));
        await Assert.ThrowsExactlyAsync<FileNotFoundException>(() => LegalDocuments.ReadSectionAsync(_root, "en-US", "privacy-policy"));
    }

    [TestMethod]
    public void ResolveLocalLink_ResolvesBundledLicenseFromLocaleFolder() {
        string document = Path.Combine(_root, "en-US", "README.md");
        Assert.AreEqual(Path.Combine(_root, "licenses", "GPL-3.0.txt"),
            LegalDocuments.ResolveLocalLink(_root, document, "../licenses/GPL-3.0.txt"));
    }

    [TestMethod]
    [DataRow("../../outside.txt")]
    [DataRow("../program.exe")]
    [DataRow("../README.md")]
    [DataRow("https://example.com/license.txt")]
    [DataRow("file:///C:/private.txt")]
    public void ResolveLocalLink_RejectsFilesOutsideTheBundleAndExecutableTargets(string link) =>
        Assert.IsNull(LegalDocuments.ResolveLocalLink(_root, Path.Combine(_root, "en-US", "README.md"), link));
}
