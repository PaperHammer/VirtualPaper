using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace VirtualPaper.WpSettingsPanel.Utils {
    public record WallpaperOrganization {
        public bool Favorite { get; init; }
        public string[] Tags { get; init; } = [];

        internal static string[] NormalizeTags(IEnumerable<string> tags) => tags
            .SelectMany(tag => (tag ?? "").Split([';', ',', '；', '，', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
            .Select(tag => tag.Trim()).Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(tag => tag, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public enum WallpaperTagUpdate { Replace, Add, Remove }

    internal class WallpaperLibraryState {
        public Dictionary<string, WallpaperOrganization> Items { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    [JsonSerializable(typeof(WallpaperLibraryState))]
    internal partial class WallpaperLibraryStateContext : JsonSerializerContext { }
}
