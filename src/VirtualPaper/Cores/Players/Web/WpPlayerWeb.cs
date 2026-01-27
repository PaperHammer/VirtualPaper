using System.Diagnostics;
using System.IO;
using System.Text.Json;
using VirtualPaper.Common;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Common.Utils.Shell;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Cores.Players.Web {
    internal partial class WpPlayerWeb : IWpPlayer {
        public Process Proc { get; private set; }
        public nint RealPlayerWindowHandle { get; private set; }
        public nint ProcWindowHandle { get; private set; }
        public IWpPlayerData Data { get; private set; }
        public IMonitor? Monitor { get; set; }
        public bool IsExited { get; private set; }
        public bool IsLoaded { get; private set; }
        public bool IsPreview { get; private set; }
        public string StartArgs { get; private set; }
        public EventHandler? Closing { get; set; }

        public WpPlayerWeb(IWpPlayerData data, IMonitor? monitor, bool isPreview) {
            string workingDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                Constants.WorkingDir.PlayerWeb);

            CheckParams(data);
            IsPreview = isPreview;

            ProcessStartInfo start = new() {
                FileName = Path.Combine(workingDir, Constants.ModuleName.PlayerWeb),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                UseShellExecute = false,
                WorkingDirectory = workingDir,
                WindowStyle = ProcessWindowStyle.Minimized
            };

            Process process = new() {
                EnableRaisingEvents = true,
                StartInfo = start,
            };

            Proc = process;
            Data = data;
            Monitor = monitor;
            _uniqueId = _globalCount++;
        }

        private static void CheckParams(IWpPlayerData data) {
            if (string.IsNullOrEmpty(data.FilePath) ||
                string.IsNullOrEmpty(data.WpEffectFilePathUsing) ||
                string.IsNullOrEmpty(data.WpEffectFilePathTemplate) ||
                //string.IsNullOrEmpty(data.WpEffectFilePathTemporary) ||
                data.RType == RuntimeType.RImage3D && string.IsNullOrEmpty(data.DepthFilePath)) {
                throw new Exception("Some necessary parameters are missing when starting WpPlayerWeb.");
            }
        }

        public void Close() {
            Terminate();
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
            if (Interlocked.CompareExchange(ref _parallaxState, 1, 0) == 0) {
                SendMessage(new VirtualPaperParallaxSuspendCmd());
            }
        }

        public void ResumeParallax() {
            if (Interlocked.CompareExchange(ref _parallaxState, 0, 1) == 1) {
                SendMessage(new VirtualPaperParallaxResumeCmd());
            }
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

                StartArgs = new PlayerWebSrartArgs(Data, IsPreview).ToJson();
                SendMessage(StartArgs);

                using var registration = token.Register(() => {
                    _tcsProcessWait.TrySetCanceled();
                });
                token.ThrowIfCancellationRequested(); // 在结束前再检查一次

                await _tcsProcessWait.Task;
                if (_tcsProcessWait.Task.Result is not null)
                    throw _tcsProcessWait.Task.Result;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
                Terminate();
            }
            catch (Exception ex) {
                App.Log.Error(ex);
                Terminate();
                throw;
            }

            return _isInitialized && !Proc.HasExited;
        }

        public void Update(IWpPlayerData data) {
            this.Data = data;
            SendMessage(new VirtualPaperUpdateCmd() {
                RType = data.RType.ToString(),
                FilePath = data.FilePath,
                WpEffectFilePathUsing = data.WpEffectFilePathUsing,
                WpEffectFilePathTemplate = data.WpEffectFilePathTemplate,
                WpEffectFilePathTemporary = data.WpEffectFilePathTemporary,
            });
        }

        public void Stop() {
            Pause();
        }

        private void Terminate() {
            try {
                Proc.StandardInput?.Close();
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
                DebugUtil.DebugOutPut($"WpPlayerWeb Send: {msg}");
                Proc?.StandardInput.WriteLine(msg);
                Proc?.StandardInput.Flush();
            }
            catch (Exception e) {
                App.Log.Error($"Stdin write fail: {e.Message}");
                Terminate();
            }
        }

        private void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e) {
            //When the redirected stream is closed, a null line is sent to the event handler.
            if (!string.IsNullOrEmpty(e.Data)) {
                try {
                    if (JsonSerializer.Deserialize(e.Data, IpcMessageContext.Default.IpcMessage) is VirtualPaperMessageConsole messageConsole && messageConsole.MsgType == ConsoleMessageType.Error) {
                        App.Log.Error($"WpPlayerWeb-{_uniqueId}: {messageConsole.Message}");
                    }
                    else {
                        App.Log.Info($"WpPlayerWeb-{_uniqueId}: {e.Data}");
                    }
                }
                catch (Exception ex) {
                    ArcLog.GetLogger<WpPlayerWeb>().Error(ex);
                    ArcLog.GetLogger<WpPlayerWeb>().Error(e.Data);
                    return;
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
                    if (obj.Type == MessageType.msg_hwnd) {
                        Exception? error = null;
                        try {
                            var handle = new IntPtr(((VirtualPaperMessageHwnd)obj).Hwnd);
                            //WindowsForms10.Window.8.app.0.141b42a_r9_ad1
                            var chrome_WidgetWin_0 = Native.FindWindowEx(handle, IntPtr.Zero, "Chrome_WidgetWin_0", null);
                            if (!chrome_WidgetWin_0.Equals(IntPtr.Zero)) {
                                RealPlayerWindowHandle = Native.FindWindowEx(chrome_WidgetWin_0, IntPtr.Zero, "Chrome_WidgetWin_1", null);
                            }
                            ProcWindowHandle = Proc.GetProcessWindow(true);

                            App.Log.Info($"WpPlayerWeb-{_uniqueId}: ProcId: {Proc.Id} - ProcWindowHandle: 0x{ProcWindowHandle.ToInt64():X} - RealPlayerWindowHandle: 0x{RealPlayerWindowHandle.ToInt64():X}");

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
            }
            else {
                _tcsProcessWait.TrySetResult(new ArgumentException(e.Data));
            }
        }

        private void Proc_Exited(object? sender, EventArgs e) {
            _tcsProcessWait.TrySetResult(null);
            Proc.OutputDataReceived -= Proc_OutputDataReceived;
            Terminate();
            IsExited = true;
        }

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

                Proc.StandardInput?.Close();
                Proc?.Dispose();
                Proc = null;
                RealPlayerWindowHandle = default;
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
        private int _parallaxState = 0; // 0: 运行中, 1: 已暂停
    }
}
