using System;
using System.IO;
using System.Threading.Tasks;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;

namespace VirtualPaper.AppSettingsPanel.Utils;

internal static class LegalDocuments {
    internal static string GetPath(string root, string? language) {
        string code = SupportedLanguages.GetLanguage(language).Codes[0];
        string path = Path.Combine(root, code, "README.md");
        return File.Exists(path) ? path : Path.Combine(root, SupportedLanguages.DefaultLanguage, "README.md");
    }

    internal static async Task<string> ReadSectionAsync(string root, string? language, string section) {
        if (section is not ("privacy-policy" or "ai-processing-and-rights" or "copyright-and-licenses"))
            throw new ArgumentOutOfRangeException(nameof(section));
        string text = await File.ReadAllTextAsync(GetPath(root, language));
        string marker = $"<a id=\"{section}\"></a>";
        int start = text.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) throw new InvalidDataException("The requested legal document section is missing.");
        int headerEnd = text.IndexOf("<a id=", StringComparison.Ordinal);
        start += marker.Length;
        int end = text.IndexOf("<a id=", start, StringComparison.Ordinal);
        return text[..headerEnd].Trim() + "\n\n" + text[start..(end < 0 ? text.Length : end)].Trim();
    }

    internal static string? ResolveLocalLink(string root, string documentPath, string link) {
        if (Uri.TryCreate(link, UriKind.Absolute, out _) || Path.IsPathRooted(link)) return null;
        string path = Path.GetFullPath(link, Path.GetDirectoryName(documentPath)!);
        // Legal links may open bundled plain-text notices, never arbitrary files or programs.
        return Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase)
            && FileUtil.IsPathWithinDirectory(root, path) ? path : null;
    }
}
