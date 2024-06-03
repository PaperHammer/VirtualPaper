using Microsoft.Win32;
using NLog;
using System.Diagnostics;
using System.Text;
using System.Windows.Threading;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Hardware;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Cores.Desktop;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Cores.ScreenSaver;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Cores.PlaybackControl
{
    /// <summary>
    /// System monitor logic to Pause/unpause wallpaper playback.
    /// </summary>
    public class Playback : IPlayback
    {
        public event EventHandler<PlaybackMode>? PlaybackModeChanged;

        private PlaybackMode _wallpaperPlayback;
        public PlaybackMode WallpaperPlaybackMode
        {
            get => _wallpaperPlayback;
            set { _wallpaperPlayback = value; PlaybackModeChanged?.Invoke(this, _wallpaperPlayback); }
        }

        public Playback(
            IUserSettingsService userSettings,
            IWallpaperControl wpControl,
            IScrControl scrControl,
            IMonitorManager monitoeManger)
        {
            _userSettings = userSettings;
            _wpControl = wpControl;
            _scrControl = scrControl;
            _monitorManger = monitoeManger;

            Initialize();
            wpControl.WallpaperReset += (s, e) => FindNewMonitorAndResetHandles();
        }

        private void FindNewMonitorAndResetHandles()
        {
            _workerWOrig = IntPtr.Zero;
            _progman = IntPtr.Zero;

            _progman = Native.FindWindow("Progman", null);
            var folderView = Native.FindWindowEx(_progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (folderView == IntPtr.Zero)
            {
                do
                {
                    _workerWOrig = Native.FindWindowEx(Native.GetDesktopWindow(), _workerWOrig, "WorkerW", null);
                    folderView = Native.FindWindowEx(_workerWOrig, IntPtr.Zero, "SHELLDLL_DefView", null);
                } while (folderView == IntPtr.Zero && _workerWOrig != IntPtr.Zero);
            }
        }

        private void Initialize()
        {
            InitializeTimer();
            WallpaperPlaybackMode = PlaybackMode.Play;

            try
            {
                using Process process = Process.GetCurrentProcess();
                _virtualPaperPid = process.Id;
            }
            catch (Exception e)
            {
                _logger.Error("Failed to retrieve Virtual Paper Pid:" + e.Message);
            }

            _isLockScreen = IsSystemLocked();
            if (_isLockScreen)
            {
                _logger.Info("Lockscreen Session already started!");
            }
            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
        }

        private void InitializeTimer()
        {
            _dispatcherTimer.Tick += new EventHandler(ProcessMonitor);
            _dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, _userSettings.Settings.ProcessTimerInterval);
        }

        private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.RemoteConnect)
            {
                _isRemoteSession = true;
                _logger.Info("Remote Desktop Session started!");
            }
            else if (e.Reason == SessionSwitchReason.RemoteDisconnect)
            {
                _isRemoteSession = false;
                _logger.Info("Remote Desktop Session ended!");
            }
            else if (e.Reason == SessionSwitchReason.SessionLock)
            {
                _isLockScreen = true;
                _logger.Info("Lockscreen Session started!");
            }
            else if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                _isLockScreen = false;
                _logger.Info("Lockscreen Session ended!");
            }
        }

        public void Start()
        {
            _dispatcherTimer.Start();
        }

        public void Stop()
        {
            _dispatcherTimer.Stop();
        }

        private void ProcessMonitor(object? sender, EventArgs e)
        {
            if (_scrControl.IsRunning)
            {
                ChangeState(AppWpRunRulesEnum.Pause);
            }
            else if (WallpaperPlaybackMode == PlaybackMode.Paused || _isLockScreen ||
                (_isRemoteSession && _userSettings.Settings.RemoteDesktop == AppWpRunRulesEnum.Pause))
            {
                ChangeState(AppWpRunRulesEnum.Pause);
            }
            else if (WallpaperPlaybackMode == PlaybackMode.Silence || _isLockScreen ||
                (_isRemoteSession && _userSettings.Settings.RemoteDesktop == AppWpRunRulesEnum.Silence))
            {
                ChangeState(AppWpRunRulesEnum.Silence);
            }
            else if (PowerUtil.GetACPowerStatus() == PowerUtil.ACLineStatus.Offline &&
                _userSettings.Settings.BatteryPoweredn == AppWpRunRulesEnum.Pause)
            {
                ChangeState(AppWpRunRulesEnum.Pause);
            }
            else if (PowerUtil.GetACPowerStatus() == PowerUtil.ACLineStatus.Offline &&
                _userSettings.Settings.BatteryPoweredn == AppWpRunRulesEnum.Silence)
            {
                ChangeState(AppWpRunRulesEnum.Silence);
            }
            else if (PowerUtil.GetBatterySaverStatus() == PowerUtil.SystemStatusFlag.On &&
                _userSettings.Settings.PowerSaving == AppWpRunRulesEnum.Pause)
            {
                ChangeState(AppWpRunRulesEnum.Pause);
            }
            else if (PowerUtil.GetBatterySaverStatus() == PowerUtil.SystemStatusFlag.On &&
                _userSettings.Settings.PowerSaving == AppWpRunRulesEnum.Silence)
            {
                ChangeState(AppWpRunRulesEnum.Silence);
            }
            else
            {
                AdjustWpBehaviourBaseOnForegroundApp();
            }
        }

        private void AdjustWpBehaviourBaseOnForegroundApp()
        {
            var isDesktop = false;
            var fHandle = Native.GetForegroundWindow(); // 当前最前台进程

            #region 白名单判断
            if (IsWhitelistedClass(fHandle))
            {
                ChangeState(AppWpRunRulesEnum.KeepRun);
                return;
            }
            #endregion

            #region 预设内容
            try
            {
                _ = Native.GetWindowThreadProcessId(fHandle, out int processID);
                using Process fProcess = Process.GetProcessById(processID);

                //process with no name, possibly overlay or some other service pgm; resume playback.
                if (string.IsNullOrEmpty(fProcess.ProcessName) || fHandle.Equals(IntPtr.Zero))
                {
                    ChangeState(AppWpRunRulesEnum.KeepRun);
                    return;
                }

                ////is it vp or its plugins..
                //if (fProcess.Id == _virtualPaperPid || IsPlugin(fProcess.Id))
                //{
                //    ChangeState(AppWpRunRulesEnum.KeepRun);
                //    return;
                //}

                //looping through custom rules for user defined..
                // 对于指定前台进程的预设置操作
                for (int i = 0; i < _userSettings.AppRules.Count; i++)
                {
                    var item = _userSettings.AppRules[i];
                    if (string.Equals(item.AppName, fProcess.ProcessName, StringComparison.Ordinal))
                    {
                        ChangeState(item.Rule);
                        return;
                    }
                }
            }
            catch
            {
                //failed to get process info.. maybe remote process; resume playback.
                ChangeState(AppWpRunRulesEnum.KeepRun);
                return;
            }
            #endregion

            #region 关于桌面焦点、应用程序是否最大化/覆盖全屏的检测
            try
            {
                // 关于桌面焦点、应用程序是否最大化/覆盖全屏的检测
                if (!(fHandle.Equals(Native.GetDesktopWindow()) 
                    || fHandle.Equals(Native.GetShellWindow())))
                {
                    #region 单屏
                    // 单屏
                    if (!_monitorManger.IsMultiScreen() ||
                        _userSettings.Settings.StatuMechanism == StatuMechanismEnum.All)
                    //_userSettingsService.Settings.WallpaperArrangement == WallpaperArrangement.Duplicate)
                    {
                        // 检查前台窗口是否为桌面环境的一部分
                        if (IntPtr.Equals(fHandle, _workerWOrig) || IntPtr.Equals(fHandle, _progman))
                        {
                            // 用户焦点在桌面
                            //win10 and win7 desktop foreground while running.
                            isDesktop = true;
                            ChangeState(AppWpRunRulesEnum.KeepRun);
                        }
                        // 检查前台窗口是否最大化或几乎覆盖整个屏幕
                        else if (Native.IsZoomed(fHandle) || IsZoomedCustom(fHandle))
                        {
                            //maximised window or window covering whole screen.
                            ChangeState(_userSettings.Settings.AppFullscreen);
                        }
                        else
                        {
                            //window is just in focus, not covering screen.
                            ChangeState(_userSettings.Settings.AppFocus);
                        }
                    }
                    #endregion

                    #region 多屏
                    // 多屏
                    else
                    {
                        // 仅聚焦屏幕播放声音
                        IMonitor? focusedScreen;
                        if ((focusedScreen = MapWindowToMonitor(fHandle)) != null)
                        {
                            //unpausing the rest of _wallpapers.
                            //only one window can be foreground!
                            foreach (var item in _monitorManger.Monitors)
                            {
                                if (_userSettings.Settings.WallpaperArrangement != WallpaperArrangement.Expand &&
                                    !focusedScreen.Equals(item))
                                {
                                    ChangeState(AppWpRunRulesEnum.Silence, item);
                                }
                            }
                        }
                        else
                        {
                            //no monitor connected?
                            return;
                        }

                        // 检查前台窗口是否为桌面环境的一部分
                        if (IntPtr.Equals(fHandle, _workerWOrig) || IntPtr.Equals(fHandle, _progman))
                        {
                            //win10 and win7 desktop foreground while running.
                            isDesktop = true;
                            ChangeState(AppWpRunRulesEnum.Silence, focusedScreen);
                        }
                        // 说明在其他焦点应用程序上
                        else if (_userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Expand)
                        {
                            // 跨越多屏                            
                            if (IsZoomedSpan(fHandle))
                            {
                                ChangeState(AppWpRunRulesEnum.Pause);
                                //PauseWallpapers();
                            }
                            else //window is not greater >90%
                            {
                                ChangeState(_userSettings.Settings.AppFocus);
                            }
                        }
                        else if (Native.IsZoomed(fHandle) || IsZoomedCustom(fHandle))
                        {
                            //maximised window or window covering whole screen.
                            ChangeState(_userSettings.Settings.AppFullscreen, focusedScreen);
                        }
                        else
                        {
                            //window is just in focus, not covering screen.
                            ChangeState(_userSettings.Settings.AppFocus, focusedScreen);
                        }
                    }
                    #endregion

                    if (isDesktop)
                    {
                        ChangeState(AppWpRunRulesEnum.KeepRun);
                    }
                    else if (_userSettings.Settings.IsAudioOnlyOnDesktop)
                    {
                        ChangeState(AppWpRunRulesEnum.Silence);
                    }
                }
            }
            catch { }
            #endregion
        }

        private bool IsWhitelistedClass(IntPtr hwnd)
        {
            const int maxChars = 256;
            StringBuilder className = new(maxChars);
            return Native.GetClassName(hwnd, className, maxChars) > 0 && _classWhiteList.Any(x => x.Equals(className.ToString(), StringComparison.Ordinal));
        }

        private void PauseWallpapers()
        {
            foreach (var x in _wpControl.Wallpapers)
            {
                x.Pause();
            }
        }

        private void PlayWallpapers()
        {
            foreach (var x in _wpControl.Wallpapers)
            {
                x.Play();
            }
        }

        private void PauseWallpaper(IMonitor monitor)
        {
            foreach (var x in _wpControl.Wallpapers)
            {
                if (x.Monitor.Equals(monitor))
                {
                    x.Pause();
                }
            }
        }

        private void PlayWallpaper(IMonitor monitor)
        {
            foreach (var x in _wpControl.Wallpapers)
            {
                if (x.Monitor.Equals(monitor))
                {
                    x.Play();
                }
            }
        }

        private void SilenceWallpapers()
        {
            foreach (var x in _wpControl.Wallpapers)
            {
                x.SetMute(true);
            }
        }

        private void UnSilenceWallpapers()
        {
            foreach (var x in _wpControl.Wallpapers)
            {
                x.SetMute(false);
            }
        }

        private void SilenceWallpaper(IMonitor monitor)
        {
            foreach (var x in _wpControl.Wallpapers)
            {
                if (x.Monitor.Equals(monitor))
                {
                    x.SetMute(true);
                }
            }
        }

        private void UnSilenceWallpaper(IMonitor monitor)
        {
            foreach (var x in _wpControl.Wallpapers)
            {
                if (x.Monitor.Equals(monitor))
                {
                    x.SetMute(false);
                }
            }
        }

        #region utils
        public void ChangeState(AppWpRunRulesEnum nextState, IMonitor? targetMonitor = null)
        {
            switch (nextState)
            {
                case AppWpRunRulesEnum.KeepRun:
                    if (targetMonitor == null)
                    {
                        PlayWallpapers();
                        UnSilenceWallpapers();
                    }
                    else
                    {
                        PlayWallpaper(targetMonitor);
                        UnSilenceWallpaper(targetMonitor);
                    }
                    break;
                case AppWpRunRulesEnum.Silence:
                    if (targetMonitor == null)
                    {
                        PlayWallpapers();
                        SilenceWallpapers();
                    }
                    else
                    {
                        PlayWallpaper(targetMonitor);
                        SilenceWallpaper(targetMonitor);
                    }
                    break;
                case AppWpRunRulesEnum.Pause:
                    if (targetMonitor == null)
                    {
                        PauseWallpapers();
                    }
                    else
                    {
                        PauseWallpaper(targetMonitor);
                    }
                    break;
            }
        }

        //private bool IsPlugin(int pid)
        //{
        //    return _wpControl.Wallpapers.Any(x => x.Proc != null && x.Proc.Id == pid);
        //}

        /// <summary>
        /// Checks if hWnd window size is >95% for its running screen.
        /// </summary>
        /// <returns>True if window dimensions are greater.</returns>
        private bool IsZoomedCustom(IntPtr hWnd)
        {
            try
            {
                Rectangle screenBounds;
                _ = Native.GetWindowRect(hWnd, out Native.RECT appBounds);
                screenBounds = _monitorManger.GetMonitorByHWnd(hWnd).Bounds;
                //If foreground app 95% working-area( -taskbar of monitor)
                if ((appBounds.Bottom - appBounds.Top) >= screenBounds.Height * .95f && (appBounds.Right - appBounds.Left) >= screenBounds.Width * .95f)
                    return true;
                else
                    return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Finds out which displaydevice the given application is residing.
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        private IMonitor? MapWindowToMonitor(IntPtr handle)
        {
            try
            {
                return _monitorManger.GetMonitorByHWnd(handle);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Checks if the hWnd dimension is spanned across All displays.
        /// </summary>
        /// <param name="hWnd"></param>
        /// <returns></returns>
        private bool IsZoomedSpan(IntPtr hWnd)
        {
            _ = Native.GetWindowRect(hWnd, out Native.RECT appBounds);
            var screenArea = _monitorManger.VirtualScreenBounds;
            // If foreground app 95% working-area( - taskbar of monitor)
            return ((appBounds.Bottom - appBounds.Top) >= screenArea.Height * .95f &&
               (appBounds.Right - appBounds.Left) >= screenArea.Width * .95f);
        }

        /// <summary>
        /// Checks if LockApp is foreground program.
        /// <para>Could not find a better way to do this quickly,
        /// Lockscreen class is "Windows.UI.Core.CoreWindow" which is used by other windows UI elements.</para>
        /// This should be enough for just checking before subscribing to the Lock/Unlocked windows event.
        /// </summary>
        /// <returns>True if lockscreen is active.</returns>
        private bool IsSystemLocked()
        {
            bool result = false;
            var fHandle = Native.GetForegroundWindow();
            try
            {
                _ = Native.GetWindowThreadProcessId(fHandle, out int processID);
                using (Process fProcess = Process.GetProcessById(processID))
                {
                    result = fProcess.ProcessName.Equals("LockApp", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
            return result;
        }
        #endregion

        #region dispoae
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _dispatcherTimer.Stop();
                    SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
                }
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly string[] _classWhiteList =
        [
            //startmeu, taskview (win10), action center etc
            "Windows.UI.Core.CoreWindow",
            //alt+tab screen (win10)
            "MultitaskingViewFrame",
            //taskview (win11)
            "XamlExplorerHostIslandWindow",
            //widget window (win11)
            "WindowsDashboard",
            //taskbar(s)
            "Shell_TrayWnd",
            "Shell_SecondaryTrayWnd",
            //systray notifyicon expanded popup
            "NotifyIconOverflowWindow",
            //rainmeter widgets
            "RainmeterMeterWindow",

            "_cls_desk_"
        ];
        private IntPtr _workerWOrig, _progman;
        private readonly DispatcherTimer _dispatcherTimer = new();
        private bool _isLockScreen, _isRemoteSession;
        private int _virtualPaperPid = 0;
        private readonly IUserSettingsService _userSettings;
        private readonly IWallpaperControl _wpControl;
        private readonly IMonitorManager _monitorManger;
        private readonly IScrControl _scrControl;
    }
}
