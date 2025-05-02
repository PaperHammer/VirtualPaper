using System.Text.Json;
using Google.Protobuf;
using Grpc.Core;
using VirtualPaper.DataAssistor;
using VirtualPaper.Grpc.Service.Gallery;
using VirtualPaper.Models.Net;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Utils;

namespace VirtualPaper.GrpcServers {
    class GalleryServer(
        IGalleryService galleryService) : Grpc_GalleryService.Grpc_GalleryServiceBase {
        public override Task<FilePropertyResponse> GetFileProperty(FilePropertyRequest request, ServerCallContext context) {
            var property = WallpaperUtil.GetWpProperty(request.FilePath, (Common.FileType)request.FType);
            var response = new FilePropertyResponse {
                Resolution = property?.Resolution,
                AspectRatio = property?.AspectRatio,
                FileSize = property?.FileSize,
            };
            return Task.FromResult(response);
        }

        public override async Task<CloudLibResponse> GetCloudLib(CloudLibRequest request, ServerCallContext context) {
            var data = await _galleryService.GetCloudLibAsync(request.SearchKey);
            var wallpapers = data.Data == null ? null : JsonSerializer.Deserialize(
                data.Data.ToString(),
                WpBasicDataDtoContext.Default.ListWpBasicDataDto);
            var tasks = wallpapers?.Select(async wp => await DataAssist.ToGrpcWpBasciDataThuAsync(wp));
            var grpcWallpapers = tasks == null ? [] : await Task.WhenAll(tasks);
            var response = new CloudLibResponse {
                Success = data.Code == 1,
                Message = data.MsgKey,
                Wallpapers = { grpcWallpapers },
            };
            return response;
        }

        public override async Task<WpSourceDataResponse> GetWpSourceDataByWpUid(WpSourceDataRequest request, ServerCallContext context) {
            var data = await _galleryService.GetWpSourceDataByWpUidAsync(request.WallpaperUid);
            byte[] decodedBytes = data.Data == null ? [] : Convert.FromBase64String(data.Data.ToString());
            ByteString byteStringData = ByteString.CopyFrom(decodedBytes);

            var response = new WpSourceDataResponse {
                Success = data.Code == 1,
                Message = data.MsgKey,
                SourceData = new() {
                    Data = byteStringData,
                },
            };
            return response;
        }

        private readonly IGalleryService _galleryService = galleryService;
    }
}
