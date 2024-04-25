using NLog;
using System.IO;
using System.Reflection;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Utils;

namespace VirtualPaper.Services
{
    public class UserSettingsService : IUserSettingsService
    {
        public ISettings Settings { get; private set; }
        public List<IApplicationRules> AppRules { get; private set; } = [];
        public List<IWallpaperLayout> WallpaperLayout { get; private set; } = [];

        public UserSettingsService(
            IMonitorManager moitorManager,
            ITaskbarService taskbarSevice)
        {
            Load<ISettings>();
            Load<List<IApplicationRules>>();
            Load<List<IWallpaperLayout>>();

            Settings.SelectedMonitor = Settings.SelectedMonitor != null ?
                moitorManager.Monitors.FirstOrDefault(x => x.Equals(Settings.SelectedMonitor)) ?? moitorManager.PrimaryMonitor : moitorManager.PrimaryMonitor;

            //Settings.VideoPlayer = IsVideoPlayerAvailable(Settings.VideoPlayer) ? Settings.VideoPlayer : VirtualPaperMediaPlayer.mpv;
            //Settings.GifPlayer = IsGifPlayerAvailable(Settings.GifPlayer) ? Settings.GifPlayer : VirtualPaperGifPlayer.mpv;
            //Settings.WebBrowser = IsWebPlayerAvailable(Settings.WebBrowser) ? Settings.WebBrowser : VirtualPaperWebBrowser.cef;

            //previous installed appversion is different from current instance..    
            if (!Settings.AppVersion.Equals(Assembly.GetExecutingAssembly().GetName().Version.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                Settings.AppVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                Settings.IsUpdated = true;
            }

            var lang = SupportedLanguages.GetLanguage(Settings.Language);
            if (lang.Codes.FirstOrDefault(x => x == Settings.Language) == null)
            {
                Settings.Language = lang.Codes[0];
            }

            try
            {
                _ = WindowsAutoStart.SetAutoStart(Settings.IsAutoStart);
            }
            catch (Exception e)
            {
                _logger.Error(e);
            }

            if (Settings.SystemTaskbarTheme != TaskbarTheme.none)
            {
                taskbarSevice.Start(Settings.SystemTaskbarTheme);
            }
        }

        public void Load<T>()
        {
            if (typeof(T) == typeof(ISettings))
            {
                try
                {
                    Settings = JsonStorage<Settings>.LoadData(_settingsPath);
                }
                catch (Exception e)
                {
                    _logger.Error(e);
                    Settings = new Settings();
                    Save<ISettings>();
                }
            }
            else if (typeof(T) == typeof(List<IApplicationRules>))
            {
                try
                {
                    AppRules = new List<IApplicationRules>(JsonStorage<List<ApplicationRules>>.LoadData(_appRulesPath));
                }
                catch (Exception e)
                {
                    _logger.Error(e.ToString());
                    AppRules =
                    [
                        new ApplicationRules("Discord", AppWpRunRulesEnum.KeepRun)
                    ];
                    Save<List<IApplicationRules>>();
                }
            }
            else if (typeof(T) == typeof(List<IWallpaperLayout>))
            {
                try
                {
                    WallpaperLayout = new List<IWallpaperLayout>(JsonStorage<List<WallpaperLayout>>.LoadData(_wallpaperLayoutPath));
                }
                catch (Exception e)
                {
                    _logger.Error(e.ToString());
                    WallpaperLayout = [];
                    Save<List<IWallpaperLayout>>();
                }
            }
            else
            {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        public void Save<T>()
        {
            if (typeof(T) == typeof(ISettings))
            {
                JsonStorage<ISettings>.StoreData(_settingsPath, Settings);
            }
            else if (typeof(T) == typeof(List<IApplicationRules>))
            {
                JsonStorage<List<IApplicationRules>>.StoreData(_appRulesPath, AppRules);
            }
            else if (typeof(T) == typeof(List<IWallpaperLayout>))
            {
                JsonStorage<List<IWallpaperLayout>>.StoreData(_wallpaperLayoutPath, WallpaperLayout);
            }
            else
            {
                throw new InvalidCastException($"ValueType not found: {typeof(T)}");
            }
        }

        #region helpers
        private bool IsVideoPlayerAvailable(VirtualPaperMediaPlayer mp)
        {
            return mp switch
            {
                //VirtualPaperMediaPlayer.libvlc => false, //depreciated
                //VirtualPaperMediaPlayer.libmpv => false, //depreciated
                VirtualPaperMediaPlayer.wmf => File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "wmf", "VirtualPaper.PlayerWmf.exe")),
                //VirtualPaperMediaPlayer.libvlcExt => File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "libVLCPlayer", "libVLCPlayer.exe")),
                //VirtualPaperMediaPlayer.libmpvExt => File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "libMPVPlayer", "libMPVPlayer.exe")),
                VirtualPaperMediaPlayer.mpv => File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "mpv", "mpv.exe")),
                VirtualPaperMediaPlayer.vlc => File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "vlc", "vlc.exe")),
                _ => false,
            };
        }

        private bool IsGifPlayerAvailable(VirtualPaperGifPlayer gp)
        {
            return gp switch
            {
                //VirtualPaperGifPlayer.win10Img => false, //xaml island
                //VirtualPaperGifPlayer.libmpvExt => File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "libMPVPlayer", "libMPVPlayer.exe")),
                VirtualPaperGifPlayer.mpv => File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "mpv", "mpv.exe")),
                _ => false,
            };
        }

        private bool IsWebPlayerAvailable(VirtualPaperWebBrowser wp)
        {
            return wp switch
            {
                VirtualPaperWebBrowser.cef => File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "Cef", "VirtualPaper.PlayerCefSharp.exe")),
                VirtualPaperWebBrowser.webview2 => File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "Wv2", "VirtualPaper.PlayerWebView2.exe")),
                _ => false,
            };
        }
        #endregion

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly string _settingsPath = Constants.CommonPaths.UserSettingsPath;
        private readonly string _appRulesPath = Constants.CommonPaths.AppRulesPath;
        private readonly string _wallpaperLayoutPath = Constants.CommonPaths.WallpaperLayoutPath;
    }
}
