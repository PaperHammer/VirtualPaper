using System.Text.Json.Serialization;
using VirtualPaper.Common;

namespace VirtualPaper.Models.Cores.Interfaces
{
    public interface ISettings
    {
        #region for app
        AppWpRunRulesEnum AppFullscreen { get; set; }
        AppWpRunRulesEnum AppFocus { get; set; }        
        AppTheme ApplicationTheme { get; set; }
        AppWpRunRulesEnum BatteryPoweredn { get; set; }
        AppWpRunRulesEnum PowerSaving { get; set; }
        AppWpRunRulesEnum RemoteDesktop { get; set; }
        AppSystemBackdrop SystemBackdrop { get; set; }
        string AppVersion { get; set; }
        //string ApplicationThemeBackgroundPath { get; set; }
        string Language { get; set; }        
        bool IsUpdated { get; set; }
        bool IsAutoStart { get; set; }
        bool IsFirstRun { get; set; }
        #endregion

        #region dirs
        /// <summary>
        /// 壁纸所在位置
        /// </summary>
        string WallpaperDir { get; set; }
        #endregion

        #region play utils
        [JsonIgnore]
        Monitor SelectedMonitor { get; set; }

        bool IsAudioOnlyOnDesktop { get; set; }
        StatuMechanismEnum StatuMechanism { get; set; }

        /// <summary>
        /// 壁纸播放时占位方式
        /// </summary>
        WallpaperScaler WallpaperScaling { get; set; }

        /// <summary>
        /// 加载延时
        /// </summary>
        int WallpaperWaitTime { get; set; }
        #endregion

        #region input
        InputForwardMode InputForward { get; set; }
        bool MouseInputMovAlways { get; set; }
        #endregion

        #region screen settings
        WallpaperArrangement WallpaperArrangement { get; set; }
        #endregion

        #region screen saver
        /// <summary>
        /// 恢复时屏幕保护程序是否锁定
        /// </summary>
        bool IsScreensaverLockOnResume { get; set; }
        /// <summary>
        /// 屏幕保护程序是否空屏显示黑色
        /// </summary>
        bool IsScreensaverEmptyScreenShowBlack { get; set; }
        #endregion

        #region process utils
        int ProcessTimerInterval { get; set; }
        #endregion
    }
}
