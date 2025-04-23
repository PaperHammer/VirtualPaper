using VirtualPaper.Grpc.Service.Account;

namespace VirtualPaper.Grpc.Client.Interfaces {
    public interface IAccountClient {
        Task<LoginResponse> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
        Task<RegisterResponse> RegisterAsync(string email, string username, string securityCode, string password, string confirmPassword, CancellationToken cancellationToken = default);
        Task<SendEmailCodeResponse> SendEmailCodeAsync(string email, CancellationToken cancellationToken = default);
        Task<bool> ResetPasswordAsync(string email, string code, string newPassword, CancellationToken cancellationToken = default);
        Task<bool> ChangePasswordAsync(string oldPassword, string newPassword, CancellationToken cancellationToken = default);
        Task<bool> LogoutAsync(CancellationToken cancellationToken = default);
        Task<bool> IsLoginAsync(CancellationToken cancellationToken = default);
    }
}
