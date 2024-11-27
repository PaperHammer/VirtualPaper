using System.Reflection;
using NLog;
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
                _logger.Error(e);
            }
        }

        public void Load<T>() {
            if (typeof(T) == typeof(ISettings)) {
                try {
                    Settings = JsonStorage<Settings>.LoadData(_settingsPath);
                }
                catch (Exception e) {
                    _logger.Error(e);
                    Settings = new Settings();
                    Save<ISettings>();
                }
            }
            else if (typeof(T) == typeof(List<IApplicationRules>)) {
                try {
                    AppRules = new List<IApplicationRules>(JsonStorage<List<ApplicationRules>>.LoadData(_appRulesPath));
                }
                catch (Exception e) {
                    _logger.Error(e.ToString());
                    AppRules =
                    [
                        new ApplicationRules("Discord", AppWpRunRulesEnum.KeepRun)
                    ];
                    Save<List<IApplicationRules>>();
                }
            }
            else if (typeof(T) == typeof(List<IWallpaperLayout>)) {
                try {
                    WallpaperLayouts = new List<IWallpaperLayout>(JsonStorage<List<WallpaperLayout>>.LoadData(_wallpaperLayoutPath));
                }
                catch (Exception e) {
                    _logger.Error(e.ToString());
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
                JsonStorage<ISettings>.StoreData(_settingsPath, Settings);
            }
            else if (typeof(T) == typeof(List<IApplicationRules>)) {
                JsonStorage<List<IApplicationRules>>.StoreData(_appRulesPath, AppRules);
            }
            else if (typeof(T) == typeof(List<IWallpaperLayout>)) {
                JsonStorage<List<IWallpaperLayout>>.StoreData(_wallpaperLayoutPath, WallpaperLayouts);
            }
            else {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly string _settingsPath = Constants.CommonPaths.UserSettingsPath;
        private readonly string _appRulesPath = Constants.CommonPaths.AppRulesPath;
        private readonly string _wallpaperLayoutPath = Constants.CommonPaths.WallpaperLayoutPath;
    }
}
