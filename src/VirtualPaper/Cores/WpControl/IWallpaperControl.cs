using System.Collections.ObjectModel;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files.Models;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Cores.WpControl {
    /// <summary>
    /// 桌面壁纸控制
    /// </summary>
    public interface IWallpaperControl : IDisposable {
        /// <summary>
        /// Wallpaper set/update/closed.
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

        /// <summary>
        /// 桌面窗口句柄
        /// </summary>
        IntPtr DesktopWorkerW { get; }

        /// <summary>
        /// 使用中的壁纸
        /// </summary>
        ReadOnlyCollection<IWpPlayer> Wallpapers { get; }

        #region wallpaper actions
        void CloseAllWallpapers();
        void CloseWallpaper(IMonitor monitor);
        (string?, RuntimeType?) GetPrimaryWpFilePathRType();
        IWpMetadata GetWallpaperByFolderPath(string folderPath, string monitorContent, string rtype);
        IWpBasicData GetWpBasicDataByForlderPath(string folderPath);
        string GetPlayerStartArgsInRunning(string monitorId);
        string? GetPlayerStartArgs(IWpPlayerData wpPlayingData, CancellationToken toke = default);
        Task ResetWallpaperAsync();
        Grpc_RestartWallpaperResponse RestoreWallpaper();
        Task<Grpc_SetWallpaperResponse> SetWallpaperAsync(IWpPlayerData data, IMonitor monitor, bool fromPreview = false, CancellationToken token = default);
        void SeekWallpaper(IWpPlayerData data, float seek, PlaybackPosType type);
        void SeekWallpaper(IMonitor monitor, float seek, PlaybackPosType type);
        void SendMessageWallpaper(string deviceId, string ipcMsg);
        #endregion

        #region data
        IWpBasicData CreateBasicData(string filePath, FileType ftype, string? folderName = null, bool isAutoSave = true, CancellationToken token = default);
        IWpBasicData CreateBasicDataInMem(string filePath, FileType ftype, string? folderName = null, bool isAutoSave = true, CancellationToken token = default);
        IWpRuntimeData CreateRuntimeData(string filePath, string folderPath, RuntimeType rtype, bool isPreview, string monitorContent);
        Task<IWpBasicData> UpdateBasicDataAsync(string folderPath, string folderName, string filePath, FileType ftype, CancellationToken token = default);
        #endregion

        #region utils
        //nint GetWorkWHwnd();
        //nint GetProgmanHwnd();
        void ChangeWallpaperLayoutFolrderPath(string previousDir, string newDir);
        FileProperty GetWpProperty(string filePath, FileType ftype);
        Grpc_MonitorData GetRunMonitorByWallpaper(string wpUid);
        #endregion
    }
}
