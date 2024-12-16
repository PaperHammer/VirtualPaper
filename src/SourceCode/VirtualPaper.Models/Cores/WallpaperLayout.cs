using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Models.Cores {
    /// <summary>
    /// Wallpaper arragement on monitor.
    /// </summary>
    [Serializable]
    public class WallpaperLayout( string folderPath, string monitorDeviceId) : IWallpaperLayout {
        public string FolderPath { get; set; } = folderPath;
        public string MonitorDeviceId { get; set; } = monitorDeviceId;
    }
}
