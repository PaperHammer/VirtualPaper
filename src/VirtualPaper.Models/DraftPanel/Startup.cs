using VirtualPaper.Common;
using Windows.System;

namespace VirtualPaper.Models.DraftPanel {
    public class Startup(ConfigSpacePanelType type, string title, string desc, VirtualKey shortCut) {
        public ConfigSpacePanelType Type { get; } = type;
        public string Glyph { get; set; } = GetGlyphByType(type);
        public string Title { get; set; } = title;
        public string Desc { get; set; } = desc;
        public VirtualKey ShortCut { get; set; } = shortCut;

        private static string GetGlyphByType(ConfigSpacePanelType type) {
            return type switch {
                ConfigSpacePanelType.OpenVpd => "\ue7C3",
                ConfigSpacePanelType.OpenFile => "\ue8E5",
                ConfigSpacePanelType.NewVpd => "\ueB3C",
                _ => string.Empty,
            };
        }
    }
}
