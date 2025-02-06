using VirtualPaper.Common;

namespace VirtualPaper.Models.ProjectPanel {
    public class Startup {
        public DraftPanelStartupType Type { get; }
        public string Glyph { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Desc { get; set; } = string.Empty;

        public Startup(DraftPanelStartupType type, string title, string desc) {
            Type = type;
            Glyph = GetGlyphByType(type);
            Title = title;
            Desc = desc;
        }

        private static string GetGlyphByType(DraftPanelStartupType type) {
            return type switch {
                DraftPanelStartupType.OpenVpd => "\ue7C3",
                DraftPanelStartupType.OpenFile => "\ue8E5",
                DraftPanelStartupType.OpenFolder => "\ue838",
                DraftPanelStartupType.NewVpd => "\ueB3C",
                _ => string.Empty,
            };
        }
    }
}
