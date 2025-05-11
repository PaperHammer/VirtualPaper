using Google.Protobuf.WellKnownTypes;
using GrpcDotNetNamedPipes;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.DataAssistor;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.Models;
using VirtualPaper.Grpc.Service.UserSettings;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.DraftPanel;

namespace VirtualPaper.Grpc.Client {
    public class UserSettingsClient : IUserSettingsClient {
        public ISettings Settings { get; private set; } = new Settings();
        public List<IApplicationRules> AppRules { get; private set; } = [];
        public List<IWallpaperLayout> WallpaperLayouts { get; private set; } = [];
        public List<IRecentUsed> RecentUseds { get; private set; } = [];

        public UserSettingsClient() {
            _client = new Grpc_UserSettingsService.Grpc_UserSettingsServiceClient(new NamedPipeChannel(".", Constants.CoreField.GrpcPipeServerName));

            Task.Run(async () => {
                await LoadAsync<ISettings>().ConfigureAwait(false);
                await LoadAsync<List<IApplicationRules>>().ConfigureAwait(false);
                await LoadAsync<List<IWallpaperLayout>>().ConfigureAwait(false);
                await LoadAsync<List<IRecentUsed>>().ConfigureAwait(false);
            }).Wait();
        }

        public void Load<T>() {
            if (typeof(T) == typeof(ISettings)) {
                Settings = GetSettings();
            }
            else if (typeof(T) == typeof(List<IApplicationRules>)) {
                AppRules = GetAppRulesSettings();
            }
            else if (typeof(T) == typeof(List<IWallpaperLayout>)) {
                WallpaperLayouts = GetWallpaperLayouts();
            }
            else {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        public async Task LoadAsync<T>() {
            if (typeof(T) == typeof(ISettings)) {
                Settings = await GetSettingsAsync().ConfigureAwait(false);
            }
            else if (typeof(T) == typeof(List<IApplicationRules>)) {
                AppRules = await GetAppRulesSettingsAsync().ConfigureAwait(false);
            }
            else if (typeof(T) == typeof(List<IWallpaperLayout>)) {
                WallpaperLayouts = await GetWallpaperLayoutsAsync().ConfigureAwait(false);
            }
            else if (typeof(T) == typeof(List<IRecentUsed>)) {
                RecentUseds = await GetRecentUsedsAsync().ConfigureAwait(false);
            }
            else {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        public void Save<T>() {
            if (typeof(T) == typeof(ISettings)) {
                SetSettings();
            }
            else if (typeof(T) == typeof(List<IApplicationRules>)) {
                SetAppRulesSettings();
            }
            else if (typeof(T) == typeof(List<IRecentUsed>)) {
                SetRecentUseds();
            }
            else {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        public async Task SaveAsync<T>() {
            if (typeof(T) == typeof(ISettings)) {
                await SetSettingsAsync().ConfigureAwait(false);
            }
            else if (typeof(T) == typeof(List<IApplicationRules>)) {
                await SetAppRulesSettingsAsync().ConfigureAwait(false);
            }
            else {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        private void SetSettings() {
            _ = _client.SetSettings(DataAssist.SettingsToGrpc(Settings));
        }

        private async Task SetSettingsAsync() {
            _ = await _client.SetSettingsAsync(DataAssist.SettingsToGrpc(Settings));
        }

        private Settings GetSettings() {
            var resp = _client.GetSettings(new Empty());
            return DataAssist.GrpcToSettings(resp);
        }

        private async Task<ISettings> GetSettingsAsync() {
            var resp = await _client.GetSettingsAsync(new Empty());
            return DataAssist.GrpcToSettings(resp);
        }

        private List<IApplicationRules> GetAppRulesSettings() {
            var appRules = new List<IApplicationRules>();
            var resp = _client.GetAppRulesSettings(new Empty());
            foreach (var item in resp.AppRules) {
                appRules.Add(new ApplicationRules(item.AppName, (AppWpRunRulesEnum)((int)item.Rule)));
            }
            return appRules;
        }

        private async Task<List<IApplicationRules>> GetAppRulesSettingsAsync() {
            var appRules = new List<IApplicationRules>();
            var resp = await _client.GetAppRulesSettingsAsync(new Empty());
            foreach (var item in resp.AppRules) {
                appRules.Add(new ApplicationRules(item.AppName, (AppWpRunRulesEnum)((int)item.Rule)));
            }
            return appRules;
        }

        private List<IWallpaperLayout> GetWallpaperLayouts() {
            var wpLayouts = new List<IWallpaperLayout>();
            var resp = _client.GetWallpaperLayouts(new Empty());
            foreach (var item in resp.WallpaperLayouts) {
                wpLayouts.Add(new WallpaperLayout(item.FolderPath, item.MonitorDeviceId, item.MonitorContent, item.RType));
            }
            return wpLayouts;
        }

        private async Task<List<IWallpaperLayout>> GetWallpaperLayoutsAsync() {
            var wpLayouts = new List<IWallpaperLayout>();
            var resp = await _client.GetWallpaperLayoutsAsync(new Empty());
            foreach (var item in resp.WallpaperLayouts) {
                wpLayouts.Add(new WallpaperLayout( item.FolderPath, item.MonitorDeviceId,item.MonitorContent, item.RType));
            }
            return wpLayouts;
        }

        private async Task<List<IRecentUsed>> GetRecentUsedsAsync() {
            var wpLayouts = new List<IRecentUsed>();
            var resp = await _client.GetRecentUsedsAsync(new Empty());
            foreach (var item in resp.RecentUseds) {
                wpLayouts.Add(new RecentUsed((FileType)item.FType, item.FileName, item.FilePath, item.DateTime));
            }
            return wpLayouts;
        }

        private void SetAppRulesSettings() {
            var tmp = new Grpc_AppRulesSettings();
            foreach (var item in AppRules) {
                tmp.AppRules.Add(new Grpc_AppRulesData {
                    AppName = item.AppName,
                    Rule = (Grpc_AppRulesEnum)(int)item.Rule
                });
            }
            _ = _client.SetAppRulesSettings(tmp);
        }

        private async Task SetAppRulesSettingsAsync() {
            var tmp = new Grpc_AppRulesSettings();
            foreach (var item in AppRules) {
                tmp.AppRules.Add(new Grpc_AppRulesData {
                    AppName = item.AppName,
                    Rule = (Grpc_AppRulesEnum)(int)item.Rule
                });
            }
            _ = await _client.SetAppRulesSettingsAsync(tmp);
        }
        
        private void SetRecentUseds() {
            var tmp = new Grpc_RecentUseds();
            foreach (var item in RecentUseds) {
                tmp.RecentUseds.Add(new Grpc_RecentUsed {
                    FType = (Grpc_FileType)item.Type,
                    FileName = item.FileName,
                    FilePath = item.FilePath,
                    DateTime = item.DateTime
                });
            }
            _ = _client.SetRecentUseds(tmp);
        }
        
        private async Task SetRecentUsedsAsync() {
            var tmp = new Grpc_RecentUseds();
            foreach (var item in RecentUseds) {
                tmp.RecentUseds.Add(new Grpc_RecentUsed {
                    FType = (Grpc_FileType)item.Type,
                    FileName = item.FileName,
                    FilePath = item.FilePath,
                    DateTime = item.DateTime
                });
            }
            _ = await _client.SetRecentUsedsAsync(tmp);
        }

        public async Task UpdateRecetUsedAsync(string filePath) {
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var existingItem = RecentUseds.FirstOrDefault(r => r.FilePath == filePath);
            if (existingItem != null) {
                existingItem.DateTime = now;
            }
            else {
                var recentUsed = new RecentUsed(
                    FileFilter.GetRuntimeFileType(Path.GetExtension(filePath)),
                    Path.GetFileName(filePath),
                    filePath,
                    now
                );
                RecentUseds.Add(recentUsed);
            }
            await SetRecentUsedsAsync();
        }

        private readonly Grpc_UserSettingsService.Grpc_UserSettingsServiceClient _client;
    }
}
