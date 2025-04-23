using VirtualPaper.Models;

namespace VirtualPaper.Utils.Net.Interfaces {
    interface IHttpConnect {
        Task<NetMessage> AutoLogin(string email, string password, string token);
        Task<NetMessage> LoginAsync(string email, string key);
        Task<NetMessage> RegisterAsync(string email, string userName, string code, string key, string confirmKey);
        Task<NetMessage> RequestCodeAsync(string email);
    }
}
