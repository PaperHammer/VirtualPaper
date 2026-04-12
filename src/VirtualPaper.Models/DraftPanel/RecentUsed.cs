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
        public FileType Type { get; init; }
        [JsonIgnore]
        public string Glyph => GetGlyphByType(Type);
        public string FileName { get; init; } = string.Empty;
        public string FilePath { get; init; } = string.Empty;
        public string DateTime { get; set; } = string.Empty;

        [JsonConstructor]
        public RecentUsed(FileType type, string fileName, string filePath, string dateTime) {
            Type = type;
            FileName = fileName;
            FilePath = filePath;
            DateTime = dateTime;
        }

        private static string GetGlyphByType(FileType type) {
            return type switch {
                FileType.FDesign => "\uEB3C",
                FileType.FImage => "\uEC88",
                _ => string.Empty,
            };
        }
    }
}
