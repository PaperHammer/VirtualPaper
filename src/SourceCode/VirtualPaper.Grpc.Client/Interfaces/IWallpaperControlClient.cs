using System.Collections.ObjectModel;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Grpc.Service.WallpaperControl;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.WallpaperMetaData;

namespace VirtualPaper.Grpc.Client.Interfaces
{
    public interface IWallpaperControlClient : IDisposable
    {
        ReadOnlyCollection<WallpaperBasicData> Wallpapers { get; }
        string BaseDirectory { get; }
        Version AssemblyVersion { get; }

        Task<WpMetaData> GetWallpaperAsync(string folderPath);

        Task<SetWallpaperResponse> SetWallpaperAsync(IMetaData metaData, IMonitor monitor, CancellationToken cancellationToken);

        Task CloseAllWallpapersAsync();
        Task CloseWallpaperAsync(IMonitor monitor);
        
        Task<RestartWallpaperResponse> RestartAllWallpaperAsync();

        Task<WpMetaData?> CreateWallpaperAsync(string folderPath, string filePath, Common.WallpaperType type, CancellationToken token = default);

        //Task SendMessageWallpaperAsync(IMetaData metaData, IpcMessage msg);
        Task SendMessageWallpaperAsync(IMonitor monitor, IMetaData metaData, IpcMessage msg);

        Task PreviewWallpaperAsync(IMetaData metaData, bool isLibraryPreview);
        Task TakeScreenshotAsync(string monitorId, string savePath);
        Task ModifyPreviewAsync(string controlName, string propertyName, string val);

        Task ChangeWallpaperLayoutFolrderPath(string previousDir, string newDir);

        event EventHandler? WallpaperChanged;
        event EventHandler<Exception>? WallpaperError;
    }

    public class WallpaperBasicData
    {
        public string VirtualPaperUid { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
        public string WpCustomizePathUsing { get; set; } = string.Empty;
        public string ThumbnailPath { get; set; } = string.Empty;
        public IMonitor? Monitor { get; set; }
        public Common.WallpaperType Tyep { get; set; }
    }
}
