using VirtualPaper.Common;
using VirtualPaper.Grpc.Service.Gallery;

namespace VirtualPaper.Grpc.Client.Interfaces {
    public interface IGalleryClient {
        Task<DeleteWallpaperResponse> DeleteWallpaper(string wallpaperUid);
        Task<CloudLibResponse> GetCloudLibAsync(string searchKey);
        Task<FilePropertyResponse> GetFilePropertyAsync(string filePath, FileType fType);
        Task<WpSourceDataResponse> GetWpSourceDataByWpUidAsync(string wallpaperUid);
    }
}
