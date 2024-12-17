using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Models.Cores {
    /// <summary>
    /// Wallpaper arragement on monitor.
    /// </summary>
    [Serializable]
    public class WallpaperLayout( 
        string folderPath, 
        string monitorDeviceId, 
        string monitorContent, 
        string rtype) : IWallpaperLayout {
        public string FolderPath { get; set; } = folderPath;
        public string MonitorDeviceId { get; set; } = monitorDeviceId;
        public string MonitorContent { get; set; } = monitorContent;
        public string RType { get; set; } = rtype;

    }
}
