using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using VirtualPaper.Models;
using VirtualPaper.Models.AccountPanel;
using VirtualPaper.Models.Net;
using VirtualPaper.Utils.Net.Interfaces;

namespace VirtualPaper.Utils.Net {
    public class HttpConnect : IHttpConnect {
        public async Task<NetMessage> LoginAsync(string email, string key) {
            NetMessage msg = new();

            try {
                var res = await _httpConnect.GetAsync($"/User/Login/{email}/{key}");
                var response = await res.Content.ReadAsStringAsync();
                msg = JsonSerializer.Deserialize<NetMessage>(response, _serializeOptions) ?? new();

                return msg;
            }
            catch (Exception ex) {
                Debug.WriteLine(ex);
                return msg;
            }
        }

        public async Task<NetMessage> RegisterAsync(string email, string userName, string securityCode, string key, string confirmKey) {
            NetMessage msgRes = new();

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
                msgRes = JsonSerializer.Deserialize<NetMessage>(response, _serializeOptions) ?? new();

                return msgRes;
            }
            catch (Exception ex) {
                Debug.WriteLine(ex);
                return msgRes;
            }
        }

        public async Task<NetMessage> RequestCodeAsync(string email) {
            NetMessage msgRes = new();

            try {
                var res = await _httpConnect.GetAsync($"/User/RequestCode/{email}");
                var response = await res.Content.ReadAsStringAsync();
                msgRes = JsonSerializer.Deserialize<NetMessage>(response, _serializeOptions) ?? new();

                return msgRes;
            }
            catch (Exception ex) {
                Debug.WriteLine(ex);
                return msgRes;
            }
        }

        public async Task<NetMessage> AutoLogin(string email, string password, string token) {
            NetMessage msg = new();

            try {
                var res = await _httpConnect.GetAsync($"/User/AutoLogin/{email}/{password}");
                var response = await res.Content.ReadAsStringAsync();
                msg = JsonSerializer.Deserialize<NetMessage>(response, _serializeOptions) ?? new();

                return msg;
            }
            catch (Exception ex) {
                Debug.WriteLine(ex);
                return msg;
            }
        }

        public async Task<NetMessage> UpdateUserInfoAsync(UserInfo userInfo) {
            NetMessage msg = new();

            try {
                using StringContent jsonContent = new(
                    JsonSerializer.Serialize(userInfo),
                    Encoding.UTF8,
                    "application/json");
                var res = await _httpConnect.PutAsync($"/User/UpdateUserInfo", jsonContent);
                var response = await res.Content.ReadAsStringAsync();
                msg = JsonSerializer.Deserialize<NetMessage>(response, _serializeOptions) ?? new();

                return msg;
            }
            catch (Exception ex) {
                Debug.WriteLine(ex);
                return msg;
            }
        }

        public async Task<NetMessage> GetPersonalCloudLibAsync(long uid, string token) {
            NetMessage msg = new();

            try {
                _httpConnect.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var res = await _httpConnect.GetAsync($"/Wallpaper/GetPersonalCloud/{uid}");
                var response = await res.Content.ReadAsByteArrayAsync();
                //var response = await res.Content.ReadAsStringAsync();
                msg = JsonSerializer.Deserialize<NetMessage>(response, _serializeOptions) ?? new();

                return msg;
            }
            catch (Exception ex) {
                Debug.WriteLine(ex);
                return msg;
            }
        }

        public async Task<NetMessage> GetPartitionsAsync(string token) {
            NetMessage msg = new();

            try {
                _httpConnect.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var res = await _httpConnect.GetAsync($"/Wallpaper/GetPartitions");
                var response = await res.Content.ReadAsStringAsync();
                msg = JsonSerializer.Deserialize<NetMessage>(response, _serializeOptions) ?? new();

                return msg;
            }
            catch (Exception ex) {
                Debug.WriteLine(ex);
                return msg;
            }
        }

        public async Task<NetMessage> UploadWallpaperAsync(WpBasicDataDto dto) {
            NetMessage msg = new();

            try {
                _httpConnect.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", App.Token);
                using StringContent jsonContent = new(
                    JsonSerializer.Serialize(dto, WpBasicDataDtoContext.Default.WpBasicDataDto),
                    Encoding.UTF8,
                    "application/json");
                var res = await _httpConnect.PostAsync($"/Wallpaper/UploadWallpaper", jsonContent);
                var response = await res.Content.ReadAsStringAsync();
                msg = JsonSerializer.Deserialize<NetMessage>(response, _serializeOptions) ?? new();

                return msg;
            }
            catch (Exception ex) {
                Debug.WriteLine(ex);
                return msg;
            }
        }

        public async Task<NetMessage> GetCloudLibAsync(string searchKey) {
            NetMessage msg = new();

            try {
                //_httpConnect.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var res = await _httpConnect.GetAsync($"/Wallpaper/GetCloud/{searchKey}");
                var response = await res.Content.ReadAsByteArrayAsync();
                //var response = await res.Content.ReadAsStringAsync();
                msg = JsonSerializer.Deserialize<NetMessage>(response, _serializeOptions) ?? new();

                return msg;
            }
            catch (Exception ex) {
                Debug.WriteLine(ex);
                return msg;
            }
        }

        public async Task<NetMessage> GetWpSourceDataByWpUidAsync(string wallpaperUid) {
            NetMessage msg = new();

            try {
                var res = await _httpConnect.GetAsync($"/Wallpaper/GetWpSourceData/{wallpaperUid}");
                var response = await res.Content.ReadAsByteArrayAsync();
                msg = JsonSerializer.Deserialize<NetMessage>(response, _serializeOptions) ?? new();

                return msg;
            }
            catch (Exception ex) {
                Debug.WriteLine(ex);
                return msg;
            }
        }

        public async Task<NetMessage> DeleteWallpaperAsync(string wallpaperUid) {
            NetMessage msg = new();

            try {
                UserWpTupleDto dto = new(App.User.Uid, wallpaperUid);
                using StringContent jsonContent = new(
                    JsonSerializer.Serialize(dto, UserWpTupleDtoContext.Default.UserWpTupleDto),
                    Encoding.UTF8,
                    "application/json");
                var res = await _httpConnect.PutAsync($"/Wallpaper/DeleteWallpaper", jsonContent);
                var response = await res.Content.ReadAsByteArrayAsync();
                msg = JsonSerializer.Deserialize<NetMessage>(response, _serializeOptions) ?? new();

                return msg;
            }
            catch (Exception ex) {
                Debug.WriteLine(ex);
                return msg;
            }
        }

        static readonly HttpClient _httpConnect = new() {
            BaseAddress = new Uri("http://127.0.0.1:5057"),
        };
        static readonly JsonSerializerOptions _serializeOptions = new() {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
    }
}
