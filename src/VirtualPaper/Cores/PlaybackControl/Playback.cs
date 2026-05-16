using System.Diagnostics;
using Microsoft.Win32;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Hardware;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Cores.ScreenSaver;
using VirtualPaper.Cores.WpControl;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Utils.Interfcaes;

namespace VirtualPaper.Cores.PlaybackControl {
    /// <summary>
    /// System _monitor logic to Pause/unpause wallpaper playback.
    /// </summary>
    public partial class Playback : IPlayback {
        public event EventHandler<PlaybackMode>? PlaybackModeChanged;

        private PlaybackMode _wallpaperPlayback;
        public PlaybackMode WallpaperPlaybackMode {
            get => _wallpaperPlayback;
            set { _wallpaperPlayback = value; PlaybackModeChanged?.Invoke(this, _wallpaperPlayback); }
        }

        public Playback(
            IUserSettingsService userSettings,
            IWallpaperControl wpControl,
            IScrControl scrControl,
            IMonitorManager monitoeManger,
            IPowerService powerService) {
            _userSettings = userSettings;
            _wpControl = wpControl;
            _scrControl = scrControl;
            _monitorManger = monitoeManger;
            _powerService = powerService;

            Initialize();
            _timer = new(TimeSpan.FromMilliseconds(Math.Max(_userSettings.Settings.ProcessTimerInterval, 500)));
        }

        public async void Start(CancellationTokenSource tokenSource) {
            try {
                _ctsPlayback = tokenSource;
                while (await _timer.WaitForNextTickAsync(tokenSource.Token)) {
                    RunPlayback();
                }
            }
            catch (OperationCanceledException) {
                ArcLog.GetLogger<Playback>().Info("Playback stoppped");
            }
            catch (Exception ex) {
                ArcLog.GetLogger<Playback>().Error("Playback runtime Error: ", ex);
            }
        }

        public void Stop() {
            _ctsPlayback?.Cancel();
        }

        private void Initialize() {
            WallpaperPlaybackMode = PlaybackMode.Play;

            _isLockScreen = IsSystemLocked();
            if (_isLockScreen) {
                ArcLog.GetLogger<Playback>().Info("Lockscreen Session already started!");
            }
            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
        }

        #region utils
        private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e) {
            if (e.Reason == SessionSwitchReason.RemoteConnect) {
                _isRemoteSession = true;
                ArcLog.GetLogger<Playback>().Info("Remote Desktop Session started!");
            }
            else if (e.Reason == SessionSwitchReason.RemoteDisconnect) {
                _isRemoteSession = false;
                ArcLog.GetLogger<Playback>().Info("Remote Desktop Session ended!");
            }
            else if (e.Reason == SessionSwitchReason.SessionLock) {
                _isLockScreen = true;
                ArcLog.GetLogger<Playback>().Info("Lockscreen Session started!");
            }
            else if (e.Reason == SessionSwitchReason.SessionUnlock) {
                _isLockScreen = false;
                ArcLog.GetLogger<Playback>().Info("Lockscreen Session ended!");
            }
        }

        private void RunPlayback() {
            if (_wpControl.Wallpapers.Count > 0) {
                if (_scrControl.IsRunning) {
                    ChangeWpState(AppWpRunRulesEnum.Pause);
                }
                else if (WallpaperPlaybackMode == PlaybackMode.Paused || _isLockScreen ||
                    (_isRemoteSession && _userSettings.Settings.RemoteDesktop == AppWpRunRulesEnum.Pause)) {
                    ChangeWpState(AppWpRunRulesEnum.Pause);
                }
                else if (WallpaperPlaybackMode == PlaybackMode.Silence || _isLockScreen ||
                    (_isRemoteSession && _userSettings.Settings.RemoteDesktop == AppWpRunRulesEnum.Silence)) {
                    ChangeWpState(AppWpRunRulesEnum.Silence);
                }
                else if (_powerService.GetACPowerStatus() == PowerUtil.ACLineStatus.Offline &&
                    _userSettings.Settings.BatteryPoweredn == AppWpRunRulesEnum.Pause) {
                    ChangeWpState(AppWpRunRulesEnum.Pause);
                }
                else if (_powerService.GetACPowerStatus() == PowerUtil.ACLineStatus.Offline &&
                    _userSettings.Settings.BatteryPoweredn == AppWpRunRulesEnum.Silence) {
                    ChangeWpState(AppWpRunRulesEnum.Silence);
                }
                else if (_powerService.GetBatterySaverStatus() == PowerUtil.SystemStatusFlag.On &&
                    _userSettings.Settings.PowerSaving == AppWpRunRulesEnum.Pause) {
                    ChangeWpState(AppWpRunRulesEnum.Pause);
                }
                else if (_powerService.GetBatterySaverStatus() == PowerUtil.SystemStatusFlag.On &&
                    _userSettings.Settings.PowerSaving == AppWpRunRulesEnum.Silence) {
                    ChangeWpState(AppWpRunRulesEnum.Silence);
                }
                else {
                    AdjustWpBehaviourBaseOnForegroundApp();
                }
            }
        }

