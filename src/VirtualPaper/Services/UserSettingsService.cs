using System.Reflection;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Utils;

namespace VirtualPaper.Services {
    public class UserSettingsService : IUserSettingsService {
        public ISettings Settings { get; private set; }
        public List<IApplicationRules> AppRules { get; private set; } = [];
        public List<IWallpaperLayout> WallpaperLayouts { get; private set; } = [];

        public UserSettingsService(
            IMonitorManager moitorManager) {
            Load<ISettings>();
            Load<List<IApplicationRules>>();
            Load<List<IWallpaperLayout>>();

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
                App.Log.Error(e);
            }
        }

        public void Load<T>() {
            if (typeof(T) == typeof(ISettings)) {
                try {
                    Settings = JsonSaver.Load<Settings>(_settingsPath, SettingsContext.Default);
                }
                catch (Exception e) {
                    App.Log.Error(e);
                    Settings = new Settings();
                    Save<ISettings>();
                }
            }
            else if (typeof(T) == typeof(List<IApplicationRules>)) {
                try {
                    AppRules = new List<IApplicationRules>(JsonSaver.Load<List<ApplicationRules>>(_appRulesPath, ApplicationRulesContext.Default));
                }
                catch (Exception e) {
                    App.Log.Error(e.ToString());
                    AppRules =
                    [
                        new ApplicationRules(Constants.CoreField.AppName, AppWpRunRulesEnum.KeepRun)
                    ];
                    Save<List<IApplicationRules>>();
                }
            }
            else if (typeof(T) == typeof(List<IWallpaperLayout>)) {
                try {
                    WallpaperLayouts = new List<IWallpaperLayout>(JsonSaver.Load<List<WallpaperLayout>>(_wallpaperLayoutPath, WallpaperLayoutContext.Default));
                }
                catch (Exception e) {
                    App.Log.Error(e.ToString());
                    WallpaperLayouts = [];
                    Save<List<IWallpaperLayout>>();
                }
            }
            else {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        public void Save<T>() {
            if (typeof(T) == typeof(ISettings)) {
                JsonSaver.Store(_settingsPath, Settings, SettingsContext.Default);
            }
            else if (typeof(T) == typeof(List<IApplicationRules>)) {
                JsonSaver.Store(_appRulesPath, AppRules, ApplicationRulesContext.Default);
            }
            else if (typeof(T) == typeof(List<IWallpaperLayout>)) {
                JsonSaver.Store(_wallpaperLayoutPath, WallpaperLayouts, WallpaperLayoutContext.Default);
            }
            else {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        private readonly string _settingsPath = Constants.CommonPaths.UserSettingsPath;
        private readonly string _appRulesPath = Constants.CommonPaths.AppRulesPath;
        private readonly string _wallpaperLayoutPath = Constants.CommonPaths.WallpaperLayoutPath;
        private readonly string _recentUsedPath = Constants.CommonPaths.RecentUsedPath;
    }
}
