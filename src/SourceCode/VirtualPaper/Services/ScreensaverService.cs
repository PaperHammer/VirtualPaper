using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Common.Utils.Shell;
using VirtualPaper.Cores.Desktop;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Views;
using VirtualPaper.Views.WindowsMsg;
using Application = System.Windows.Application;
using Timer = System.Timers.Timer;

namespace VirtualPaper.Services
{
    public class ScreensaverService : IScreensaverService
    {
        #region init
        public bool IsRunning { get; private set; } = false;

        public ScreensaverService(IUserSettingsService userSettings,
            IWallpaperControl desktopCore,
            IMonitorManager displayManager,
            RawInputMsgWindow rawInput)
        {
            this.userSettings = userSettings;
            this.desktopCore = desktopCore;
            this.displayManager = displayManager;
            this.rawInput = rawInput;

            _idleTimer.Elapsed += IdleCheckTimer;
            _idleTimer.Interval = 30000;
        }
        #endregion

        #region public
        public void Start()
        {
            if (!IsRunning)
            {
                //moving cursor outside screen..
                _ = Native.SetCursorPos(int.MaxValue, 0);
                _logger.Info("Starting _screensaver..");
                IsRunning = true;
                ShowScreensavers();
                //ShowBlankScreensavers();
                StartInputListener();
            }
        }

        public void Stop()
        {
            if (IsRunning)
            {
                _logger.Info("Stopping _screensaver..");
                IsRunning = false;
                StopInputListener();
                HideScreensavers();
                //CloseBlankScreensavers();

                if (userSettings.Settings.IsScreensaverLockOnResume)
                {
                    try
                    {
                        LockWorkStationSafe();
                    }
                    catch (Win32Exception e)
                    {
                        _logger.Error("Failed to lock pc: " + e.Message);
                    }
                }
            }
        }

        public void StartIdleTimer(uint idleTime)
        {
            if (idleTime == 0)
            {
                StopIdleTimer();
            }
            else
            {
                _logger.Info("Starting _screensaver idle wait {0}ms..", idleTime);
                _idleWaitTime = idleTime;
                _idleTimer.Start();
            }
        }

        public void StopIdleTimer()
        {
            if (_idleTimer.Enabled)
            {
                _logger.Info("Stopping _screensaver idle wait..");
                _idleTimer.Stop();
            }
        }

