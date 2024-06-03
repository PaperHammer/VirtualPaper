using Newtonsoft.Json;
using NLog;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Threading;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Models.WallpaperMetaData;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Views.WindowsMsg;

namespace VirtualPaper.Cores.ScreenSaver
{
    public class ScrControl : IScrControl
    {
        public Process? Proc { get; private set; }
        public bool IsRunning { get; private set; } = false;

        public ScrControl(
            IUserSettingsService userSettingsService,
            IWatchdogService watchdogService,
            RawInputMsgWindow msgWindow)
        {
            _userSettingsService = userSettingsService;
            _watchdog = watchdogService;
            _msgWindow = msgWindow;

            _msgWindow.MouseMoveRaw += MsgWindow_MouseMoveRaw;
            _msgWindow.MouseDownRaw += MsgWindow_MouseDownRaw;
            _msgWindow.MouseUpRaw += MsgWindow_MouseUpRaw;
            _msgWindow.KeyboardClickRaw += MsgWindow_KeyboardClickRaw;

            _dispatcherTimer = new()
            {
                Interval = TimeSpan.FromMinutes(_waitingTime),
            };

            foreach (var proc in userSettingsService.Settings.WhiteListScr)
            {
                _scrWhiteListProcState[proc.ProcName] = false;
            }
        }

        public void ChangeLockStatu(bool isLock)
        {
            _isRunningLock = isLock;
        }

        public void Stop()
        {
            StopTimeTask();
        }

        public void Start(IMetaData metaData)
        {
            try
            {
                _metaData = metaData;
                _waitingTime = _userSettingsService.Settings.WaitingTime;
                _isRunningLock = _userSettingsService.Settings.IsRunningLock;

                StartTimerTask();
            }
            catch (Exception ex)
            {
                _logger.Error("ScreenSaver started error..." + ex.Message);
            }
        }

        public void AddToWhiteList(string procName)
        {
            _scrWhiteListProcState[procName] = false;
        }

        public void RemoveFromWhiteList(string procName)
        {
            if (_scrWhiteListProcState.ContainsKey(procName))
                _scrWhiteListProcState.Remove(procName, out _);
        }

        private void ResetTimer(string callback)
        {
            StopTimeTask();
            if (IsRunning)
            {
                StopProc();
            }
            if (_userSettingsService.Settings.IsScreenSaverOn)
            {
                StartTimerTask();
            }
        }

        private void StartTimerTask()
        {
            if (_metaData == null) return;

            _dispatcherTimer.Tick += DispatcherTimer_Tick;
            _dispatcherTimer.Start();
        }

        private void StopTimeTask()
        {
            _dispatcherTimer.Tick -= DispatcherTimer_Tick;
            _dispatcherTimer.Stop();
        }

        private void StopProc()
        {
            lock (_objStop)
            {
                if (_isStopping) return;
                _isStopping = true;

                SendMessage(new VirtualPaperCloseCmd());
                _logger.Info("ScreenSaver was stoppped.");
                if (_isRunningLock)
                {
                    Native.LockWorkStation();
                }
            }
            //else
            //{
            //    if (Proc == null)
            //    {
            //        _semaphoreScrStop.Release();
            //    }
            //}
        }

        private void InitScr(IMetaData metaData)
        {
            StringBuilder cmdArgs = new();
            cmdArgs.Append($" --file-path {metaData.FilePath}");
            cmdArgs.Append(" --wallpaper-type " + metaData.Type.ToString());
            cmdArgs.Append(" --effect " + _userSettingsService.Settings.ScreenSaverEffect.ToString());

            ProcessStartInfo start = new()
            {
                FileName = _fileName,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = _workingDir,
                Arguments = cmdArgs.ToString(),
            };

            Process _process = new()
            {
                EnableRaisingEvents = true,
                StartInfo = start,
            };

            Proc = _process;
        }

        private void DispatcherTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                StopTimeTask();

                if (_metaData == null)
                {
                    ResetTimer("");
                    return;
                }

                if (Native.SHQueryUserNotificationState(out Native.QUERY_USER_NOTIFICATION_STATE state) == 0)
                {
                    switch (state)
                    {
                        case Native.QUERY_USER_NOTIFICATION_STATE.QUNS_NOT_PRESENT:
                        case Native.QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY:
                        case Native.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN:
                        case Native.QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE:
                            ResetTimer("");
                            return;
                    }
                }

