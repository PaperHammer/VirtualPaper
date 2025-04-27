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

        #region wallpaper actions
        Task CloseAllWallpapersAsync();
        Task CloseWallpaperAsync(IMonitor monitor);
        Task CloseAllPreviewAsync();
        Task<Grpc_WpMetaData> GetWallpaperAsync(string folderPath, string monitorContent, string rtype);
        Task<bool> AdjustWallpaperAsync(string monitorDeviceId, CancellationToken cancellationToken);
        Task<bool> PreviewWallpaperAsync(string monitorDeviceId, IWpBasicData data, RuntimeType rtype, CancellationToken cancellationToken);
        Task<Grpc_RestartWallpaperResponse> RestartAllWallpapersAsync();
        Task<Grpc_SetWallpaperResponse> SetWallpaperAsync(IMonitor monitor, IWpBasicData metaData, RuntimeType rtype, CancellationToken cancellationToken);
        #endregion

        #region data
        Task<Grpc_WpBasicData?> CreateBasicDataAsync(string sourceFilePath, FileType ftype, CancellationToken token = default);
        IWpMetadata GetWpMetadataByMonitorThu(string thumbnailPath);
        Task<Grpc_WpBasicData> CreateBasicDataInMemAsync(string filePath, FileType ftype, CancellationToken token);

        #endregion

        #region utils
        Task ChangeWallpaperLayoutFolrderPathAsync(string previousDir, string newDir);
        Task<Grpc_MonitorData?> GetRunMonitorByWallpaperAsync(string wpUid);
        Task SendMessageWallpaperAsync(IMonitor monitor, IWpRuntimeData metaData, IpcMessage msg);
        Task TakeScreenshotAsync(string monitorId, string savePath);
        Task<Grpc_WpBasicData?> UpdateBasicDataAsync(string folderPath, string folderName, string filePath, FileType ftype);
        #endregion
    }
}
