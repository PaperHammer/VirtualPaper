using Newtonsoft.Json;
using NLog;
using System.Diagnostics;
using System.IO;
using System.Text;
using VirtualPaper.Common;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Common.Utils.Shell;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.WallpaperMetaData;
using VirtualPaper.Views.Preview;

namespace VirtualPaper.Cores.Players.Web
{
    internal class Webviewer : IWallpaper
    {
        public Process Proc { get; }

        public nint Handle { get; private set; }

        public nint InputHandle { get; private set; }

        public IMetaData MetaData { get; private set; }

        public IMonitor Monitor { get; set; }

        public bool IsExited { get; private set; }

        public bool IsLoaded { get; private set; } = false;

        public Webviewer(
            IMetaData metaData,
            IMonitor monitor,
            bool isPreview,
            bool isLibrarypreview)
        {
            _isPreview = isPreview;

            if (isPreview)
            {
                _webPreviewer = new(
                    metaData.Type,
                    metaData.FilePath,
                    isLibrarypreview ? metaData.WpCustomizePath : metaData.WpCustomizePathTmp);
                _webPreviewer.Reset += ResetPreviewer;
                return;
            }

            StringBuilder cmdArgs = new();
            cmdArgs.Append($" --working-dir {_workingDir}");
            cmdArgs.Append($" --file-path {metaData.FilePath}");
            cmdArgs.Append(" --wallpaper-type " + metaData.Type.ToString());
            cmdArgs.Append($" --customize-file-path {metaData.WpCustomizePathUsing}");

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
            MetaData = metaData;
            Monitor = monitor;
            _uniqueId = _globalCount++;
        }

        public void Close()
        {
            SendMessage(new VirtualPaperCloseCmd());
        }

        public void ClosePreview()
        {
            if (_webPreviewer != null)
            {
                _webPreviewer.Close();
                _webPreviewer = null;
            }
        }

        public void Pause()
        {
            if (MetaData.Type == WallpaperType.video)
                SendMessage(new VirtualPaperSuspendCmd());
        }

        public void Play()
        {
            if (MetaData.Type == WallpaperType.video)
                SendMessage(new VirtualPaperResumeCmd());
        }

        public Task ScreenCapture(string filePath) { return Task.FromResult(string.Empty); }

        public void SetMute(bool mute)
        {
            if (MetaData.Type == WallpaperType.video)
                SendMessage(new VirtualPaperMuted() { IsMuted = mute });
        }

        public void SetPlaybackPos(float pos, PlaybackPosType type) { }

        public async Task<bool> ShowAsync(CancellationToken token)
        {
            if (_isPreview)
            {
                _webPreviewer?.Show();
                _webPreviewer?.Activate();
                return true;
            }

            if (Proc is null)
                return false;

            try
            {
                token.ThrowIfCancellationRequested(); // 在开始前检查一次

                Proc.Exited += Proc_Exited;
                Proc.OutputDataReceived += Proc_OutputDataReceived;
                Proc.Start();
                Proc.BeginOutputReadLine();

                using var registration = token.Register(() =>
                {
                    _tcsProcessWait.TrySetCanceled();
                });
                token.ThrowIfCancellationRequested(); // 在结束前再检查一次

                await _tcsProcessWait.Task;
                if (_tcsProcessWait.Task.Result is not null)
                    throw _tcsProcessWait.Task.Result;
            }
            catch (OperationCanceledException)
            {
                // 如果捕获到取消异常，则说明任务已经被取消
                Terminate();

                return false;
            }
            catch (Exception)
            {
                Terminate();

                throw;
            }

            return true;
        }

        public void Modify(string controlName, string propertyName, string value)
        {
            if (_webPreviewer == null) return;

            _webPreviewer.ModifySource(controlName, propertyName, value);
        }

        public void Update(IMetaData metaData)
        {
            this.MetaData = metaData;
            SendMessage(new VirtualPaperUpdate()
            {
                WpType = metaData.Type.ToString(),
                FilePath = metaData.FilePath,
                WpCustomizePathUsing = metaData.WpCustomizePathUsing
            });
        }

        private void ResetPreviewer()
        {
            _webPreviewer = null;
        }

        public void Stop()
        {
            Pause();
        }

        private void Terminate()
        {
            try
            {
                Proc?.Kill();
            }
            catch { }
            DesktopUtil.RefreshDesktop();
        }

        public void SendMessage(IpcMessage obj)
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
            //When the redirected stream is closed, a null line is sent to the event handler.
            if (!string.IsNullOrEmpty(e.Data))
            {
                VirtualPaperMessageConsole messageConsole = System.Text.Json.JsonSerializer.Deserialize<VirtualPaperMessageConsole>(e.Data.ToString());
                if (messageConsole.MsgType == ConsoleMessageType.Error)
                {
                    _logger.Error($"Webview2-{_uniqueId}: {messageConsole.Message}");
                }
                else
                {
                    _logger.Info($"Webview2-{_uniqueId}: {e.Data}");
                }

                if (!_isInitialized || !IsLoaded)
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

                    if (obj.Type == MessageType.msg_hwnd)
                    {
                        Exception? error = null;
                        try
                        {
                            var handle = new IntPtr(((VirtualPaperMessageHwnd)obj).Hwnd);

                            var chrome_WidgetWin_0 = Native.FindWindowEx(handle, IntPtr.Zero, "Chrome_WidgetWin_0", null);
                            //var chrome_WidgetWin_0 = EnumerateChildWindows(handle, "Chrome_WidgetWin_0");
                            if (!chrome_WidgetWin_0.Equals(IntPtr.Zero))
                            {
                                this.InputHandle = Native.FindWindowEx(chrome_WidgetWin_0, IntPtr.Zero, "Chrome_WidgetWin_1", null);
                            }
                            Handle = Proc.GetProcessWindow(true);

                            if (IntPtr.Equals(Handle, IntPtr.Zero) || IntPtr.Equals(InputHandle, IntPtr.Zero))
                            {
                                throw new Exception("Browser input/window handle NULL.");
                            }
                        }
                        catch (Exception ie)
                        {
                            error = ie;
                        }
                        finally
                        {
                            _isInitialized = true;
                            _tcsProcessWait.TrySetResult(error);
                        }
                    }
                    else if (obj.Type == MessageType.msg_wploaded)
                    {
                        IsLoaded = true;
                    }
                }
            }
        }

        //private IntPtr EnumerateChildWindows(IntPtr parentHandle, string targetClassName)
        //{
        //    IntPtr childHandle = Native.FindWindowEx(parentHandle, IntPtr.Zero, null, null); // 开始枚举第一个子窗口

        //    while (childHandle != IntPtr.Zero)
        //    {
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

        private void Proc_Exited(object? sender, EventArgs e)
        {
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

        private WebPreviewer? _webPreviewer;
        private bool _isPreview;
        private readonly TaskCompletionSource<Exception> _tcsProcessWait = new();
        private static int _globalCount;
        private readonly int _uniqueId;
        private bool _isInitialized;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        //private readonly int _timeout = 50000;
        private readonly string _workingDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Webviewer");
        private readonly string _fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Webviewer", "VirtualPaper.PlayerWebView2.exe");
    }
}
