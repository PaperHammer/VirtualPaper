using System.Net;
using System.Text.Json;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Security;
using VirtualPaper.Models;
using VirtualPaper.Models.AccountPanel;
using VirtualPaper.Models.Net;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Services {
    class AccountService : IAccountService {
        public async Task<NetMessage> GetPersonalCloudLibAsync() {
            if (App.User == null || string.IsNullOrEmpty(App.Token)) {
                return new() {
                    Code = 1001,
                    MsgKey = nameof(Constants.I18n.App_UserNotLogin),
                };
            }

            var data = await App.HttpConnect.GetPersonalCloudLibAsync();
            return data;
        }

        public async Task<NetMessage> GetPartitionsAsync() {
            if (App.User == null || string.IsNullOrEmpty(App.Token)) {
                return new() {
                    Code = 1001,
                    MsgKey = nameof(Constants.I18n.App_UserNotLogin),
                };
            }

            var data = await App.HttpConnect.GetPartitionsAsync();
            return data;
        }

        public async Task<NetMessage> LoginAsync(string email, string password) {
            string key = AESHelper.EncryptStringToBytes_Aes(password);
            var data = await App.HttpConnect.LoginAsync(email, WebUtility.UrlEncode(key));

            if (data.Code == 1) {
                App.User = data.Data == null ? null : JsonSerializer.Deserialize(data.Data.ToString() ?? string.Empty, UserInfoContext.Default.UserInfo);
                App.Token = data.Token;
            }

            return data;
        }

        public async Task<NetMessage> RegisterAsync(string email, string username, string securityCode, string password, string confirmPassword) {
            string key = AESHelper.EncryptStringToBytes_Aes(password);
            string confirmKey = AESHelper.EncryptStringToBytes_Aes(confirmPassword);
            var data = await App.HttpConnect.RegisterAsync(email, username, securityCode, WebUtility.UrlEncode(key), WebUtility.UrlEncode(confirmKey));

            if (data.Code == 1) {
                App.User = data.Data == null ? null : JsonSerializer.Deserialize(data.Data.ToString() ?? string.Empty, UserInfoContext.Default.UserInfo);
                App.Token = data.Token;
            }

            return data;
        }

        public async Task<NetMessage> SendEmailCodeAsync(string email) {
            var data = await App.HttpConnect.RequestCodeAsync(email) ?? new();
            return data;
        }

        public async Task<NetMessage> UpdateUserInfoAsync(UserInfo userInfo) {
            var data = await App.HttpConnect.UpdateUserInfoAsync(userInfo);
            return data;
        }

        public async Task<NetMessage> UploadWallpaperAsync(WpBasicDataDto dto) {
            dto.AppName = Constants.CoreField.AppName;
            dto.AppVersion = App.UserSettings.Settings.AppVersion;
            dto.FileVersion = Constants.CoreField.FileVersion;
            var data = await App.HttpConnect.UploadWallpaperAsync(dto);
            return data;
        }
    }
}
