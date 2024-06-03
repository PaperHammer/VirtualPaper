using NLog;
using System.Windows;
using System.Windows.Interop;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Cores.Desktop;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.lang;
using MessageBox = System.Windows.MessageBox;

namespace VirtualPaper.Views.WindowsMsg
{
    /// <summary>
    /// WndProcMsgWindow 实现对系统事件或行为的自定义响应
    /// </summary>
    public partial class WndProcMsgWindow : Window
    {
        public WndProcMsgWindow(
            IMonitorManager monitorManager, 
            IWallpaperControl desktopWpControl)
        {
            _monitorManager = monitorManager;
            _wpControl = desktopWpControl;

            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = PresentationSource.FromVisual(this) as HwndSource;
            source?.AddHook(WndProc);
        }

        /// <summary>
        /// 当窗口接收到任何 Windows 消息时，都会首先经过 WndProc 函数进行处理
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <param name="handled"></param>
        /// <returns></returns>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_TASKBARCREATED)
            {
                _logger.Info("WM_TASKBARCREATED: New taskbar created.");
                int newExplorerPid = GetTaskbarExplorerPid();
                if (prevExplorerPid != newExplorerPid)
                {
                    //Explorer crash detection, dpi change also sends WM_TASKBARCREATED..
                    _logger.Info($"Explorer crashed, pid mismatch: {prevExplorerPid} != {newExplorerPid}");
                    if ((DateTime.Now - prevCrashTime).TotalSeconds > 30)
                    {
                        _ = _wpControl.ResetWallpaperAsync();
                    }
                    else
                    {
                        _logger.Warn("Explorer restarted multiple times in the last 30s.");
                        _ = Task.Run(() => MessageBox.Show(LanguageManager.Instance["WndProcMsg_DescExplorerCrash"],
                                $"{LanguageManager.Instance["WndProcMsg_TitleAppName"]} - {LanguageManager.Instance["WndProcMsg_TextError"]}",
                                MessageBoxButton.OK, MessageBoxImage.Error));
                        _wpControl.CloseAllWallpapers();
                        _ = _wpControl.ResetWallpaperAsync();
                    }
                    prevCrashTime = DateTime.Now;
                    prevExplorerPid = newExplorerPid;
                }
            }
            else if (msg == (uint)Native.WM.QUERYENDSESSION)
            {
                if (lParam != IntPtr.Zero && lParam == (IntPtr)0x00000001) // ENDSESSION_CLOSEAPP
                {
                    //The app is being queried if it can close for an update.
                    _ = Native.RegisterApplicationRestart(
                        string.Empty,
                        (int)Native.RestartFlags.RESTART_NO_CRASH |
                        (int)Native.RestartFlags.RESTART_NO_HANG |
                        (int)Native.RestartFlags.RESTART_NO_REBOOT);

                    return (IntPtr)1;
                }
            }
            else if (msg == (uint)Native.WM.ENDSESSION)
            {
                //Gracefully close app.
                App.ShutDown();
            }

            //Monitor message processing...
            _ = _monitorManager.OnWndProc(hwnd, (uint)msg, wParam, lParam);

            return IntPtr.Zero;
        }

        #region helpers
        /// <summary>
        /// 获取 Windows 操作系统任务栏（Shell Tray）所关联的 explorer.exe 进程的进程标识符（PID）
        /// </summary>
        /// <returns></returns>
        private static int GetTaskbarExplorerPid()
        {
            _ = Native.GetWindowThreadProcessId(Native.FindWindow("Shell_TrayWnd", null), out int pid);
            return pid;
        }
        #endregion

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly int WM_TASKBARCREATED = Native.RegisterWindowMessage("TaskbarCreated");
        private int prevExplorerPid = GetTaskbarExplorerPid();
        private DateTime prevCrashTime = DateTime.MinValue;

        private readonly IMonitorManager _monitorManager;
        private readonly IWallpaperControl _wpControl;
    }
}
