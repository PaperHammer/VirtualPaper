using System.Collections.ObjectModel;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Grpc.Service.Models;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Grpc.Client.Interfaces {
    public interface IWallpaperControlClient : IDisposable {
        event EventHandler? WallpaperChanged;
        event EventHandler<Exception>? WallpaperError;

        Version AssemblyVersion { get; }
        string BaseDirectory { get; }
        ReadOnlyCollection<IWpMetadata> Wallpapers { get; }

        #region wallpaper actions
        Task CloseAllWallpapersAsync();
        Task CloseWallpaperAsync(IMonitor monitor);
        Task CloseAllPreviewAsync();
        Task<Grpc_WpMetaData> GetWallpaperAsync(string folderPath);
        Task<bool> PreviewWallpaperAsync(IWpBasicData data, RuntimeType rtype, CancellationToken cancellationToken);
        Task<Grpc_RestartWallpaperResponse> RestartAllWallpapersAsync();
        Task<Grpc_SetWallpaperResponse> SetWallpaperAsync(IMonitor monitor, IWpBasicData metaData, RuntimeType rtype, CancellationToken cancellationToken);
        //Task UpdateWallpaperAsync(IMonitor monitor, IWpMetadata metaData, CancellationToken token);
        #endregion

        #region data
        Task<Grpc_WpBasicData?> CreateBasicDataAsync(string sourceFilePath, FileType ftype, CancellationToken token = default);
        ///// <summary>
        ///// 不包括 Using 文件
        ///// </summary>
        //Task<Grpc_WpRuntimeData?> CreateRuntimeDataAsync(string filePath, string folderPath, RuntimeType rtype, CancellationToken token = default);
        //Task<string?> CreateRuntimeDataUsingAsync(string folderPath, string wpEffectFilePathTemplate, string monitorContent, CancellationToken token = default);
        IWpMetadata GetWpMetadataByMonitorThu(string thumbnailPath);
        #endregion

        #region utils
        Task ChangeWallpaperLayoutFolrderPathAsync(string previousDir, string newDir);
        Task<Grpc_MonitorData?> GetRunMonitorByWallpaperAsync(string wpUid);
        Task ModifyPreviewAsync(string controlName, string propertyName, string val);
        Task SendMessageWallpaperAsync(IMonitor monitor, IWpRuntimeData metaData, IpcMessage msg);
        Task TakeScreenshotAsync(string monitorId, string savePath);
        Task<Grpc_WpBasicData?> UpdateBasicDataAsync(IWpBasicData data, CancellationToken token);
        #endregion
    }

    public class WallpaperBasicData {
        public string WallPaperUid { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
        public string WpEffectFilePathUsing { get; set; } = string.Empty;
        public string ThumbnailPath { get; set; } = string.Empty;
        public IMonitor? Monitor { get; set; }
        public FileType FType { get; set; }
    }
}
