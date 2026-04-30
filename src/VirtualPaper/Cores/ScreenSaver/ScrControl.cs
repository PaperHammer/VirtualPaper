using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Cores.WpControl;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Utils.Interfcaes;
using VirtualPaper.Views.WindowsMsg;

namespace VirtualPaper.Cores.ScreenSaver {
    public partial class ScrControl : IScrControl {
        //public Process? Proc { get; private set; }
        public bool IsRunning { get; private set; } = false;

        public ScrControl(
            IUserSettingsService userSettingsService,
            IWallpaperControl wpControl,
            IRawInputMsg msgWindow,
            IDispatcherTimer dispatcherTimer,
            INativeService nativeService,
            IProcessLauncher processLauncher,
            IJobService jobService) {
            _userSettingsService = userSettingsService;
            _msgWindow = msgWindow;
            _wpControl = wpControl;
            _nativeService = nativeService;
            _processLauncher = processLauncher;
            _jobService = jobService;

            _msgWindow.MouseMoveRaw += MsgWindow_MouseMoveRaw;
            _msgWindow.MouseDownRaw += MsgWindow_MouseDownRaw;
            _msgWindow.MouseUpRaw += MsgWindow_MouseUpRaw;
            _msgWindow.KeyboardClickRaw += MsgWindow_KeyboardClickRaw;

            _dispatcherTimer = dispatcherTimer;

            foreach (var proc in userSettingsService.Settings.WhiteListScr) {
                _scrWhiteListProcState[proc.ProcName] = false;
            }
        }

        public void Start() {
            if (_isTiming || IsRunning) return;

            try {
                _isRunningLock = _userSettingsService.Settings.IsRunningLock;
                StartTimerTask();
            }
            catch (Exception ex) {
                ArcLog.GetLogger<ScrControl>().Error("ScreenSaver started Error: " + ex.Message);
            }
        }

        public void Stop() {
            StopTimerTask();
        }

        public void ChangeLockStatu(bool isLock) {
            _isRunningLock = isLock;
        }

        public void AddToWhiteList(string procName) {
            _scrWhiteListProcState[procName] = false;
        }

        public void RemoveFromWhiteList(string procName) {
            _scrWhiteListProcState.Remove(procName, out _);
        }

        // -------------------------------------------------------------------------
        // Timer
        // -------------------------------------------------------------------------

        /// <summary>
        /// 启动倒计时，倒计时结束后触发屏保启动
        /// </summary>
        private void StartTimerTask() {
            _dispatcherTimer.Interval = TimeSpan.FromMinutes(_userSettingsService.Settings.WaitingTime);
            _dispatcherTimer.Tick += DispatcherTimer_Tick;
            _dispatcherTimer.Start();
            _isTiming = true;
        }

        /// <summary>
        /// 停止倒计时
        /// </summary>
        private void StopTimerTask() {
            _dispatcherTimer.Tick -= DispatcherTimer_Tick;
            _dispatcherTimer.Stop();
            _isTiming = false;
        }

        /// <summary>
        /// 重置计时器：停止当前计时（若进程在运行则一并停止），再重新开始计时。
        /// 由用户输入、白名单检测等场景触发。
        /// </summary>
        private void ResetTimer(string reason) {
            ArcLog.GetLogger<ScrControl>().Info($"ResetTimer: {reason}");

            StopTimerTask();

            if (IsRunning) {
                StopProc();
                // 计时器重启由 Proc_Exited → RestartTimerAfterExit 完成
                return;
            }

            // 进程未运行，直接重启计时器
            if (_userSettingsService.Settings.IsScreenSaverOn) {
                StartTimerTask();
            }
        }

        // -------------------------------------------------------------------------
        // Process: Start
        // -------------------------------------------------------------------------

