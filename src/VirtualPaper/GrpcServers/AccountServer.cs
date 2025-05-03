using System.IO;
using System.Text.Json;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using VirtualPaper.Common;
using VirtualPaper.DataAssistor;
using VirtualPaper.Grpc.Service.Account;
using VirtualPaper.Grpc.Service.Models;
using VirtualPaper.Models.AccountPanel;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Net;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Utils;
using static VirtualPaper.Common.Utils.Archive.ZipUtil;

namespace VirtualPaper.GrpcServers {
    internal class AccountServer(
        IAccountService accountService) : Grpc_AccountService.Grpc_AccountServiceBase {
        public override Task<ChangePasswordResponse> ChangePassword(ChangePasswordRequest request, ServerCallContext context) {
            return base.ChangePassword(request, context);
        }

        public override Task<IsLoginResponse> IsLogin(Empty request, ServerCallContext context) {
            return base.IsLogin(request, context);
        }

        public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context) {
            var data = await _accountService.LoginAsync(request.Email, request.Password);
            var user = data.Data == null ? null : JsonSerializer.Deserialize(data.Data.ToString() ?? string.Empty, UserInfoContext.Default.UserInfo);
            var response = new LoginResponse {
                Success = data.Code == 1,
                Message = data.MsgKey,
                User = DataAssist.UserInfoToGrpc(user),
            };
            return response;
        }

        public override Task<LogoutResponse> Logout(Empty request, ServerCallContext context) {
            return base.Logout(request, context);
        }

        public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context) {
            var data = await _accountService.RegisterAsync(request.Email, request.Username, request.SecurityCode, request.Password, request.ConfirmPassword);
            var user = data.Data == null ? null : JsonSerializer.Deserialize(data.Data.ToString() ?? string.Empty, UserInfoContext.Default.UserInfo);
            var response = new RegisterResponse {
                Success = data.Code == 1,
                Message = data.MsgKey,
                User = DataAssist.UserInfoToGrpc(user),
            };
            return response;
        }

        public override Task<ResetPasswordResponse> ResetPassword(ResetPasswordRequest request, ServerCallContext context) {
            return base.ResetPassword(request, context);
        }

        public override async Task<SendEmailCodeResponse> SendEmailCode(SendEmailCodeRequest request, ServerCallContext context) {
            var data = await _accountService.SendEmailCodeAsync(request.Email);
            var response = new SendEmailCodeResponse {
                Success = data.Code == 1,
                Message = data.MsgKey,
            };
            return response;
        }

        public override async Task<UpdateUserInfoResponse> UpdateUserInfo(UpdateUserInfoRequest request, ServerCallContext context) {
            var user = DataAssist.FromGrpcUserInfo(request.User);
            if (user == null) {
                return new UpdateUserInfoResponse {
                    Success = false,
                    Message = nameof(Constants.I18n.Server_CannotAccess),
                };
            }

            var data = await _accountService.UpdateUserInfoAsync(user);
            user = data.Data == null ? null : JsonSerializer.Deserialize(data.Data.ToString() ?? string.Empty, UserInfoContext.Default.UserInfo);
            var response = new UpdateUserInfoResponse {
                Success = data.Code == 1,
                Message = data.MsgKey,
                User = DataAssist.UserInfoToGrpc(user),
            };
            return response;
        }

        public override async Task<PersonalCloudLibResponse> GetPersonalCloudLib(Empty request, ServerCallContext context) {
            var data = await _accountService.GetPersonalCloudLibAsync();
            var wallpapers = data.Data == null ? null : JsonSerializer.Deserialize(
                data.Data.ToString(),
                WpBasicDataDtoContext.Default.ListWpBasicDataDto);
            var tasks = wallpapers?.Select(async wp => await DataAssist.ToGrpcWpBasciDataThuAsync(wp));
            var grpcWallpapers = tasks == null ? [] : await Task.WhenAll(tasks);
            var response = new PersonalCloudLibResponse {
                Success = data.Code == 1,
                Message = data.MsgKey,
                Wallpapers = { grpcWallpapers },
            };
            return response;
        }

        public override async Task<PartitionsResponse> GetPartitions(Empty request, ServerCallContext context) {
            var data = await _accountService.GetPartitionsAsync();
            var partitions = data.Data == null ? [] : JsonSerializer.Deserialize<List<string>>(data.Data.ToString() ?? string.Empty);
            var response = new PartitionsResponse {
                Success = data.Code == 1,
                Message = data.MsgKey,
                Partitions = { partitions },
            };
            return response;
        }

        public override async Task<UploadWallpaperResponse> UploadWallpaper(UplaodWallpaperRequest request, ServerCallContext context) {
            if (App.User == null || string.IsNullOrEmpty(App.Token)) {
                return new() {
                    Success = false,
                    Message = nameof(Constants.I18n.App_UserNotLogin),
                };
            }

            var dto = await ToWpBasciDataDtoAsync(request.WpBasicData);
            if (dto == null) {
                return new() {
                    Success = false,
                    Message = nameof(Constants.I18n.Text_FileNotAccess),
                };
            }
            var data = await _accountService.UploadWallpaperAsync(dto);
            var response = new UploadWallpaperResponse {
                Success = data.Code == 1,
                Message = data.MsgKey
            };
            return response;
        }

        private static async Task<WpBasicDataDto?> ToWpBasciDataDtoAsync(Grpc_WpBasicData wpBasicData) {
            try {
                WpBasicDataDto dto = new() {
                    Image = await File.ReadAllBytesAsync(wpBasicData.FilePath),
                    ThuImage = await File.ReadAllBytesAsync(wpBasicData.ThumbnailPath),
                    Uid = wpBasicData.WallpaperUid,
                    UserUid = App.User.Uid,
                    Title = wpBasicData.Title,
                    Type = (FileType)wpBasicData.FType,
                    FileExtension = wpBasicData.FileExtension,
                    PublishDate = wpBasicData.PublishDate,
                    Publisher = App.User.Name,
                    Partition = wpBasicData.Partition,
                    Tags = wpBasicData.Tags,
                    Description = wpBasicData.Desc,
                };
                return dto;
            }
            catch (Exception ex) { }

            return null;
        }        

        private readonly IAccountService _accountService = accountService;
        //static readonly JsonSerializerOptions _serializeOptions = new() {
        //    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        //    WriteIndented = true,
        //};
    }
}
