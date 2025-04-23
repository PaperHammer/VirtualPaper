using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VirtualPaper.Models;
using VirtualPaper.Utils.Net.Interfaces;

namespace VirtualPaper.Utils.Net {
    public class HttpConnect : IHttpConnect {
        public async Task<NetMessage> LoginAsync(string email, string key) {
            NetMessage msg;

            try {
                var res = await _httpConnect.GetAsync($"/User/Login/{email}/{key}");
                var response = await res.Content.ReadAsStringAsync();
                msg = JsonSerializer.Deserialize<NetMessage>(response, _serializeOptions);

                return msg;
            }
            catch (Exception ex) {
                Debug.WriteLine(ex);
                return null;
            }
        }

        public async Task<NetMessage> RegisterAsync(string email, string userName, string securityCode, string key, string confirmKey) {
            NetMessage msgRes;

            try {
                using StringContent jsonContent = new(
                    JsonSerializer.Serialize(new {
                        email,
                        userName,
                        securityCode,
                        key,
                        confirmKey,
                    }),
                    Encoding.UTF8,
                    "application/json");
                var res = await _httpConnect.PostAsync($"/User/Register", jsonContent);
                var response = await res.Content.ReadAsStringAsync();
                msgRes = JsonSerializer.Deserialize<NetMessage>(response, _serializeOptions);
            }
            catch (Exception) {
                return null;
            }

            return msgRes;
        }

        public async Task<NetMessage> RequestCodeAsync(string email) {
            NetMessage msgRes;

            try {
                var res = await _httpConnect.GetAsync($"/User/RequestCode/{email}");
                var response = await res.Content.ReadAsStringAsync();
                msgRes = JsonSerializer.Deserialize<NetMessage>(response, _serializeOptions);
            }
            catch (Exception) {
                return null;
            }

            return msgRes;
        }

        public async Task<NetMessage> AutoLogin(string email, string password, string token) {
            NetMessage msg;

            try {
                _httpConnect.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var res = await _httpConnect.GetAsync($"/User/AutoLogin/{email}/{password}");
                var response = await res.Content.ReadAsStringAsync();
                msg = JsonSerializer.Deserialize<NetMessage>(response, _serializeOptions);

                return msg;
            }
            catch (Exception ex) {
                Debug.WriteLine(ex);
                return null;
            }
        }

        readonly HttpClient _httpConnect = new() {
            BaseAddress = new Uri("http://127.0.0.1:5057")
        };
        readonly JsonSerializerOptions _serializeOptions = new() {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }
}
