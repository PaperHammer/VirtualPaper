using VirtualPaper.Models;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Services {
    class GalleryService : IGalleryService {
        public async Task<NetMessage> GetCloudLibAsync(string searchKey) {
            var data = await App.HttpConnect.GetCloudLibAsync(searchKey);
            return data;
        }

        public async Task<NetMessage> GetWpSourceDataByWpUidAsync(string wallpaperUid) {
            var data = await App.HttpConnect.GetWpSourceDataByWpUidAsync(wallpaperUid);
            return data;
        }
    }
}