        private void DispatcherTimer_Tick(object? sender, EventArgs e) {
            // 计时结束，先停掉计时器再启动屏保
            StopTimerTask();

            try {
                // 检查壁纸
                var (filePath, runtimeType) = _wpControl.GetPrimaryWpFilePathRType();
                if (filePath == null) {
                    ArcLog.GetLogger<ScrControl>().Info("Primary wallpaper was none, reset timer.");
                    RestartTimer();
                    return;
                }

                // 检查系统通知状态（全屏/演示/忙碌时不启动屏保）
                if (_nativeService.SHQueryUserNotificationState(out var state) == 0) {
                    switch (state) {
                        case Native.QUERY_USER_NOTIFICATION_STATE.QUNS_NOT_PRESENT:
                        case Native.QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY:
                        case Native.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN:
                        case Native.QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE:
                            ArcLog.GetLogger<ScrControl>().Info($"System notification state [{state}], reset timer.");
                            RestartTimer();
                            return;
                    }
                }

                // 检查白名单
                var hwnd = _nativeService.GetForegroundWindow();
                _nativeService.GetWindowThreadProcessId(hwnd, out int processId);
                string procName = _nativeService.GetProcessNameById(processId);
                if (_scrWhiteListProcState.ContainsKey(procName)) {
                    ArcLog.GetLogger<ScrControl>().Info($"Whitelisted process [{procName}] is active, reset timer.");
                    RestartTimer();
                    return;
                }

                // 启动屏保进程
                LaunchProc(filePath, runtimeType.ToString());
            }
            catch (Exception ex) {
                ArcLog.GetLogger<ScrControl>().Error("ScreenSaver runtime Error: " + ex.Message);
                // 启动失败，清理并重新计时
                CleanupProc();
                RestartTimer();
            }
        }

        private void LaunchProc(string filePath, string rtype) {
            lock (_objStart) {
                if (_isStarting || IsRunning) return;
                _isStarting = true;
            }

            var startInfo = BuildStartInfo(filePath, rtype);

            _processLauncher.Exited += Proc_Exited;
            _processLauncher.OutputDataReceived += Proc_OutputDataReceived;
            _processLauncher.Launch(startInfo);
            _jobService.AddProcess(_processLauncher.ProcessId);
            _processLauncher.BeginOutputReadLine();

            ArcLog.GetLogger<ScrControl>().Info("ScreenSaver launched.");
        }

        // -------------------------------------------------------------------------
        // Process: Stop
        // -------------------------------------------------------------------------

        /// <summary>
        /// 主动停止屏保进程：发送关闭消息，进程退出后由 Proc_Exited 完成清理和重启计时。
        /// </summary>
        private void StopProc() {
            lock (_objStop) {
                if (_isStopping) return;
                _isStopping = true;
            }

            ArcLog.GetLogger<ScrControl>().Info("Requesting ScreenSaver stop...");
            SendMessage(new VirtualPaperCloseCmd());

            if (_isRunningLock) {
                _nativeService.LockWorkStation();
            }
        }

        /// <summary>
        /// 进程退出事件：清理资源，并根据配置决定是否重启计时器。
        /// 无论是主动 Stop 还是意外退出，都走这里。
        /// </summary>
        private void Proc_Exited(object? sender, EventArgs e) {
            _processLauncher.OutputDataReceived -= Proc_OutputDataReceived;
            _processLauncher.Exited -= Proc_Exited;

            CleanupProc();
            RestartTimerAfterExit();
        }

        /// <summary>
        /// 清理进程资源，重置运行状态。
        /// </summary>
        private void CleanupProc() {
            try {
                _processLauncher.Kill();
                _processLauncher.Dispose();
                ArcLog.GetLogger<ScrControl>().Info("ScreenSaver process cleaned up.");
            }
            catch (Exception ex) {
                ArcLog.GetLogger<ScrControl>().Error("CleanupProc Error: " + ex.Message);
            }
            finally {
                IsRunning = false;
                _isStarting = false;
                _isStopping = false;
            }
        }

        // -------------------------------------------------------------------------
        // Timer Restart Helpers
        // -------------------------------------------------------------------------

        /// <summary>
        /// 进程退出后重启计时器（用于 Proc_Exited 场景）。
        /// </summary>
        private void RestartTimerAfterExit() {
            if (_userSettingsService.Settings.IsScreenSaverOn) {
                StartTimerTask();
            }
        }

