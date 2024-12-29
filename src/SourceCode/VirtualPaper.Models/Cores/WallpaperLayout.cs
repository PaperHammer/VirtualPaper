using System.Text.Json.Serialization;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Models.Cores {
    [JsonSerializable(typeof(WallpaperLayout))]
    [JsonSerializable(typeof(IWallpaperLayout))]
    [JsonSerializable(typeof(List<WallpaperLayout>))]
    [JsonSerializable(typeof(List<IWallpaperLayout>))]
    public partial class WallpaperLayoutContext : JsonSerializerContext { }

    /// <summary>
    /// Wallpaper arragement on monitor.
    /// </summary>
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
