using VirtualPaper.Common;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Grpc.Service.CommonModels;
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
        Task<Grpc_WpMetaData> GetWallpaperAsync(string folderPath, string monitorContent, string rtype);
        Task<string> GetPlayerStartArgsAsync(IWpBasicData data, RuntimeType rtype, CancellationToken token);
        Task<string> GetPlayerStartArgsByMonitorIdAsync(string monitorId, CancellationToken token);
        Task<Grpc_RestartWallpaperResponse> RestartAllWallpapersAsync();
        Task<Grpc_SetWallpaperResponse> SetWallpaperAsync(IMonitor monitor, IWpBasicData metaData, RuntimeType rtype, CancellationToken token);
        #endregion

        #region data
        Task<Grpc_WpBasicData?> CreateBasicDataAsync(string sourceFilePath, FileType ftype, CancellationToken token, bool isTemp = false);
        IWpMetadata GetWpMetadataByMonitorThu(string thumbnailPath);
        Task<Grpc_WpBasicData> CreateBasicDataInMemAsync(string filePath, FileType ftype, CancellationToken token);

        #endregion

        #region utils
        Task ChangeWallpaperLayoutFolrderPathAsync(string previousDir, string newDir);
        Task<Grpc_MonitorData?> GetRunMonitorByWallpaperAsync(string wpUid);
        Task SendMessageWallpaperAsync(string deviceId, IpcMessage msg);
        Task TakeScreenshotAsync(string monitorId, string savePath);
        Task<Grpc_WpBasicData?> UpdateBasicDataAsync(string folderPath, string folderName, string filePath, FileType ftype);
        #endregion
    }
}
