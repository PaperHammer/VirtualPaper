using System.Text.Json;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using VirtualPaper.Common;
using VirtualPaper.DataAssistor;
using VirtualPaper.Grpc.Service.Account;
using VirtualPaper.Models.AccountPanel;
using VirtualPaper.Services.Interfaces;

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
                User = DataAssist.UserInfoTpGrpc(user),
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
                User = DataAssist.UserInfoTpGrpc(user),
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
                User = DataAssist.UserInfoTpGrpc(user),
            };
            return response;
        }

        private readonly IAccountService _accountService = accountService;
    }
}