        /// <summary>
        /// 将屏幕保护程序预览附加到预览区域 <br>
        /// (To be run in UI thread.)</br>
        /// </summary>
        /// <param name="hwnd"></param>
        public void CreatePreview(IntPtr hwnd)
        {
            //Issue: Multiple display setup with diff dpi - making the window child affects DisplayMonitor offset values.
            if (IsRunning || displayManager.IsMultiScreen())
            {
                return;
            }

            //Verify if the hwnd is _screensaver demo area.
            const int maxChars = 256;
            StringBuilder className = new StringBuilder(maxChars);
            if (Native.GetClassName(hwnd, className, maxChars) > 0)
            {
                string cName = className.ToString();
                if (!string.Equals(cName, "SSDemoParent", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Info("Skipping ss preview, wrong hwnd class {0}.", cName);
                    return;
                }
            }
            else
            {
                _logger.Info("Skipping ss preview, failed to get hwnd class.");
                return;
            }

            _logger.Info("Showing ss preview..");
            var preview = new ScreenSaverPreview
            {
                ShowActivated = false,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -9999,
            };
            preview.Show();
            var previewHandle = new WindowInteropHelper(preview).Handle;
            //Set child of target.
            WindowUtil.SetParentSafe(previewHandle, hwnd);
            //Make this a child window so it will close when the parent dialog closes.
            Native.SetWindowLongPtr(new HandleRef(null, previewHandle),
                (int)Native.GWL.GWL_STYLE,
                new IntPtr(Native.GetWindowLong(previewHandle, (int)Native.GWL.GWL_STYLE) | Native.WindowStyles.WS_CHILD));
            //Get size of target.
            Native.GetClientRect(hwnd, out Native.RECT prct);
            //Update preview size and position.
            if (!Native.SetWindowPos(previewHandle, 1, 0, 0, prct.Right - prct.Left, prct.Bottom - prct.Top, 0x0010))
            {
                //TODO
            }
        }
        #endregion

        #region screensavers
        /// <summary>
        /// 从桌面工作程序中分离壁纸
        /// </summary>
        private void ShowScreensavers()
        {
            foreach (var item in desktopCore.Wallpapers)
            {
                //detach wallpaper.
                WindowUtil.SetParentSafe(item.Handle, IntPtr.Zero);
                //show on the currently running screen, not changing size.
                if (!Native.SetWindowPos(
                    item.Handle,
                    -1, //topmost
                    userSettings.Settings.WallpaperArrangement != WallpaperArrangement.Span ? item.Monitor.Bounds.Left : 0,
                    userSettings.Settings.WallpaperArrangement != WallpaperArrangement.Span ? item.Monitor.Bounds.Top : 0,
                    item.Monitor.Bounds.Width,
                    item.Monitor.Bounds.Height,
                    userSettings.Settings.WallpaperArrangement != WallpaperArrangement.Span ? 0x0040 : 0x0001)) //KeepRun WxH if Span
                {
                    _logger.Error(LogUtil.GetWin32Error("Screensaver show fail"));
                }
            }
        }

        /// <summary>
        /// 将壁纸重新附加到桌面工作程序
        /// </summary>
        private void HideScreensavers()
        {
            if (userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Span)
            {
                if (desktopCore.Wallpapers.Count > 0)
                {
                    //get spawned _workerW rectangle data.
                    _ = Native.GetWindowRect(desktopCore.DesktopWorkerW, out Native.RECT prct);
                    WindowUtil.SetParentSafe(desktopCore.Wallpapers[0].Handle, desktopCore.DesktopWorkerW);
                    //fill wp into the whole _workerW area.
                    if (!Native.SetWindowPos(desktopCore.Wallpapers[0].Handle, 1, 0, 0, prct.Right - prct.Left, prct.Bottom - prct.Top, 0x0010))
                    {
                        _logger.Error(LogUtil.GetWin32Error("Screensaver hide fail"));
                    }
                }
            }
            else
            {
                foreach (var item in desktopCore.Wallpapers)
                {
                    //update position & size incase window is moved.
                    if (!Native.SetWindowPos(item.Handle, 1, item.Monitor.Bounds.Left, item.Monitor.Bounds.Top, item.Monitor.Bounds.Width, item.Monitor.Bounds.Height, 0x0010))
                    {
                        //LogUtil.LogWin32Error("Failed to hide _screensaver(2)");
                    }
                    //re-calcuate position on desktop _workerW.
                    Native.RECT prct = new Native.RECT();
                    Native.MapWindowPoints(item.Handle, desktopCore.DesktopWorkerW, ref prct, 2);
                    //re-attach wallpaper to desktop.
                    WindowUtil.SetParentSafe(item.Handle, desktopCore.DesktopWorkerW);
                    //update position & size on desktop _workerW.
                    if (!Native.SetWindowPos(item.Handle, 1, prct.Left, prct.Top, item.Monitor.Bounds.Width, item.Monitor.Bounds.Height, 0x0010))
                    {
                        //LogUtil.LogWin32Error("Failed to hide _screensaver(3)");
                    }
                }
            }
            DesktopUtil.RefreshDesktop();
        }

        private void ShowBlankScreensavers()
        {
            if (!userSettings.Settings.IsScreensaverEmptyScreenShowBlack ||
                (userSettings.Settings.WallpaperArrangement == WallpaperArrangement.Span && desktopCore.Wallpapers.Count > 0))
            {
                return;
            }

            _ = Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
            {
                var freeScreens = displayManager.Monitors.ToList().FindAll(
                    x => !desktopCore.Wallpapers.Any(y => y.Monitor.Equals(x)));
                foreach (var item in freeScreens)
                {
                    var blankWindow = new Blank
                    {
                        Left = item.Bounds.Left,
                        Top = item.Bounds.Top,
                        Width = item.Bounds.Width,
                        Height = item.Bounds.Height,
                        //WindowStartupLocation = WindowStartupLocation.Manual,
                        //WindowState = WindowState.Maximized,
                        WindowStyle = WindowStyle.None,
                        Topmost = true,
                    };
                    //blankWindow.Loaded += (s, e) => { blankWindow.WindowState = WindowState.Maximized; };
                    blankWindow.Show();
                    blankWindows.Add(blankWindow);
                }
            }));
        }

