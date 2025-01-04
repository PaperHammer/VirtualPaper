using VirtualPaper.Common;

namespace VirtualPaper.Models.UI {
    public class WallpaperCreateData {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Icon { get; set; } = string.Empty;

        public WallpaperCreateType CreateType;
    }
}