                var hwnd = Native.GetForegroundWindow();
                _ = Native.GetWindowThreadProcessId(hwnd, out int processId);
                string procName = Process.GetProcessById(processId).ProcessName;

                if ( _scrWhiteListProcState.ContainsKey(procName))
                {
                    ResetTimer("");
                    return;
                }

                lock (_objStart)
                {
                    if (_isStarting || IsRunning) return;
                    _isStarting = true;

                    InitScr(_metaData);

                    if (Proc == null)
                    {
                        _logger.Error("Run ScreenSaver failed...");
                        return;
                    }

                    Proc.Exited += Proc_Exited;
                    Proc.OutputDataReceived += Proc_OutputDataReceived;
                    Proc.Start();
                    Proc.BeginOutputReadLine();

                    _watchdog.Add(Proc.Id);
                    _logger.Info("ScreenSaver is started.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("ScreenSaver runtime error..." + ex.Message);
                Terminate();
            }
        }

        private void Terminate()
        {
            try
            {
                StopTimeTask();
                if (Proc != null)
                {
                    _watchdog.Remove(Proc.Id);
                    Proc.Kill();
                    Proc.Dispose();
                    _logger.Info("Proc was Killed");
                }
            }
            catch { }
            finally
            {
                IsRunning = false;
                _isStopping = false;
            }
        }

        private void SendMessage(IpcMessage obj)
        {
            SendMessage(JsonConvert.SerializeObject(obj));
        }

        private void SendMessage(string msg)
        {
            try
            {
                Proc?.StandardInput.WriteLine(msg);
            }
            catch (Exception e)
            {
                _logger.Error($"Stdin write fail: {e.Message}");
            }
        }

        private void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logger.Info($"ScreenSaver: {e.Data}");
                if (!IsRunning)
                {
                    IpcMessage obj;
                    try
                    {
                        obj = JsonConvert.DeserializeObject<IpcMessage>(e.Data, new JsonSerializerSettings() { Converters = { new IpcMessageConverter() } }) ?? throw new("null msg recieved");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Ipcmessage parse Error: {ex.Message}");
                        return;
                    }

                    if (obj.Type == MessageType.msg_wploaded)
                    {
                        IsRunning = true;
                        _isStarting = false;
                    }
                }
            }
        }

        private void Proc_Exited(object? sender, EventArgs e)
        {
            if (Proc != null)
            {
                Proc.OutputDataReceived -= Proc_OutputDataReceived;
            }
            Terminate();
        }

        #region raw input
        private void MsgWindow_KeyboardClickRaw(object? sender, KeyboardClickRawArgs e)
        {
            ResetTimer($"Keyboard was Clicked at : {e.Key}");
        }

        private void MsgWindow_MouseUpRaw(object? sender, MouseClickRawArgs e)
        {
            ResetTimer($"Mouse was uped at: {e.X} {e.Y} by {e.Button}");
        }

        private void MsgWindow_MouseDownRaw(object? sender, MouseClickRawArgs e)
        {
            ResetTimer($"Mouse was downed at: {e.X} {e.Y} by {e.Button}");
        }

        private void MsgWindow_MouseMoveRaw(object? sender, MouseRawArgs e)
        {
            ResetTimer($"Mouse was moved at: {e.X} {e.Y}");
        }
        #endregion

        #region dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                _msgWindow.MouseMoveRaw -= MsgWindow_MouseMoveRaw;
                _msgWindow.MouseDownRaw -= MsgWindow_MouseDownRaw;
                _msgWindow.MouseUpRaw -= MsgWindow_MouseUpRaw;
                _msgWindow.KeyboardClickRaw -= MsgWindow_KeyboardClickRaw;
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
        private IUserSettingsService _userSettingsService;
        private IWatchdogService _watchdog;
        private RawInputMsgWindow _msgWindow;
        private IMetaData? _metaData;
        private int _waitingTime = 1; // 分钟(minutes) default
        private DispatcherTimer _dispatcherTimer;
        private bool _isRunningLock = false;
        private readonly ConcurrentDictionary<string, bool> _scrWhiteListProcState = [];

        private static object _objStop = new();
        private static object _objStart = new();
        private bool _isStopping = false;
        private bool _isStarting = false;
        private readonly string _workingDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "ScrSaver");
        private readonly string _fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "ScrSaver", "VirtualPaper.ScreenSaver.exe");
    }
}
