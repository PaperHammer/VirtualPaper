using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace VirtualPaper.TestSupport;

/// <summary>Read acceptance expectations from the requested locale, independently of the host's UI language.</summary>
internal static class LocalizedTestStrings {
    internal static string Read(string uiDirectory, string language, string key) {
        string locale = CultureInfo.GetCultureInfo(language).Name;
        if (locale.Length == 0) throw new InvalidDataException("Acceptance requires an explicit UI language.");
        string path = Path.Combine(uiDirectory, "VirtualPaper.UIComponent", "Strings", locale, "Resources.resw");
        string? value = XDocument.Load(path).Root?.Elements("data")
            .SingleOrDefault(entry => (string?)entry.Attribute("name") == key)?.Element("value")?.Value;
        return !string.IsNullOrWhiteSpace(value) ? value
            : throw new InvalidDataException($"Missing acceptance translation: {locale}/{key}");
    }
}