        private void CloseBlankScreensavers()
        {
            _ = Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
            {
                blankWindows.ForEach(x => x.Close());
                blankWindows.Clear();
            }));
        }
        #endregion

        #region input checks
        private void IdleCheckTimer(object? sender, ElapsedEventArgs e)
        {
            try
            {
                if (GetLastInputTime() >= _idleWaitTime && !IsExclusiveFullScreenAppRunning())
                {
                    Start();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex.ToString());
                //StopIdleTimer();
            }
        }

        private void StartInputListener()
        {
            rawInput.MouseMoveRaw += RawInputHook_MouseMoveRaw;
            rawInput.MouseDownRaw += RawInputHook_MouseDownRaw;
            rawInput.KeyboardClickRaw += RawInputHook_KeyboardClickRaw;
        }

        private void StopInputListener()
        {
            rawInput.MouseMoveRaw -= RawInputHook_MouseMoveRaw;
            rawInput.MouseDownRaw -= RawInputHook_MouseDownRaw;
            rawInput.KeyboardClickRaw -= RawInputHook_KeyboardClickRaw;
        }

        private void RawInputHook_KeyboardClickRaw(object? sender, KeyboardClickRawArgs e)
        {
            Stop();
        }

        private void RawInputHook_MouseDownRaw(object? sender, MouseClickRawArgs e)
        {
            Stop();
        }

        private void RawInputHook_MouseMoveRaw(object? sender, MouseRawArgs e)
        {
            Stop();
        }
        #endregion

        #region helpers
        private static void LockWorkStationSafe()
        {
            if (!Native.LockWorkStation())
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        // Fails after 50 days (uint limit.)
        private static uint GetLastInputTime()
        {
            Native.LASTINPUTINFO lastInputInfo = new();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            lastInputInfo.dwTime = 0;

            uint envTicks = (uint)Environment.TickCount;

            if (Native.GetLastInputInfo(ref lastInputInfo))
            {
                uint lastInputTick = lastInputInfo.dwTime;

                return (envTicks - lastInputTick);
            }
            else
            {
                throw new Win32Exception("GetLastInputTime fail.");
            }
        }

        private static bool IsExclusiveFullScreenAppRunning()
        {
            if (Native.SHQueryUserNotificationState(out Native.QUERY_USER_NOTIFICATION_STATE state) == 0)
            {
                return state switch
                {
                    Native.QUERY_USER_NOTIFICATION_STATE.QUNS_NOT_PRESENT => false,
                    Native.QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY => false,
                    Native.QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE => false,
                    Native.QUERY_USER_NOTIFICATION_STATE.QUNS_ACCEPTS_NOTIFICATIONS => false,
                    Native.QUERY_USER_NOTIFICATION_STATE.QUNS_QUIET_TIME => false,
                    Native.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN => true,
                    _ => false,
                };
            }
            else
            {
                throw new Win32Exception("SHQueryUserNotificationState fail.");
            }
        }
        #endregion

        private uint _idleWaitTime = 300000;
        private readonly Timer _idleTimer = new();
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly List<Blank> blankWindows = [];
        private readonly IUserSettingsService userSettings;
        private readonly IWallpaperControl desktopCore;
        private readonly IMonitorManager displayManager;
        private readonly RawInputMsgWindow rawInput;

    }
}
