using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Models.Cores {
    /// <summary>
    /// Wallpaper arragement on monitor.
    /// </summary>
    [Serializable]
    public class WallpaperLayout(Monitor monitor, string folderPath) : IWallpaperLayout {
        public Monitor Monitor { get; set; } = monitor;

        public string FolderPath { get; set; } = folderPath;
    }
}
