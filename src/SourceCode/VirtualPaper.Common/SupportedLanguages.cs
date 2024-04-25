using System.Collections.ObjectModel;
using VirtualPaper.Common.Utils.Localization;

namespace VirtualPaper.Common
{
    public static class SupportedLanguages
    {
        private readonly static LanguagesModel[] languages = [
            new("简体中文(zh-CN)", ["zh-CN"]),
            new("English(en-US)", ["en-US"])
        ];

        public static ReadOnlyCollection<LanguagesModel> Languages => Array.AsReadOnly(languages);

        /// <summary>
        /// Returns language code if exists, default language(en) otherwise.
        /// </summary>
        /// <param name="langCode"></param>
        /// <returns></returns>
        public static LanguagesModel GetLanguage(string langCode) =>
            Languages.FirstOrDefault(lang => lang.Codes.Contains(langCode, StringComparer.OrdinalIgnoreCase)) ?? Languages[0];
    }
}
