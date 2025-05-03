using VirtualPaper.Common;
using VirtualPaper.Models;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Services {
    class GalleryService : IGalleryService {
        public async Task<NetMessage> DeleteWallpaperAsync(string wallpaperUid) {
            if (App.User == null || string.IsNullOrEmpty(App.Token)) {
                return new() {
                    Code = 1001,
                    MsgKey = nameof(Constants.I18n.App_UserNotLogin),
                };
            }

            var data = await App.HttpConnect.DeleteWallpaperAsync(wallpaperUid);
            return data;
        }

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