        private void AdjustWpBehaviourBaseOnForegroundApp() {
            nint fHandle = Native.GetForegroundWindow();
            var isValidWindow = WindowUtil.IsVisibleTopLevelWindows(fHandle);

            #region 焦点是否在桌面
            bool isDesktop;
            if (isValidWindow) {
                isDesktop = WindowUtil.IsExcludedDesktopWindowClass(fHandle);
            }
            else {
                // We assume its desktop, not enough information otherwise since only foreground window is checked.
                isDesktop = fHandle == Native.GetDesktopWindow() || fHandle == Native.GetShellWindow() || WindowUtil.IsExcludedDesktopWindowClass(fHandle);
            }
            #endregion

            #region 预设内容
            try {
                _ = Native.GetWindowThreadProcessId(fHandle, out int processID);
                using Process fProcess = Process.GetProcessById(processID);

                //process with no name, possibly overlay or some other service pgm; resume playback.
                if (string.IsNullOrEmpty(fProcess.ProcessName) || fHandle.Equals(IntPtr.Zero)) {
                    ChangeWpState(AppWpRunRulesEnum.KeepRun);
                    return;
                }

                ////is it vp or its plugins..
                //if (fProcess.Id == _virtualPaperPid || IsPlugin(fProcess.Id))
                //{
                //    ChangeWpState(AppWpRunRulesEnum.KeepRun);
                //    return;
                //}

                //looping through custom rules for user defined..
                // 对于指定前台进程的预设置操作
                for (int i = 0; i < _userSettings.AppRules.Count; i++) {
                    var item = _userSettings.AppRules[i];
                    if (string.Equals(item.AppName, fProcess.ProcessName, StringComparison.Ordinal)) {
                        ChangeWpState(item.Rule);
                        return;
                    }
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<Playback>().Error("Playback Changes for AppRules Error: ", ex);
                //failed to get process info.. maybe remote process; resume playback.
                ChangeWpState(AppWpRunRulesEnum.KeepRun);
                return;
            }
            #endregion

            #region 关于桌面焦点、应用程序是否最大化/覆盖全屏的检测
            try {
                // 关于桌面焦点、应用程序是否最大化/覆盖全屏的检测
                #region 单屏
                // 单屏
                if (!_monitorManger.IsMultiScreen() ||
                    _userSettings.Settings.StatuMechanism == StatuMechanismEnum.All)
                //_userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Duplicate)
                {
                    // 检查前台窗口是否为桌面环境的一部分
                    if (isDesktop) {
                        // 焦点在桌面
                        //win10 and win7 desktop foreground while running.
                        ChangeWpState(AppWpRunRulesEnum.KeepRun);
                        ChangeParralaxState(AppParallaxRulesEnum.KeepRun);
                    }
                    // 检查前台窗口是否最大化或几乎覆盖整个屏幕
                    else if (Native.IsZoomed(fHandle) || IsZoomedCustom(fHandle)) {
                        //maximised window or window covering whole screen.
                        ChangeWpState(_userSettings.Settings.AppFullscreen);
                        ChangeParralaxState(AppParallaxRulesEnum.Pause);
                    }
                    else {
                        //window is just in focus, not covering screen.
                        ChangeWpState(_userSettings.Settings.AppFocus);
                        ChangeParralaxState(AppParallaxRulesEnum.Pause);
                    }

                    if (!isDesktop && _userSettings.Settings.IsAudioOnlyOnDesktop) {
                        ChangeWpState(AppWpRunRulesEnum.Silence);
                    }
                }
                #endregion

                #region 多屏
                // 多屏
                else {
                    // 仅聚焦屏幕播放声音
                    Models.Cores.Monitor? focusedScreen;
                    Native.GetCursorPos(out var pos);
                    if ((focusedScreen = _monitorManger.GetMonitorByPoint(pos)) != null) {
                        //unpausing the rest of _wallpapers.
                        //only one window can be foreground!
                        foreach (var item in _monitorManger.Monitors) {
                            if (_userSettings.Settings.WallpaperArrangement != WallpaperArrangement.Expand &&
                                !focusedScreen.Equals(item)) {
                                ChangeWpState(AppWpRunRulesEnum.Silence, item);
                            }
                        }
                    }
                    else {
                        // 未获取到合法显示器
                        return;
                    }

                    // 检查前台窗口是否为桌面环境的一部分
                    if (isDesktop) {
                        // 焦点在桌面
                        //win10 and win7 desktop foreground while running.
                        ChangeWpState(AppWpRunRulesEnum.KeepRun);
                        ChangeParralaxState(AppParallaxRulesEnum.KeepRun);
                    }
                    // 说明在其他焦点应用程序上
                    else if (_userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Expand) {
                        // 跨越多屏                            
                        if (IsZoomedSpan(fHandle)) {
                            ChangeWpState(AppWpRunRulesEnum.Pause);
                        }
                        else //window is not greater >90%
                        {
                            ChangeWpState(_userSettings.Settings.AppFocus);
                        }
                        ChangeParralaxState(AppParallaxRulesEnum.Pause, focusedScreen);
                    }
                    else if (Native.IsZoomed(fHandle) || IsZoomedCustom(fHandle)) {
                        //maximised window or window covering whole screen.
                        ChangeWpState(_userSettings.Settings.AppFullscreen, focusedScreen);
                        ChangeParralaxState(AppParallaxRulesEnum.Pause, focusedScreen);
                    }
                    else {
                        //window is just in focus, not covering screen.
                        ChangeWpState(_userSettings.Settings.AppFocus, focusedScreen);
                        ChangeParralaxState(AppParallaxRulesEnum.Pause, focusedScreen);
                    }

                    if (!isDesktop && _userSettings.Settings.IsAudioOnlyOnDesktop) {
                        ChangeWpState(AppWpRunRulesEnum.Silence);
                    }
                }
                #endregion
            }
            catch (Exception ex) {
                ArcLog.GetLogger<Playback>().Error("Playback Changes for Focus Error: ", ex);
            }
            #endregion
        }

        private void PauseWallpapers() {
            foreach (var x in _wpControl.Wallpapers) {
                x.Pause();
            }
        }

        private void PlayWallpapers() {
            foreach (var x in _wpControl.Wallpapers) {
                x.Play();
            }
        }

        private void PauseParallaxs() {
            foreach (var x in _wpControl.Wallpapers) {
                x.PauseParallax();
            }
        }

        private void ResumeParallaxs() {
            foreach (var x in _wpControl.Wallpapers) {
                x.ResumeParallax();
            }
        }

        private void PauseWallpaper(IMonitor monitor) {
            foreach (var x in _wpControl.Wallpapers) {
                if (x.Monitor?.Equals(monitor) == true) {
                    x.Pause();
                }
            }
        }

        private void PlayWallpaper(IMonitor monitor) {
            foreach (var x in _wpControl.Wallpapers) {
                if (x.Monitor?.Equals(monitor) == true) {
                    x.Play();
                }
            }
        }

        private void PauseParallax(IMonitor monitor) {
            foreach (var x in _wpControl.Wallpapers) {
                if (x.Monitor?.Equals(monitor) == true) {
                    x.PauseParallax();
                }
            }
        }

        private void ResumeParallax(IMonitor monitor) {
            foreach (var x in _wpControl.Wallpapers) {
                if (x.Monitor?.Equals(monitor) == true) {
                    x.ResumeParallax();
                }
            }
        }

        private void SilenceWallpapers() {
            foreach (var x in _wpControl.Wallpapers) {
                x.SetMute(true);
            }
        }

        private void UnSilenceWallpapers() {
            foreach (var x in _wpControl.Wallpapers) {
                x.SetMute(false);
            }
        }

        private void SilenceWallpaper(IMonitor monitor) {
            foreach (var x in _wpControl.Wallpapers) {
                if (x.Monitor?.Equals(monitor) == true) {
                    x.SetMute(true);
                }
            }
        }

        private void UnSilenceWallpaper(IMonitor monitor) {
            foreach (var x in _wpControl.Wallpapers) {
                if (x.Monitor?.Equals(monitor) == true) {
                    x.SetMute(false);
                }
            }
        }

        public void ChangeParralaxState(AppParallaxRulesEnum nextState, IMonitor? targetMonitor = null) {
            switch (nextState) {
                case AppParallaxRulesEnum.KeepRun:
                    if (targetMonitor == null) {
                        ResumeParallaxs();
                    }
                    else {
                        ResumeParallax(targetMonitor);
                    }
                    break;
                case AppParallaxRulesEnum.Pause:
                    if (targetMonitor == null) {
                        PauseParallaxs();
                    }
                    else {
                        PauseParallax(targetMonitor);
                    }
                    break;
            }
        }

        private void ChangeWpState(AppWpRunRulesEnum nextState, IMonitor? targetMonitor = null) {
            switch (nextState) {
                case AppWpRunRulesEnum.KeepRun:
                    if (targetMonitor == null) {
                        PlayWallpapers();
                        UnSilenceWallpapers();
                    }
                    else {
                        PlayWallpaper(targetMonitor);
                        UnSilenceWallpaper(targetMonitor);
                    }
                    break;
                case AppWpRunRulesEnum.Silence:
                    if (targetMonitor == null) {
                        PlayWallpapers();
                        SilenceWallpapers();
                    }
                    else {
                        PlayWallpaper(targetMonitor);
                        SilenceWallpaper(targetMonitor);
                    }
                    break;
                case AppWpRunRulesEnum.Pause:
                    if (targetMonitor == null) {
                        PauseWallpapers();
                    }
                    else {
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
        private bool IsZoomedCustom(IntPtr hWnd) {
            try {
                Rectangle screenBounds;
                _ = Native.GetWindowRect(hWnd, out Native.RECT appBounds);
                screenBounds = _monitorManger.GetMonitorByHWnd(hWnd).Bounds;
                //If foreground app 95% working-area( -taskbar of _monitor)
                if ((appBounds.Bottom - appBounds.Top) >= screenBounds.Height * .95f && (appBounds.Right - appBounds.Left) >= screenBounds.Width * .95f)
                    return true;
                else
                    return false;
            }
            catch {
                return false;
            }
        }

        /// <summary>
        /// Checks if the hWnd dimension is spanned across All displays.
        /// </summary>
        /// <param name="hWnd"></param>
        /// <returns></returns>
        private bool IsZoomedSpan(IntPtr hWnd) {
            _ = Native.GetWindowRect(hWnd, out Native.RECT appBounds);
            var screenArea = _monitorManger.VirtualScreenBounds;
            // If foreground app 95% working-area( - taskbar of _monitor)
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
        private bool IsSystemLocked() {
            bool result = false;
            var fHandle = Native.GetForegroundWindow();
            try {
                _ = Native.GetWindowThreadProcessId(fHandle, out int processID);
                using (Process fProcess = Process.GetProcessById(processID)) {
                    result = fProcess.ProcessName.Equals("LockApp", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
            return result;
        }
        #endregion

        #region dispoae
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    Stop();
                    SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
                }
                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private readonly PeriodicTimer _timer;
        private bool _isLockScreen, _isRemoteSession;
        private CancellationTokenSource? _ctsPlayback;
        private readonly IUserSettingsService _userSettings;
        private readonly IWallpaperControl _wpControl;
        private readonly IMonitorManager _monitorManger;
        private readonly IScrControl _scrControl;
        private readonly IPowerService _powerService;

        public void InvokeRunPlayback() => RunPlayback();
        public void SimulateLockScreen(bool value) => _isLockScreen = value;
        public void SimulateRemoteSession(bool value) => _isRemoteSession = value;
    }
}
