namespace VirtualPaper.Models.Cores.Interfaces {
    /// <summary>
    /// 显示器所显示的内容路径
    /// </summary>
    public interface IWallpaperLayout {
        string FolderPath { get; set; }
        string MonitorDeviceId { get; set; }
        string MonitorContent { get; set; }
        string RType { get; set; }
    }
}