        /// <summary>
        /// 直接重启计时器（用于 Tick 内部检查未通过的场景）。
        /// </summary>
        private void RestartTimer() {
            if (_userSettingsService.Settings.IsScreenSaverOn) {
                StartTimerTask();
            }
        }

        // -------------------------------------------------------------------------
        // IPC
        // -------------------------------------------------------------------------

        private void Proc_OutputDataReceived(object? sender, ProcessOutputEventArgs e) {
            if (string.IsNullOrEmpty(e.Data)) return;

            ArcLog.GetLogger<ScrControl>().Info($"ScreenSaver: {e.Data}");

            if (IsRunning) return;

            try {
                var obj = JsonSerializer.Deserialize(e.Data, IpcMessageContext.Default.IpcMessage)
                          ?? throw new Exception("null msg received");

                if (obj.Type == MessageType.msg_wploaded) {
                    IsRunning = true;
                    _isStarting = false;
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<ScrControl>().Error($"IpcMessage parse Error: {ex.Message}");
            }
        }

        private void SendMessage(IpcMessage obj) {
            SendMessage(JsonSerializer.Serialize(obj, IpcMessageContext.Default.IpcMessage));
        }

        private void SendMessage(string msg) {
            try {
                _processLauncher.WriteStdin(msg);
            }
            catch (Exception e) {
                ArcLog.GetLogger<ScrControl>().Error($"Stdin write fail: {e.Message}");
            }
        }

        // -------------------------------------------------------------------------
        // Raw Input → ResetTimer
        // -------------------------------------------------------------------------

        private void MsgWindow_KeyboardClickRaw(object? sender, KeyboardClickRawArgs e)
            => ResetTimer($"Keyboard clicked: {e.Key}");

        private void MsgWindow_MouseUpRaw(object? sender, MouseClickRawArgs e)
            => ResetTimer($"Mouse up: {e.X},{e.Y} {e.Button}");

        private void MsgWindow_MouseDownRaw(object? sender, MouseClickRawArgs e)
            => ResetTimer($"Mouse down: {e.X},{e.Y} {e.Button}");

        private void MsgWindow_MouseMoveRaw(object? sender, MouseRawArgs e)
            => ResetTimer($"Mouse move: {e.X},{e.Y}");

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private ProcessStartInfo BuildStartInfo(string filePath, string ftype) {
            string workingDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                Constants.WorkingDir.ScrSaver);

            var cmdArgs = new StringBuilder()
                .Append($" --file-path {filePath}")
                .Append($" --wallpaper-type {ftype}")
                .Append($" --effect {_userSettingsService.Settings.ScreenSaverEffect}");

            return new ProcessStartInfo {
                FileName = Path.Combine(workingDir, Constants.ModuleName.ScrSaver),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = workingDir,
                Arguments = cmdArgs.ToString(),
            };
        }

        #region dispose
        private bool _isDisposed;

        protected virtual void Dispose(bool disposing) {
            if (_isDisposed) return;

            _msgWindow.MouseMoveRaw -= MsgWindow_MouseMoveRaw;
            _msgWindow.MouseDownRaw -= MsgWindow_MouseDownRaw;
            _msgWindow.MouseUpRaw -= MsgWindow_MouseUpRaw;
            _msgWindow.KeyboardClickRaw -= MsgWindow_KeyboardClickRaw;

            _isDisposed = true;
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private readonly IProcessLauncher _processLauncher;
        private readonly IJobService _jobService;
        private readonly IUserSettingsService _userSettingsService;
        private readonly IRawInputMsg _msgWindow;
        private readonly IWallpaperControl _wpControl;
        private readonly INativeService _nativeService;
        private readonly IDispatcherTimer _dispatcherTimer;
        private readonly ConcurrentDictionary<string, bool> _scrWhiteListProcState = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _objStop = new();
        private readonly object _objStart = new();
        private bool _isRunningLock = false;
        private bool _isStopping = false;
        private bool _isStarting = false;
        private bool _isTiming = false;
    }
}
