using VirtualPaper.Models;
using VirtualPaper.Models.AccountPanel;
using VirtualPaper.Models.Net;

namespace VirtualPaper.Services.Interfaces {
    interface IAccountService {
        Task<NetMessage> GetPersonalCloudLibAsync();
        Task<NetMessage> GetPartitionsAsync();
        Task<NetMessage> LoginAsync(string email, string password);
        Task<NetMessage> RegisterAsync(string email, string username, string securityCode, string password, string confirmPassword);
        Task<NetMessage> SendEmailCodeAsync(string email);
        Task<NetMessage> UpdateUserInfoAsync(UserInfo userInfo);
        Task<NetMessage> UploadWallpaperAsync(WpBasicDataDto dto);
    }
}
