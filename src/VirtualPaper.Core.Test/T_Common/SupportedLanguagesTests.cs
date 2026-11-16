using System.Globalization;
using System.Resources;
using VirtualPaper.Common;
using VirtualPaper.Services;

namespace VirtualPaper.Core.Test.T_Common;

[TestClass]
public class SupportedLanguagesTests {
    [TestMethod]
    [DataRow("en-US", "en-US")]
    [DataRow("EN-us", "en-US")]
    [DataRow("en-GB", "en-US")]
    [DataRow("en-AU", "en-US")]
    [DataRow("english", "en-US")]
    [DataRow("zh-CN", "zh-CN")]
    [DataRow("zh-SG", "zh-CN")]
    [DataRow("zh-Hans", "zh-CN")]
    [DataRow("zh-Hans-HK", "zh-CN")]
    [DataRow("chinese", "zh-CN")]
    [DataRow(" zh-CN ", "zh-CN")]
    [DataRow("zh-Hant-HK", "zh-TW")]
    [DataRow("ja", "ja-JP")]
    [DataRow("ko", "ko-KR")]
    [DataRow("de-AT", "de-DE")]
    [DataRow("fr-CA", "fr-FR")]
    [DataRow("es-MX", "es-ES")]
    [DataRow("pt-PT", "pt-BR")]
    [DataRow("ru-KZ", "ru-RU")]
    [DataRow("en-Latn-GB", "en-US")]
    [DataRow("ja-Jpan-JP", "ja-JP")]
    [DataRow("fr-Latn-CA", "fr-FR")]
    [DataRow("ru-Cyrl-KZ", "ru-RU")]
    public void CanonicalRegionalAndLegacyNamesResolve(string requested, string expected) =>
        Assert.AreEqual(expected, SupportedLanguages.GetLanguage(requested).Codes[0]);

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("unsupported")]
    [DataRow("../zh-CN")]
    public void InvalidOrUnsupportedNamesFallBackToEnglish(string? requested) =>
        Assert.AreEqual("en-US", SupportedLanguages.GetLanguage(requested).Codes[0]);

    [TestMethod]
    [DataRow("zh-HK")]
    [DataRow("zh-TW")]
    [DataRow("zh-Hant")]
    public void TraditionalChineseDoesNotFallBackToSimplifiedChinese(string requested) {
        var matched = SupportedLanguages.MatchLanguage(requested);
        Assert.AreEqual("zh-TW", matched?.Codes[0], "Traditional Chinese must match its own translation, not the neutral zh alias.");
    }

    [TestMethod]
    public void UnsupportedWritingSystemDoesNotMatchNeutralAlias() =>
        Assert.IsNull(SupportedLanguages.MatchLanguage("zh-Latn"));

    [TestMethod]
    public void PreferredLanguagesAreTriedInOrderBeforeDefaulting() {
        Assert.AreEqual("zh-CN", SupportedLanguages.ResolveLanguage(["unsupported", "zh-SG", "en-US"]));
        Assert.AreEqual("en-US", SupportedLanguages.ResolveLanguage(["en-GB", "zh-CN"]));
        Assert.AreEqual("en-US", SupportedLanguages.ResolveLanguage([]));
        Assert.AreEqual("en-US", SupportedLanguages.ResolveLanguage([null, "unsupported"]));
    }

    [TestMethod]
    public void CatalogHasUniqueAliasesAndAStableDefault() {
        Assert.AreEqual("en-US", SupportedLanguages.DefaultLanguage);
        Assert.AreSame(SupportedLanguages.Languages, SupportedLanguages.Languages);
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in SupportedLanguages.Languages) {
            Assert.IsFalse(string.IsNullOrWhiteSpace(language.Language));
            foreach (string code in language.Codes) {
                Assert.IsTrue(codes.Add(code), $"Duplicate alias: {code}");
                Assert.AreSame(language, SupportedLanguages.GetLanguage(code));
            }
        }
    }

    [TestMethod]
    public void GeneratedCoreAndCommonResourcesUseEnglishAsTheirNeutralFallback() {
        var core = new ResourceManager("VirtualPaper.Properties.lang", typeof(UserSettingsService).Assembly);
        var common = new ResourceManager("VirtualPaper.Common.Resources.FileAccess", typeof(SupportedLanguages).Assembly);
        Assert.AreEqual("暂停", core.GetString("AppUpdater_ActionButtonText_Downloading", CultureInfo.GetCultureInfo("zh-CN")));
        Assert.AreEqual("读取", common.GetString("Read", CultureInfo.GetCultureInfo("zh-CN")));
        Assert.AreEqual("Pause", core.GetString("AppUpdater_ActionButtonText_Downloading", CultureInfo.GetCultureInfo("it-IT")));
        Assert.AreEqual("reading", common.GetString("Read", CultureInfo.GetCultureInfo("it-IT")));
    }

    [TestMethod]
    [DataRow("zh-TW", "暫停", "讀取")]
    [DataRow("ja-JP", "一時停止", "読み込み")]
    [DataRow("ko-KR", "일시 정지", "읽기")]
    [DataRow("de-DE", "Pausieren", "Lesen")]
    [DataRow("fr-FR", "Suspendre", "lecture")]
    [DataRow("es-ES", "Pausar", "lectura")]
    [DataRow("pt-BR", "Pausar", "ler")]
    [DataRow("ru-RU", "Приостановить", "чтение")]
    public void AdditionalLanguageSatellitesContainTranslatedResources(string code, string pause, string read) {
        var culture = CultureInfo.GetCultureInfo(code);
        var core = new ResourceManager("VirtualPaper.Properties.lang", typeof(UserSettingsService).Assembly);
        var common = new ResourceManager("VirtualPaper.Common.Resources.FileAccess", typeof(SupportedLanguages).Assembly);
        Assert.AreEqual(pause, core.GetString("AppUpdater_ActionButtonText_Downloading", culture));
        Assert.AreEqual(read, common.GetString("Read", culture));
        Assert.IsFalse(string.IsNullOrWhiteSpace(core.GetString("AppUpdater_WindowTitle", culture)));
        Assert.AreNotEqual("Virtual Paper Updater", core.GetString("AppUpdater_WindowTitle", culture));
    }

    [TestMethod]
    [DoNotParallelize]
    public void ChangingCoreLanguageKeepsRegionalNumberFormats() {
        var previous = CultureInfo.CurrentUICulture;
        var previousDefault = CultureInfo.DefaultThreadCurrentUICulture;
        var region = CultureInfo.CurrentCulture;
        try {
            lang.LanguageManager.Instance.ChangeLanguage(CultureInfo.GetCultureInfo("zh-CN"));
            Assert.AreEqual("zh-CN", CultureInfo.CurrentUICulture.Name);
            Assert.AreSame(region, CultureInfo.CurrentCulture);
            Assert.AreEqual("zh-CN", CultureInfo.DefaultThreadCurrentUICulture?.Name);
        }
        finally {
            lang.LanguageManager.Instance.ChangeLanguage(previous);
            CultureInfo.DefaultThreadCurrentUICulture = previousDefault;
        }
    }
}
