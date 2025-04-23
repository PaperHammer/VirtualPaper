using System.Net;
using VirtualPaper.Common.Utils.Security;
using VirtualPaper.Models;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Services {
    class AccountService : IAccountService {
        public async Task<NetMessage> LoginAsync(string email, string password) {
            string key = AESHelper.EncryptStringToBytes_Aes(password);
            var data = await App.HttpConnect.LoginAsync(email, WebUtility.UrlEncode(key)) ?? new();
            return data;
        }

        public async Task<NetMessage> RegisterAsync(string email, string username, string securityCode, string password, string confirmPassword) {
            string key = AESHelper.EncryptStringToBytes_Aes(password);
            string confirmKey = AESHelper.EncryptStringToBytes_Aes(confirmPassword);
            var data = await App.HttpConnect.RegisterAsync(email, username, securityCode, WebUtility.UrlEncode(key), WebUtility.UrlEncode(confirmKey)) ?? new();
            return data;
        }

        public async Task<NetMessage> SendEmailCodeAsync(string email) {
            var data = await App.HttpConnect.RequestCodeAsync(email) ?? new();
            return data;
        }
    }
}
