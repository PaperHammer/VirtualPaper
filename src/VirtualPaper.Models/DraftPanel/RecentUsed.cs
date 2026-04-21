using System.Text.Json.Serialization;
using VirtualPaper.Common;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Models.DraftPanel {
    [JsonSerializable(typeof(RecentUsed))]
    [JsonSerializable(typeof(IRecentUsed))]
    [JsonSerializable(typeof(List<RecentUsed>))]
    [JsonSerializable(typeof(List<IRecentUsed>))]
    public partial class RecentUsedContext : JsonSerializerContext { }

    public class RecentUsed : IRecentUsed {
        public WpFileType Type { get; init; }
        [JsonIgnore]
        public string Glyph => GetGlyphByType(Type);
        public string FileName { get; init; } = string.Empty;
        public string FilePath { get; init; } = string.Empty;
        public string DateTime { get; set; } = string.Empty;

        [JsonConstructor]
        public RecentUsed(WpFileType type, string fileName, string filePath, string dateTime) {
            Type = type;
            FileName = fileName;
            FilePath = filePath;
            DateTime = dateTime;
        }

        private static string GetGlyphByType(WpFileType type) {
            return type switch {
                WpFileType.FDesign => "\uEB3C",
                WpFileType.FImage => "\uEC88",
                _ => string.Empty,
            };
        }
    }
}
