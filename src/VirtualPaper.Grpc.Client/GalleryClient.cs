using GrpcDotNetNamedPipes;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.Gallery;
using VirtualPaper.Grpc.Service.Models;

namespace VirtualPaper.Grpc.Client {
    public class GalleryClient : IGalleryClient {
        public GalleryClient() {
            _client = new Grpc_GalleryService.Grpc_GalleryServiceClient(new NamedPipeChannel(".", Constants.CoreField.GrpcPipeServerName));
        }

        public async Task<CloudLibResponse> GetCloudLibAsync(string searchKey) {
            CloudLibRequest request = new() {
                SearchKey = searchKey,
            };
            var res = await _client.GetCloudLibAsync(request);
            return res;
        }

        public async Task<WpSourceDataResponse> GetWpSourceDataByWpUidAsync(string wallpaperUid) {
            WpSourceDataRequest request = new() {
                WallpaperUid = wallpaperUid,
            };
            var res = await _client.GetWpSourceDataByWpUidAsync(request);
            return res;
        }

        public async Task<FilePropertyResponse> GetFilePropertyAsync(string filePath, FileType fType) {
            FilePropertyRequest request = new() {
                FilePath = filePath,
                FType = (Grpc_FileType)fType,
            };
            var res = await _client.GetFilePropertyAsync(request);
            return res;
        }

        public async Task<DeleteWallpaperResponse> DeleteWallpaper(string wallpaperUid) {
            DeleteWallpaperRequest request = new() {
                WallpaperUid = wallpaperUid,
            };
            var res = await _client.DeleteWallpaperAsync(request);
            return res;
        }

        private readonly Grpc_GalleryService.Grpc_GalleryServiceClient _client;

    }
}
