using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.Shell;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Cores.Players.Web {
    internal partial class PlayerWeb : IWpPlayer {
        public Process Proc { get; private set; }
        public nint Handle { get; private set; }
        public IWpPlayerData Data { get; private set; }
        public IMonitor Monitor { get; set; }
        public bool IsExited { get; private set; }
        public bool IsLoaded { get; private set; }
        public bool IsPreview { get; private set; }
        public EventHandler? Closing { get; set; }
        public EventHandler? Apply { get; set; }

        public PlayerWeb(
            IWpPlayerData data,
            IMonitor monitor,
            bool isPreview) {
            string workingDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                Constants.WorkingDir.PlayerWeb);

            StringBuilder cmdArgs = new();
            CheckParams(data);
            IsPreview = isPreview;
            if (isPreview) {
                cmdArgs.Append($" --is-preview");
            }
            else {
                cmdArgs.Append($" --left {monitor.WorkingArea.Left}");
                cmdArgs.Append($" --top {monitor.WorkingArea.Top}");
                cmdArgs.Append($" --right {monitor.WorkingArea.Right}");
                cmdArgs.Append($" --bottom {monitor.WorkingArea.Bottom}");
            }

            cmdArgs.Append($" -f {data.FilePath}");
            cmdArgs.Append($" -b {Path.Combine(data.FolderPath, Constants.Field.WpBasicDataFileName)}");
            if (data.RType == RuntimeType.RImage3D) {
                cmdArgs.Append($" --depth-file-path {data.DepthFilePath}");
            }
            cmdArgs.Append($" -e {data.WpEffectFilePathUsing}");
            cmdArgs.Append($" --effect-file-path-temporary {data.WpEffectFilePathTemporary}");
            cmdArgs.Append($" --effect-file-path-template {data.WpEffectFilePathTemplate}");
            cmdArgs.Append($" -r {data.RType.ToString()}");
            cmdArgs.Append($" --window-style-type {App.IUserSettgins.Settings.SystemBackdrop}");
            cmdArgs.Append($" -t {App.IUserSettgins.Settings.ApplicationTheme}");
            cmdArgs.Append($" -l {App.IUserSettgins.Settings.Language}");

            ProcessStartInfo start = new() {
                FileName = Path.Combine(
                    workingDir,
                    Constants.ModuleName.PlayerWeb),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = workingDir,
                WindowStyle = ProcessWindowStyle.Minimized,
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

        private static void CheckParams(IWpPlayerData data) {
            if (string.IsNullOrEmpty(data.FilePath) ||
                string.IsNullOrEmpty(data.WpEffectFilePathUsing) ||
                string.IsNullOrEmpty(data.WpEffectFilePathTemplate) ||
                string.IsNullOrEmpty(data.WpEffectFilePathTemporary) ||
                data.RType == RuntimeType.RImage3D && string.IsNullOrEmpty(data.DepthFilePath)) {
                throw new Exception("启动 Player 时, 缺少必要参数");
            }
        }

        public void Close() {
            SendMessage(new VirtualPaperCloseCmd());
            Closing?.Invoke(this, EventArgs.Empty);
        }

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
            if (Proc is null)
                return false;

            try {
                token.ThrowIfCancellationRequested(); // 在开始前检查一次

                Proc.Exited += Proc_Exited;
                Proc.OutputDataReceived += Proc_OutputDataReceived;
                Proc.Start();
                App.Jobs.AddProcess(Proc.Id);
                Proc.BeginOutputReadLine();

                using var registration = token.Register(() => {
                    _tcsProcessWait.TrySetCanceled();
                });
                token.ThrowIfCancellationRequested(); // 在结束前再检查一次

                await _tcsProcessWait.Task;
                if (_tcsProcessWait.Task.Result is not null)
                    throw _tcsProcessWait.Task.Result;
            }
            catch (OperationCanceledException ex) when (token.IsCancellationRequested) {
                App.Log.Warn(ex);
                Terminate();
                throw;
            }
            catch (Exception ex) {
                App.Log.Error(ex);
                Terminate();
                throw;
            }

            return true;
        }

        public void Update(IWpPlayerData data) {
            this.Data = data;
            SendMessage(new VirtualPaperUpdateCmd() {
                RType = data.RType.ToString(),
                FilePath = data.FilePath,
                WpEffectFilePathUsing = data.WpEffectFilePathUsing,
            });
        }

        public void Stop() {
            Pause();
        }

        private void Terminate() {
            try {
                Closing?.Invoke(this, EventArgs.Empty);
                Proc?.Kill();
                Proc?.Dispose();
            }
            catch { }
            DesktopUtil.RefreshDesktop();
        }

        public void SendMessage(IpcMessage obj) {
            SendMessage(JsonSerializer.Serialize(obj, IpcMessageContext.Default.IpcMessage));
        }

        private void SendMessage(string msg) {
            try {
                Proc?.StandardInput.WriteLine(msg);
            }
            catch (Exception e) {
                App.Log.Error($"Stdin write fail: {e.Message}");
                Terminate();
            }
        }

        private void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e) {
            //When the redirected stream is closed, a null line is sent to the event handler.
            if (!string.IsNullOrEmpty(e.Data)) {
                if (JsonSerializer.Deserialize(e.Data, IpcMessageContext.Default.IpcMessage) is VirtualPaperMessageConsole messageConsole && messageConsole.MsgType == ConsoleMessageType.Error) {
                    App.Log.Error($"PlayerWeb-{_uniqueId}: {messageConsole.Message}");
                }
                else {
                    App.Log.Info($"PlayerWeb-{_uniqueId}: {e.Data}");
                }

                IpcMessage obj;
                try {
                    obj = JsonSerializer.Deserialize(e.Data, IpcMessageContext.Default.IpcMessage) ?? throw new("null msg recieved");
                }
                catch (Exception ex) {
                    App.Log.Error($"Ipcmessage parse Error: {ex.Message}");
                    return;
                }
                if (!_isInitialized || !IsLoaded) {
                    if (obj.Type == MessageType.msg_procid) {
                        Exception? error = null;
                        try {
                            nint procid = new(((VirtualPaperMessageProcId)obj).ProcId);
                            Process process = Process.GetProcessById((int)procid);
                            Handle = process.MainWindowHandle; // chrome_widgetwin_1
                            App.Log.Info($"PlayerWeb-{_uniqueId}: ProcId: {procid} - WindowHwnd: {Handle}");

                            if (Equals(Handle, IntPtr.Zero)) {
                                throw new Exception("Browser input/window handle NULL.");
                            }
                            IsLoaded = true;
                        }
                        catch (Exception ie) {
                            error = ie;
                        }
                        finally {
                            _isInitialized = true;
                            _tcsProcessWait.TrySetResult(error);
                        }
                    }
                }
                else if (obj.Type == MessageType.cmd_apply) {
                    Apply?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        ///// <summary>
        ///// 查找指定父窗口下的最后一个类名为 targetClassName 的子窗口。
        ///// </summary>
        ///// <param name="parentHandle">父窗口的句柄。</param>
        ///// <param name="targetClassName">目标子窗口的类名。</param>
        ///// <returns>符合条件的最后一个窗口句柄，如果没有找到则为 nint.Zero。</returns>
        //public static nint FindLastChildWindow(nint parentHandle, string targetClassName) {
        //    nint lastMatchingHandle = nint.Zero;

        //    Native.EnumChildWindows(parentHandle, (hWnd, param) => {
        //        StringBuilder className = new(Native.MAX_CLASS_NAME_LENGTH);
        //        _ = Native.GetClassName(hWnd, className, className.Capacity);

        //        if (className.ToString() == targetClassName) {
        //            lastMatchingHandle = hWnd;
        //        }

        //        // Recursively check child windows of the current window
        //        FindLastChildWindow(hWnd, targetClassName);

        //        return true; // Continue enumeration
        //    }, IntPtr.Zero);

        //    return lastMatchingHandle;
        //}

        ///// <summary>
        ///// 查找指定父窗口下的所有名为 targetWindowTitleOrClass 的子窗口，并重新设置它们的父窗口。
        ///// </summary>
        ///// <param name="parentHandle">父窗口的句柄。</param>
        ///// <param name="targetWindowTitleOrClass">目标子窗口的标题或类名。</param>
        ///// <param name="newParentHandle">新的父窗口的句柄。</param>
        //public static void ReparentAllWindowsNamed(nint parentHandle, string targetWindowTitleOrClass, nint newParentHandle) {
        //    // Enumerate all child windows of the given parent handle and re-parent matching windows
        //    Native.EnumChildWindows(parentHandle, (hWnd, param) =>
        //    {
        //        StringBuilder className = new(Native.MAX_CLASS_NAME_LENGTH);
        //        _ = Native.GetClassName(hWnd, className, Native.MAX_CLASS_NAME_LENGTH);

        //        // Check if the window's class name or title matches the target
        //        if (className.ToString() == targetWindowTitleOrClass) {
        //            // Change the parent of the found window
        //            Native.SetParent(hWnd, newParentHandle);
        //            Console.WriteLine($"Reparented 'Intermediate D3D Window' with handle: {hWnd}");
        //        }

        //        // Continue enumeration
        //        return true;
        //    }, nint.Zero);
        //}

        //private static IntPtr EnumerateChildWindows(IntPtr parentHandle, string targetClassName) {
        //    IntPtr childHandle = Native.FindWindowEx(parentHandle, IntPtr.Zero, null, null); // 开始枚举第一个子窗口

        //    while (childHandle != IntPtr.Zero) {
        //        // 如果找到了目标窗口，返回其句柄
        //        var targetHandel = Native.FindWindowEx(childHandle, IntPtr.Zero, targetClassName, null);
        //        if (targetHandel != IntPtr.Zero)
        //            return targetHandel;

        //        // 否则，递归检查此子窗口的子窗口
        //        IntPtr resultFromChild = EnumerateChildWindows(childHandle, targetClassName);
        //        if (resultFromChild != IntPtr.Zero)
        //            return resultFromChild;

        //        // 移动到下一个兄弟窗口
        //        childHandle = Native.FindWindowEx(parentHandle, childHandle, null, null);
        //    }

        //    // 如果没有找到，返回IntPtr.Zero
        //    return IntPtr.Zero;
        //}

        private void Proc_Exited(object? sender, EventArgs e) {
            _tcsProcessWait.TrySetResult(null);
            Proc.OutputDataReceived -= Proc_OutputDataReceived;
            Terminate();
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

        public bool Equals(IWpPlayer? other) {
            return this.Data.WallpaperUid == other?.Data.WallpaperUid && this.Data.RType == other?.Data.RType;
        }

        #region dispose
        private bool _isDisposed;
        private void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    Closing = null;
                }

                Proc = null;
                Handle = default;
                Data = null;
                Monitor = null;

                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private readonly TaskCompletionSource<Exception> _tcsProcessWait = new();
        private static int _globalCount;
        private readonly int _uniqueId;
        private bool _isInitialized;
        //private readonly int _timeout = 50000;
    }
}
