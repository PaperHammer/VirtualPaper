using VirtualPaper.Common;

namespace VirtualPaper.Models.DraftPanel {
    public class RecentUsed {
        public ProjectType Type { get; }
        public string Glyph { get; } = string.Empty;
        public string ProjectName { get; } = string.Empty;
        public string FilePath { get; } = string.Empty;
        public string DateTime { get; } = string.Empty;

        public RecentUsed(ProjectType type, string projectName, string filePath, string dataTime) {
            Type = type;
            Glyph = GetGlyphByType(type);
            ProjectName = projectName;
            FilePath = filePath;
            DateTime = dataTime;
        }

        private static string GetGlyphByType(ProjectType type) {
            return type switch {
                ProjectType.PImage => "\ueC88",
                _ => string.Empty,
            };
        }
    }
}
