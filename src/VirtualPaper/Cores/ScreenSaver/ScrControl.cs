using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Cores.WpControl;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Views.WindowsMsg;

namespace VirtualPaper.Cores.ScreenSaver {
    public partial class ScrControl : IScrControl {
        public Process? Proc { get; private set; }
        public bool IsRunning { get; private set; } = false;

        public ScrControl(
            IUserSettingsService userSettingsService,
            IWallpaperControl wpControl,
            RawInputMsgWindow msgWindow) {
            _userSettingsService = userSettingsService;
            _msgWindow = msgWindow;
            _wpControl = wpControl;

            _msgWindow.MouseMoveRaw += MsgWindow_MouseMoveRaw;
            _msgWindow.MouseDownRaw += MsgWindow_MouseDownRaw;
            _msgWindow.MouseUpRaw += MsgWindow_MouseUpRaw;
            _msgWindow.KeyboardClickRaw += MsgWindow_KeyboardClickRaw;

            _dispatcherTimer = new();

            foreach (var proc in userSettingsService.Settings.WhiteListScr) {
                _scrWhiteListProcState[proc.ProcName] = false;
            }
        }

        public void ChangeLockStatu(bool isLock) {
            _isRunningLock = isLock;
        }

        public void Stop() {
            StopTimeTask();
        }

        public void Start() {
            try {
                if (_isTiming || IsRunning) {
                    return;
                }

                _isRunningLock = _userSettingsService.Settings.IsRunningLock;

                StartTimerTask();
            }
            catch (Exception ex) {
                App.Log.Error("ScreenSaver started Error..." + ex.Message);
            }
        }

        public void AddToWhiteList(string procName) {
            _scrWhiteListProcState[procName] = false;
        }

        public void RemoveFromWhiteList(string procName) {
            if (_scrWhiteListProcState.ContainsKey(procName))
                _scrWhiteListProcState.Remove(procName, out _);
        }

        private void ResetTimer(string callback) {
            StopTimeTask();
            if (IsRunning) {
                StopProc();
            }
            if (_userSettingsService.Settings.IsScreenSaverOn) {
                StartTimerTask();
            }
        }

        private void StartTimerTask() {
            _dispatcherTimer.Interval = TimeSpan.FromMinutes(_userSettingsService.Settings.WaitingTime);
            _dispatcherTimer.Tick += DispatcherTimer_Tick;
            _dispatcherTimer.Start();
            _isTiming = true;
        }

        private void StopTimeTask() {
            _dispatcherTimer.Tick -= DispatcherTimer_Tick;
            _dispatcherTimer.Stop();
            _isTiming = false;
        }

        private void StopProc() {
            lock (_objStop) {
                if (_isStopping) return;
                _isStopping = true;

                SendMessage(new VirtualPaperCloseCmd());
                App.Log.Info("ScreenSaver was stoppped.");
                if (_isRunningLock) {
                    Native.LockWorkStation();
                }
            }
        }

        private void DispatcherTimer_Tick(object? sender, EventArgs e) {
            try {
                StopTimeTask();

                var tup = _wpControl.GetPrimaryWpFilePathRType();
                string? filePath = tup.Item1;
                string? rtype = tup.Item2.ToString();
                if (filePath == null || rtype == null) {
                    ResetTimer("Primary wallpaper was none.");
                    return;
                }

                if (Native.SHQueryUserNotificationState(out Native.QUERY_USER_NOTIFICATION_STATE state) == 0) {
                    switch (state) {
                        case Native.QUERY_USER_NOTIFICATION_STATE.QUNS_NOT_PRESENT:
                        case Native.QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY:
                        case Native.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN:
                        case Native.QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE:
                            ResetTimer("The foreground whitelist event is active");
                            return;
                    }
                }

                var hwnd = Native.GetForegroundWindow();
                _ = Native.GetWindowThreadProcessId(hwnd, out int processId);
                string procName = Process.GetProcessById(processId).ProcessName;

                if (_scrWhiteListProcState.ContainsKey(procName)) {
                    ResetTimer("The foreground whitelisting program is active");
                    return;
                }

                lock (_objStart) {
                    if (_isStarting || IsRunning) return;
                    _isStarting = true;

                    InitScr(filePath, rtype);

                    if (Proc == null) {
                        App.Log.Error("Run ScreenSaver failed...");
                        return;
                    }

                    Proc.Exited += Proc_Exited;
                    Proc.OutputDataReceived += Proc_OutputDataReceived;
                    Proc.Start();
                    App.Jobs.AddProcess(Proc.Id);
                    Proc.BeginOutputReadLine();

                    App.Log.Info("ScreenSaver is started.");
                }
            }
            catch (Exception ex) {
                App.Log.Error("ScreenSaver runtime Error..." + ex.Message);
                Terminate();
            }
        }

