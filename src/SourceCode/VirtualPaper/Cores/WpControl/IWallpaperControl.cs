using System.Collections.ObjectModel;
using VirtualPaper.Common.Utils.Files.Models;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Grpc.Service.WallpaperControl;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.WallpaperMetaData;
using WallpaperType = VirtualPaper.Common.WallpaperType;

namespace VirtualPaper.Cores.WpControl
{
    /// <summary>
    /// 桌面壁纸控制
    /// </summary>
    public interface IWallpaperControl : IDisposable
    {
        /// <summary>
        /// 桌面窗口句柄
        /// </summary>
        IntPtr DesktopWorkerW { get; }

        /// <summary>
        /// 使用中的壁纸
        /// </summary>
        ReadOnlyCollection<IWallpaper> Wallpapers { get; }

        /// <summary>
        /// 关闭所有壁纸
        /// </summary>
        void CloseAllWallpapers();

        /// <summary>
        /// 关闭指定显示器的壁纸
        /// </summary>
        /// <param name="monitor"></param>
        void CloseWallpaper(IMonitor monitor);

        /// <summary>
        /// 重置壁纸
        /// </summary>
        Task ResetWallpaperAsync();

        /// <summary>
        /// 还原壁纸
        /// </summary>
        RestartWallpaperResponse RestoreWallpaper();

        void PreviewWallpaper(IMetaData metaData, bool isLibraryPreview);
        void ModifyPreview(string controlName, string propertyName, string value);

        void SeekWallpaper(IMetaData metaData, float seek, PlaybackPosType type);
        void SeekWallpaper(IMonitor monitor, float seek, PlaybackPosType type);

        //void SendMessageWallpaper(string folderPath, IpcMessage msg);
        void SendMessageWallpaper(IMonitor monitor, string folderPath, IpcMessage msg);

        IMetaData GetWallpaper(string folderPath);
        Task<SetWallpaperResponse> SetWallpaperAsync(IMetaData metaData, IMonitor monitor, CancellationToken token);
        UpdateWpResponse UpdateWp(string monitorId, IMetaData metaData, CancellationToken token);
        IMetaData CreateWallpaper(string folderPath, string filePath, WallpaperType type, CancellationToken token);
        FileProperty TryGetProeprtyInfo(string filePath, WallpaperType type);
        void ChangeWallpaperLayoutFolrderPath(string previousDir, string newDir);
        void ResetWpCustomize(string wpCustomizePath, WallpaperType type);

        /// <summary>
        /// Wallpaper set/update/removed.
        /// </summary>
        public event EventHandler? WallpaperChanged;

        /// <summary>
        /// Errors occured in wallpaper core.
        /// </summary>
        public event EventHandler<Exception>? WallpaperError;
        
        /// <summary>
        /// Wallpaper core services restarted.
        /// </summary>
        public event EventHandler? WallpaperReset;
    }
}
