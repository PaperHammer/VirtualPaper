using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using VirtualPaper.Common.Utils.Localization;

namespace VirtualPaper.Common;

/// <summary>The same embedded catalog drives both application channels and their packaging.</summary>
public static class SupportedLanguages {
    public static string DefaultLanguage { get; }
    public static ReadOnlyCollection<LanguagesModel> Languages { get; }
    private static readonly Dictionary<string, LanguagesModel> Matches = new(StringComparer.OrdinalIgnoreCase);

    static SupportedLanguages() {
        using var stream = typeof(SupportedLanguages).Assembly.GetManifestResourceStream("VirtualPaper.LanguageCatalog.json")
            ?? throw new InvalidDataException("The supported language catalog is missing.");
        using var document = JsonDocument.Parse(stream);
        DefaultLanguage = document.RootElement.GetProperty("DefaultLanguage").GetString()!;
        var languages = new List<LanguagesModel>();
        foreach (var entry in document.RootElement.GetProperty("Languages").EnumerateArray()) {
            string code = entry.GetProperty("Code").GetString()!;
            string[] aliases = entry.GetProperty("Aliases").EnumerateArray().Select(item => item.GetString()!).ToArray();
            var language = new LanguagesModel(entry.GetProperty("Name").GetString()!, [code, .. aliases]);
            languages.Add(language);
            foreach (string alias in language.Codes) Matches.Add(alias, language);
            // Accept markers written by older installers without exposing their names as locales.
            Matches.Add(entry.GetProperty("InstallerName").GetString()!, language);
        }
        Languages = languages.AsReadOnly();
        if (!Matches.ContainsKey(DefaultLanguage)) throw new InvalidDataException("The default language is not supported.");
    }

    public static LanguagesModel GetLanguage(string? languageCode) => MatchLanguage(languageCode) ?? Matches[DefaultLanguage];

    /// <summary>Try each user preference before falling back to English.</summary>
    public static string ResolveLanguage(IEnumerable<string?> languageCodes) {
        foreach (string? code in languageCodes) {
            if (MatchLanguage(code) is { } language) return language.Codes[0];
        }
        return DefaultLanguage;
    }

    public static LanguagesModel? MatchLanguage(string? languageCode) {
        if (string.IsNullOrWhiteSpace(languageCode)) return null;
        languageCode = languageCode.Trim();
        if (Matches.TryGetValue(languageCode, out var exact)) return exact;
        try {
            bool scriptFound = false;
            for (var culture = CultureInfo.GetCultureInfo(languageCode); culture.Name.Length > 0; culture = culture.Parent) {
                scriptFound |= culture.Name.Split('-').Skip(1).Any(part => part.Length == 4);
                // Do not cross writing systems (for example, Hant -> Hans) via a neutral parent.
                if (scriptFound && !culture.Name.Contains('-')) return null;
                if (Matches.TryGetValue(culture.Name, out var matched)) return matched;
            }
        }
        catch (CultureNotFoundException) { }
        return null;
    }
}
