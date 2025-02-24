using VirtualPaper.Common;
using Windows.System;

namespace VirtualPaper.Models.DraftPanel {
    public class Startup(DraftPanelStartupType type, string title, string desc, VirtualKey shortCut) {
        public DraftPanelStartupType Type { get; } = type;
        public string Glyph { get; set; } = GetGlyphByType(type);
        public string Title { get; set; } = title;
        public string Desc { get; set; } = desc;
        public VirtualKey ShortCut { get; set; } = shortCut;

        private static string GetGlyphByType(DraftPanelStartupType type) {
            return type switch {
                DraftPanelStartupType.OpenVpd => "\ue7C3",
                DraftPanelStartupType.OpenFile => "\ue8E5",
                DraftPanelStartupType.NewVpd => "\ueB3C",
                _ => string.Empty,
            };
        }
    }
}
