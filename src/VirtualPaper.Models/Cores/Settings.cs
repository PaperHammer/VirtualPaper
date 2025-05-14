using System.Globalization;
using System.IO;
using System.Text.Json.Serialization;
using VirtualPaper.Common;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Models.Cores {
    [JsonSerializable(typeof(Settings))]
    [JsonSerializable(typeof(ISettings))]
    public partial class SettingsContext : JsonSerializerContext { }

    public class Settings : ISettings {
        #region for app
        public AppWpRunRulesEnum AppFocus { get; set; }
        public AppWpRunRulesEnum AppFullscreen { get; set; }
        public AppTheme ApplicationTheme { get; set; }
        public AppWpRunRulesEnum BatteryPoweredn { get; set; }
        public AppWpRunRulesEnum PowerSaving { get; set; }
        public AppWpRunRulesEnum RemoteDesktop { get; set; }
        public AppSystemBackdrop SystemBackdrop { get; set; }
        public string AppName { get; set; } = string.Empty;
        public string AppVersion { get; set; } = string.Empty;
        public string FileVersion { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public bool IsUpdated { get; set; }
        public bool IsAutoStart { get; set; }
        public bool IsFirstRun { get; set; }
        #endregion

        #region dirs
        public string WallpaperDir { get; set; } = string.Empty;
        #endregion

        #region play
        public Monitor SelectedMonitor { get; set; }
        public bool IsAudioOnlyOnDesktop { get; set; }
        public StatuMechanismEnum StatuMechanism { get; set; }
        public WallpaperScaler WallpaperScaling { get; set; }
        public int WallpaperWaitTime { get; set; }
        #endregion

        #region input
        public InputForwardMode InputForward { get; set; }
        public bool MouseInputMovAlways { get; set; }
        #endregion

        #region screen settings
        public WallpaperArrangement WallpaperArrangement { get; set; }
        #endregion

        #region process utils
        public int ProcessTimerInterval { get; set; }
        #endregion

        #region screen saver
        public bool IsScreenSaverOn { get; set; }
        public bool IsRunningLock { get; set; }
        public int WaitingTime { get; set; }
        public ScrEffect ScreenSaverEffect { get; set; }
        public List<ProcInfo> WhiteListScr { get; set; }
        #endregion

        public Settings() {
            WallpaperArrangement = WallpaperArrangement.Per;
            AppName = Constants.CoreField.AppName;
            AppVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName()?.Version?.ToString() ?? "";
            FileVersion = Constants.CoreField.FileVersion;
            IsFirstRun = true;
            AppFocus = AppWpRunRulesEnum.KeepRun;
            AppFullscreen = AppWpRunRulesEnum.Pause;
            BatteryPoweredn = AppWpRunRulesEnum.KeepRun;

            SelectedMonitor = new();

            WallpaperWaitTime = 20000; // 20sec
            ProcessTimerInterval = 500; //reduce to 250 for quicker response.

            InputForward = InputForwardMode.mouse;
            MouseInputMovAlways = true;

            WallpaperDir = Path.Combine(Constants.CommonPaths.LibraryDir, Constants.FolderName.WpStoreFolderName);
            WallpaperScaling = WallpaperScaler.fill;
            ApplicationTheme = AppTheme.Dark;
            RemoteDesktop = AppWpRunRulesEnum.Pause;
            PowerSaving = AppWpRunRulesEnum.KeepRun;
            IsUpdated = false;
            SystemBackdrop = AppSystemBackdrop.Default;

            IsScreenSaverOn = false;
            IsRunningLock = false;
            WaitingTime = 1; // minutes
            ScreenSaverEffect = ScrEffect.None;
            WhiteListScr = [];

            try {
                Language = CultureInfo.CurrentUICulture.Name;
            }
            catch (ArgumentNullException) {
                Language = "zh-CN";
            }
        }
    }
}