        private void InitScr(string filePath, string ftype) {
            if (filePath == null || ftype == null) return;

            string workingDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                Constants.WorkingDir.ScrSaver);

            StringBuilder cmdArgs = new();
            cmdArgs.Append($" --file-path {filePath}");
            cmdArgs.Append($" --wallpaper-type {ftype}");
            cmdArgs.Append($" --effect {_userSettingsService.Settings.ScreenSaverEffect.ToString()}");

            ProcessStartInfo start = new() {
                FileName = Path.Combine(
                    workingDir,
                    Constants.ModuleName.ScrSaver),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = workingDir,
                Arguments = cmdArgs.ToString(),
            };

            Process _process = new() {
                EnableRaisingEvents = true,
                StartInfo = start,
            };

            Proc = _process;
        }

        private void Terminate() {
            try {
                StopTimeTask();
                if (Proc != null) {
                    Proc.Kill();
                    Proc.Dispose();
                    App.Log.Info("Proc was Killed");
                }
            }
            catch { }
            finally {
                IsRunning = false;
                _isStopping = false;
            }
        }

        private void SendMessage(IpcMessage obj) {
            SendMessage(JsonSerializer.Serialize(obj, IpcMessageContext.Default.IpcMessage));
        }

        private void SendMessage(string msg) {
            try {
                Proc?.StandardInput.WriteLine(msg);
            }
            catch (Exception e) {
                App.Log.Error($"Stdin write fail: {e.Message}");
            }
        }

        private void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e) {
            if (!string.IsNullOrEmpty(e.Data)) {
                App.Log.Info($"ScreenSaver: {e.Data}");
                if (!IsRunning) {
                    IpcMessage obj;
                    try {
                        obj = JsonSerializer.Deserialize(e.Data, IpcMessageContext.Default.IpcMessage) ?? throw new("null msg recieved");
                    }
                    catch (Exception ex) {
                        App.Log.Error($"Ipcmessage parse Error: {ex.Message}");
                        return;
                    }

                    if (obj.Type == MessageType.msg_wploaded) {
                        IsRunning = true;
                        _isStarting = false;
                    }
                }
            }
        }

        private void Proc_Exited(object? sender, EventArgs e) {
            if (Proc != null) {
                Proc.OutputDataReceived -= Proc_OutputDataReceived;
            }
            Terminate();
        }

        #region raw input
        private void MsgWindow_KeyboardClickRaw(object? sender, KeyboardClickRawArgs e) {
            ResetTimer($"Keyboard was Clicked at : {e.Key}");
        }

        private void MsgWindow_MouseUpRaw(object? sender, MouseClickRawArgs e) {
            ResetTimer($"Mouse was uped at: {e.X} {e.Y} by {e.Button}");
        }

        private void MsgWindow_MouseDownRaw(object? sender, MouseClickRawArgs e) {
            ResetTimer($"Mouse was downed at: {e.X} {e.Y} by {e.Button}");
        }

        private void MsgWindow_MouseMoveRaw(object? sender, MouseRawArgs e) {
            ResetTimer($"Mouse was moved at: {e.X} {e.Y}");
        }
        #endregion

        #region dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                _msgWindow.MouseMoveRaw -= MsgWindow_MouseMoveRaw;
                _msgWindow.MouseDownRaw -= MsgWindow_MouseDownRaw;
                _msgWindow.MouseUpRaw -= MsgWindow_MouseUpRaw;
                _msgWindow.KeyboardClickRaw -= MsgWindow_KeyboardClickRaw;
                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private readonly IUserSettingsService _userSettingsService;
        private readonly RawInputMsgWindow _msgWindow;
        private readonly IWallpaperControl _wpControl;
        private readonly DispatcherTimer _dispatcherTimer;
        private readonly ConcurrentDictionary<string, bool> _scrWhiteListProcState = [];
        private readonly static object _objStop = new();
        private readonly static object _objStart = new();
        private bool _isRunningLock = false;
        private bool _isStopping = false;
        private bool _isStarting = false;
        private bool _isTiming = false;
    }
}
