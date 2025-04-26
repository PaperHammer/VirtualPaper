using GrpcDotNetNamedPipes;
using VirtualPaper.Common;
using VirtualPaper.DataAssistor;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.Account;
using VirtualPaper.Models.AccountPanel;

namespace VirtualPaper.Grpc.Client {
    public class AccountClient : IAccountClient {
        public AccountClient() {
            _client = new Grpc_AccountService.Grpc_AccountServiceClient(new NamedPipeChannel(".", Constants.CoreField.GrpcPipeServerName));
        }

        public Task<bool> ChangePasswordAsync(string oldPassword, string newPassword, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public Task<bool> IsLoginAsync(CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public async Task<LoginResponse> LoginAsync(string email, string password, CancellationToken cancellationToken = default) {
            LoginRequest request = new() {
                Email = email,
                Password = password,
            };
            var res = await _client.LoginAsync(request, cancellationToken: cancellationToken);
            return res;
        }

        public Task<bool> LogoutAsync(CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public async Task<RegisterResponse> RegisterAsync(string email, string username, string securityCode, string password, string confirmPassword, CancellationToken cancellationToken = default) {
            RegisterRequest request = new() {
                Email = email,
                Username = username,
                SecurityCode = securityCode,
                Password = password,
                ConfirmPassword = confirmPassword,
            };
            var res = await _client.RegisterAsync(request, cancellationToken: cancellationToken);
            return res;
        }

        public Task<bool> ResetPasswordAsync(string email, string code, string newPassword, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        public async Task<SendEmailCodeResponse> SendEmailCodeAsync(string email, CancellationToken cancellationToken = default) {
            SendEmailCodeRequest request = new() {
                Email = email,
            };
            var res = await _client.SendEmailCodeAsync(request, cancellationToken: cancellationToken);
            return res;
        }

        public async Task<UpdateUserInfoResponse> UpdateUserInfoAsync(UserInfo newUserInfo, CancellationToken cancellationToken = default) {
            UpdateUserInfoRequest request = new() {
                User = DataAssist.UserInfoTpGrpc(newUserInfo),
            };
            var res = await _client.UpdateUserInfoAsync(request, cancellationToken: cancellationToken);
            return res;
        }

        private readonly Grpc_AccountService.Grpc_AccountServiceClient _client;
    }
}
