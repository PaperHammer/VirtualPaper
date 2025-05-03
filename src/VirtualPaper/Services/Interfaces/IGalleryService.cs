using VirtualPaper.Models;

namespace VirtualPaper.Services.Interfaces {
    interface IGalleryService {
        Task<NetMessage> DeleteWallpaperAsync(string wallpaperUid);
        Task<NetMessage> GetCloudLibAsync(string searchKey);
        Task<NetMessage> GetWpSourceDataByWpUidAsync(string wallpaperUid);
    }
}
