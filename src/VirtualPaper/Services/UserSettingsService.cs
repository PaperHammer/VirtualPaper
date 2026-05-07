using System.Reflection;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Storage.Adapter;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.DraftPanel;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Utils;

namespace VirtualPaper.Services {
    public class UserSettingsService : IUserSettingsService {
        public ISettings Settings { get; private set; }
        public List<IApplicationRules> AppRules { get; private set; } = [];
        public List<IWallpaperLayout> WallpaperLayouts { get; private set; } = [];
        public List<IRecentUsed> RecentUseds { get; private set; } = [];

        public UserSettingsService(
            IMonitorManager moitorManager,
            IJsonSaver jsonSaver) {
            _jsonSaver = jsonSaver;

            Load<ISettings>();
            Load<List<IApplicationRules>>();
            Load<List<IWallpaperLayout>>();
            Load<List<IRecentUsed>>();

            Settings.SelectedMonitor = Settings.SelectedMonitor != null ?
                moitorManager.Monitors.FirstOrDefault(x => x.Equals(Settings.SelectedMonitor)) ?? moitorManager.PrimaryMonitor : moitorManager.PrimaryMonitor;

            //previous installed appversion is different from current instance..    
            if (!Settings.AppVersion.Equals(Assembly.GetExecutingAssembly().GetName().Version.ToString(), StringComparison.OrdinalIgnoreCase)) {
                Settings.FileVersion = Constants.CoreField.FileVersion;
                Settings.AppName = Constants.CoreField.AppName;
                Settings.AppVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                Settings.IsUpdated = true;
            }

            var lang = SupportedLanguages.GetLanguage(Settings.Language);
            if (lang.Codes.FirstOrDefault(x => x == Settings.Language) == null) {
                Settings.Language = lang.Codes[0];
            }

            try {
                _ = WindowsAutoStart.SetAutoStart(Settings.IsAutoStart);
            }
            catch (Exception e) {
                ArcLog.GetLogger<UserSettingsService>().Error(e);
            }
        }

        public void Load<T>() {
            if (typeof(T) == typeof(ISettings)) {
                try {
                    Settings = _jsonSaver.Load<Settings>(_settingsPath, SettingsContext.Default);
                }
                catch (Exception e) {
                    ArcLog.GetLogger<UserSettingsService>().Error(e);
                    Settings = new Settings();
                    Save<ISettings>();
                }
            }
            else if (typeof(T) == typeof(List<IApplicationRules>)) {
                try {
                    AppRules = new List<IApplicationRules>(_jsonSaver.Load<List<ApplicationRules>>(_appRulesPath, ApplicationRulesContext.Default));
                }
                catch (Exception e) {
                    ArcLog.GetLogger<UserSettingsService>().Error(e.ToString());
                    AppRules =
                    [
                        new ApplicationRules(Constants.CoreField.AppName, AppWpRunRulesEnum.KeepRun)
                    ];
                    Save<List<IApplicationRules>>();
                }
            }
            else if (typeof(T) == typeof(List<IWallpaperLayout>)) {
                try {
                    WallpaperLayouts = new List<IWallpaperLayout>(_jsonSaver.Load<List<WallpaperLayout>>(_wallpaperLayoutPath, WallpaperLayoutContext.Default));
                }
                catch (Exception e) {
                    ArcLog.GetLogger<UserSettingsService>().Error(e.ToString());
                    WallpaperLayouts = [];
                    Save<List<IWallpaperLayout>>();
                }
            }
            else if (typeof(T) == typeof(List<IRecentUsed>)) {
                try {
                    RecentUseds = new List<IRecentUsed>(_jsonSaver.Load<List<RecentUsed>>(_recentUsedPath, RecentUsedContext.Default));
                }
                catch (Exception e) {
                    ArcLog.GetLogger<UserSettingsService>().Error(e.ToString());
                    RecentUseds = [];
                    Save<List<IRecentUsed>>();
                }
            }
            else {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        public void Save<T>() {
            if (typeof(T) == typeof(ISettings)) {
                _jsonSaver.Save(_settingsPath, Settings, SettingsContext.Default);
            }
            else if (typeof(T) == typeof(List<IApplicationRules>)) {
                _jsonSaver.Save(_appRulesPath, AppRules, ApplicationRulesContext.Default);
            }
            else if (typeof(T) == typeof(List<IWallpaperLayout>)) {
                _jsonSaver.Save(_wallpaperLayoutPath, WallpaperLayouts, WallpaperLayoutContext.Default);
            }
            else if (typeof(T) == typeof(List<IRecentUsed>)) {
                _jsonSaver.Save(_recentUsedPath, RecentUseds, RecentUsedContext.Default);
            }
            else {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        private readonly string _settingsPath = Constants.CommonPaths.UserSettingsPath;
        private readonly string _appRulesPath = Constants.CommonPaths.AppRulesPath;
        private readonly string _wallpaperLayoutPath = Constants.CommonPaths.WallpaperLayoutPath;
        private readonly string _recentUsedPath = Constants.CommonPaths.RecentUsedPath;
        private readonly IJsonSaver _jsonSaver;
    }
}
