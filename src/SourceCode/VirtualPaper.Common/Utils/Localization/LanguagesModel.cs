namespace VirtualPaper.Common.Utils.Localization {
    [Serializable]
    public class LanguagesModel(string language, string[] codes) : ILanguagesModel {
        public string Language { get; set; } = language;
        public string[] Codes { get; set; } = codes;
    }
}
