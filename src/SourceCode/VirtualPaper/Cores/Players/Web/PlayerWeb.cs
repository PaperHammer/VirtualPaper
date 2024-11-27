using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using NLog;
using VirtualPaper.Common;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Common.Utils.Shell;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Cores.Players.Web {
    internal class PlayerWeb : IWallpaperPlaying {
        public Process Proc { get; }

        public nint Handle { get; private set; }

        //public nint InputHandle { get; private set; }

        public IWpPlayerData Data { get; private set; }

        public IMonitor Monitor { get; set; }

        public bool IsExited { get; private set; }

        public bool IsLoaded { get; private set; } = false;

        public EventHandler? Closing { get; set; }

        public PlayerWeb(
            IWpPlayerData data,
            IMonitor monitor,
            bool isPreview) {
            //_isPreview = isPreview;

            string workingDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                Constants.WorkingDir.PlayerWeb);

            //string playingFile = data.RType switch {
            //    RuntimeType.RImage => Constants.PlayingFile.PlayerWeb,                
            //    RuntimeType.RImage3D => Constants.PlayingFile.PlayerWeb3D,
            //    RuntimeType.RVideo => Constants.PlayingFile.PlayerWeb,
            //    _ => string.Empty,
            //};

            //if (isPreview) {
            //    _previewerWeb = new(
            //        data.RType,
            //        data.FilePath,
            //        isCurrentWp ? data.WpEffectFilePathTemporary : data.WpEffectFilePathTemplate,
            //        playingFile);
            //    _previewerWeb.Reset += ResetPreviewer;
            //    return;
            //}

            _startParams = [data.FilePath, data.RType.ToString(), data.WpEffectFilePathUsing];
            StringBuilder cmdArgs = new();
            cmdArgs.Append($" --file-path {data.FilePath}");
            if (data.DepthFilePath != null && data.DepthFilePath != string.Empty) {
                cmdArgs.Append($" --depth-file-path {data.DepthFilePath}");
            }
            cmdArgs.Append($" --effect-file-path-using {data.WpEffectFilePathUsing}");
            cmdArgs.Append($" --effect-file-path-temporary {data.WpEffectFilePathTemporary}");
            cmdArgs.Append($" --effect-file-path-template {data.WpEffectFilePathTemplate}");
            cmdArgs.Append($" --runtime-type {data.RType.ToString()}");
            cmdArgs.Append($" --is-preview {isPreview}");
            cmdArgs.Append($" --window-style-type {App.IUserSettgins.Settings.SystemBackdrop}");
            cmdArgs.Append($" --app-theme {App.IUserSettgins.Settings.ApplicationTheme}");
            cmdArgs.Append($" --app-language {App.IUserSettgins.Settings.Language}");

            ProcessStartInfo start = new() {
                FileName = Path.Combine(
                    workingDir,
                    Constants.ModuleName.PlayerWeb),
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
            Data = data;
            Monitor = monitor;
            _uniqueId = _globalCount++;
        }

        public void Close() {
            SendMessage(new VirtualPaperCloseCmd());
        }

        //public void ClosePreview() {
        //    if (_previewerWeb != null) {
        //        _previewerWeb.Close();
        //        _previewerWeb = null;
        //    }
        //}

        public void Pause() {
            if (Data.RType == RuntimeType.RVideo)
                SendMessage(new VirtualPaperSuspendCmd());
        }

        public void Play() {
            if (Data.RType == RuntimeType.RVideo)
                SendMessage(new VirtualPaperResumeCmd());
        }

        public void PauseParallax() {
            SendMessage(new VirtualPaperParallaxSuspendCmd());
        }

        public void PlayParallax() {
            SendMessage(new VirtualPaperParallaxResumeCmd());
        }

        public Task ScreenCapture(string filePath) { return Task.FromResult(string.Empty); }

        public void SetMute(bool mute) {
            if (Data.RType == RuntimeType.RVideo)
                SendMessage(new VirtualPaperMutedCmd() { IsMuted = mute });
        }

        public void SetPlaybackPos(float pos, PlaybackPosType type) { }

        public async Task<bool> ShowAsync(CancellationToken token) {
            //if (_isPreview) {
            //    _previewerWeb?.Show();
            //    _previewerWeb?.Activate();
            //    return true;
            //}

            if (Proc is null)
                return false;

            try {
                token.ThrowIfCancellationRequested(); // 在开始前检查一次

                bool paraAvaliable = ValidateStartParameters();
                if (!paraAvaliable) {
                    throw new ArgumentException("缺少必要的启动参数");
                }

                Proc.Exited += Proc_Exited;
                Proc.OutputDataReceived += Proc_OutputDataReceived;
                Proc.Start();
                Proc.BeginOutputReadLine();

                using var registration = token.Register(() => {
                    _tcsProcessWait.TrySetCanceled();
                });
                token.ThrowIfCancellationRequested(); // 在结束前再检查一次

                await _tcsProcessWait.Task;
                if (_tcsProcessWait.Task.Result is not null)
                    throw _tcsProcessWait.Task.Result;
            }
            catch (OperationCanceledException ex) {
                _logger.Warn(ex);
                Terminate();
                throw;
            }
            catch (Exception ex) {
                _logger.Error(ex);
                Terminate();
                throw;
            }

            return true;
        }

        private bool ValidateStartParameters() {
            foreach (var para in _startParams)
                if (para == null || para.Length == 0)
                    return false;
            return true;
        }

        //public void Modify(string controlName, string propertyName, string value) {
        //    if (_previewerWeb == null) return;

        //    _previewerWeb.ModifySource(controlName, propertyName, value);
        //}

        public void Update(IWpPlayerData data) {
            this.Data = data;
            SendMessage(new VirtualPaperUpdateCmd() {
                WpType = data.RType.ToString(),
                FilePath = data.FilePath,
                WpEffectFilePathUsing = data.WpEffectFilePathUsing,
            });
        }

        //private void ResetPreviewer() {
        //    _previewerWeb = null;
        //}

        public void Stop() {
            Pause();
        }

        private void Terminate() {
            try {
                Closing?.Invoke(this, EventArgs.Empty);
                Proc?.Kill();
            }
            catch { }
            DesktopUtil.RefreshDesktop();
        }

        public void SendMessage(IpcMessage obj) {
            SendMessage(JsonSerializer.Serialize(obj));
        }

        private void SendMessage(string msg) {
            try {
                Proc?.StandardInput.WriteLine(msg);
            }
            catch (Exception e) {
                _logger.Error($"Stdin write fail: {e.Message}");
            }
        }

        private void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e) {
            //When the redirected stream is closed, a null line is sent to the event handler.
            if (!string.IsNullOrEmpty(e.Data)) {
                VirtualPaperMessageConsole messageConsole = JsonSerializer.Deserialize<VirtualPaperMessageConsole>(e.Data);
                if (messageConsole.MsgType == ConsoleMessageType.Error) {
                    _logger.Error($"PlayerWeb-{_uniqueId}: {messageConsole.Message}");
                }
                else {
                    _logger.Info($"PlayerWeb-{_uniqueId}: {e.Data}");
                }

                IpcMessage obj; 
                try {
                    obj = JsonSerializer.Deserialize<IpcMessage>(e.Data) ?? throw new("null msg recieved");
                }
                catch (Exception ex) {
                    _logger.Error($"Ipcmessage parse Error: {ex.Message}");
                    return;
                }
                if (!_isInitialized || !IsLoaded) {
                    if (obj.Type == MessageType.msg_hwnd) {
                        Exception? error = null;
                        try {
                            var handle = new IntPtr(((VirtualPaperMessageHwnd)obj).Hwnd);

                            var chrome_WidgetWin_0 = Native.FindWindowEx(handle, IntPtr.Zero, "Chrome_WidgetWin_0", null);
                            //var chrome_WidgetWin_0 = EnumerateChildWindows(handle, "Chrome_WidgetWin_0");
                            //if (!chrome_WidgetWin_0.Equals(IntPtr.Zero)) {
                            //    this.InputHandle = Native.FindWindowEx(chrome_WidgetWin_0, IntPtr.Zero, "Chrome_WidgetWin_1", null);
                            //}
                            Handle = Proc.GetProcessWindow(true);

                            if (IntPtr.Equals(Handle, IntPtr.Zero)) {
                                throw new Exception("Browser input/window handle NULL.");
                            }
                        }
                        catch (Exception ie) {
                            error = ie;
                        }
                        finally {
                            _isInitialized = true;
                            _tcsProcessWait.TrySetResult(error);
                        }
                    }
                    else if (obj.Type == MessageType.msg_wploaded) {
                        IsLoaded = true;
                    }
                }
                else {
                    if (obj.Type == MessageType.cmd_preview_off) {
                        this.Closing?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        private static IntPtr EnumerateChildWindows(IntPtr parentHandle, string targetClassName) {
            IntPtr childHandle = Native.FindWindowEx(parentHandle, IntPtr.Zero, null, null); // 开始枚举第一个子窗口

            while (childHandle != IntPtr.Zero) {
                // 如果找到了目标窗口，返回其句柄
                var targetHandel = Native.FindWindowEx(childHandle, IntPtr.Zero, targetClassName, null);
                if (targetHandel != IntPtr.Zero)
                    return targetHandel;

                // 否则，递归检查此子窗口的子窗口
                IntPtr resultFromChild = EnumerateChildWindows(childHandle, targetClassName);
                if (resultFromChild != IntPtr.Zero)
                    return resultFromChild;

                // 移动到下一个兄弟窗口
                childHandle = Native.FindWindowEx(parentHandle, childHandle, null, null);
            }

            // 如果没有找到，返回IntPtr.Zero
            return IntPtr.Zero;
        }

        private void Proc_Exited(object? sender, EventArgs e) {
            _tcsProcessWait.TrySetResult(null);
            Proc.OutputDataReceived -= Proc_OutputDataReceived;
            Proc?.Dispose();
            DesktopUtil.RefreshDesktop();
            IsExited = true;
        }

        //private async Task<Exception?> WaitForProcessWithTimeoutAsync()
        //{
        //    var cancellationTokenSource = new CancellationTokenSource(_timeout);

        //    try
        //    {
        //        // 创建一个任务用于等待进程退出
        //        var processWaitTask = Task.Run(() => Proc?.WaitForExitAsync(cancellationTokenSource.Token), cancellationTokenSource.Token);

        //        // 使用 WhenAny 等待进程结束或超时
        //        var completedTask = await Task.WhenAny(processWaitTask, Task.Delay(_timeout));

        //        // 如果是进程结束的任务先完成
        //        if (completedTask == processWaitTask)
        //        {
        //            // 获取进程退出代码并决定是否抛出异常
        //            int exitCode = Proc.ExitCode;
        //            if (exitCode != 0)
        //            {
        //                return new Exception($"The process ends with a non-zero exit code {exitCode}");
        //            }
        //            return null;
        //        }
        //        else
        //        {
        //            // 超时则取消进程并抛出异常
        //            cancellationTokenSource.Cancel();
        //            Proc?.Kill();
        //            return new TimeoutException("Process start timeout");
        //        }
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        // 当取消令牌被触发时（即超时）
        //        Proc?.Kill();
        //        return new TimeoutException("Process start timeout");
        //    }
        //    catch (Exception)
        //    {
        //        Proc?.Kill();
        //        return new TimeoutException("An Error occurred");
        //    }
        //}

        private readonly List<string> _startParams;
        //private PreviewerWeb? _previewerWeb;
        //private readonly bool _isPreview;
        private readonly TaskCompletionSource<Exception> _tcsProcessWait = new();
        private static int _globalCount;
        private readonly int _uniqueId;
        private bool _isInitialized;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        //private readonly int _timeout = 50000;
    }
}
