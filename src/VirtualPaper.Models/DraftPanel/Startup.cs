using VirtualPaper.Common;
using Windows.System;

namespace VirtualPaper.Models.DraftPanel {
    public class Startup {
        public DraftPanelStartupType Type { get; }
        public string Glyph { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Desc { get; set; } = string.Empty;
        public VirtualKey ShortCut { get; set; } = VirtualKey.None;

        public Startup(DraftPanelStartupType type, string title, string desc, VirtualKey shortCut) {
            Type = type;
            Glyph = GetGlyphByType(type);
            Title = title;
            Desc = desc;
            ShortCut = shortCut;
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
