using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using VirtualPaper.Common;
using VirtualPaper.Common.Events;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Common.Utils.Players;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.PlayerWeb.Extensions;
using VirtualPaper.PlayerWeb.Utils;
using WebView = Microsoft.Web.WebView2.WinForms.WebView2;

namespace VirtualPaper.PlayerWeb {
    public partial class Form1 : Form {
        protected override CreateParams CreateParams {
            get {
                CreateParams cp = base.CreateParams;
                // WS_EX_TOOLWINDOW: 从 Alt+Tab 切换器中隐藏窗口
                // 同时移除 WS_EX_APPWINDOW 防止其强制出现在任务栏/Alt+Tab 中
                const int WS_EX_TOOLWINDOW = 0x00000080;
                const int WS_EX_APPWINDOW = 0x00040000;
                cp.ExStyle |= WS_EX_TOOLWINDOW;
                cp.ExStyle &= ~WS_EX_APPWINDOW;
                return cp;
            }
        }

        #region native msg
        protected override void WndProc(ref Message m) {
            switch (m.Msg) {
                //case (int)Native.WM.MOUSEMOVE: {
                //        int x = (short)(m.LParam.ToInt32() & 0xFFFF);
                //        int y = (short)((m.LParam.ToInt32() >> 16) & 0xFFFF);
                //        _scriptExecutor?.EnqueueState("mousemove", Fields.MouseMove, x, y);
                //        return;
                //    }
                //case (int)Native.WM.LBUTTONDOWN: {
                //        int x = (short)(m.LParam.ToInt32() & 0xFFFF);
                //        int y = (short)((m.LParam.ToInt32() >> 16) & 0xFFFF);
                //        _scriptExecutor?.EnqueueEvent(Fields.MouseLeftButtonDown, x, y);
                //        return;
                //    }
                //case (int)Native.WM.LBUTTONUP: {
                //        int x = (short)(m.LParam.ToInt32() & 0xFFFF);
                //        int y = (short)((m.LParam.ToInt32() >> 16) & 0xFFFF);
                //        _scriptExecutor?.EnqueueEvent(Fields.MouseLeftButtonUp, x, y);
                //        return;
                //    }
                case (int)Native.WM.MOUSELEAVE: {
                        OnMouseOut();
                        return;
                    }
                case (int)Native.WM.APP_MOUSEENTER: {
                        OnMouseIn();
                        return;
                    }
            }
            base.WndProc(ref m);
        }

        private void OnMouseIn() {            
            Interlocked.Exchange(ref _isParallaxOnFromMouse, 1);
            RunParallax();
            
            _scriptExecutor?.EnqueueEvent(Fields.MouseIn);
        }

        private void OnMouseOut() {            
            Interlocked.Exchange(ref _isParallaxOnFromMouse, 0);
            RunParallax();
            
            _scriptExecutor?.EnqueueEvent(Fields.MouseOut);
        }
        #endregion

        public Form1(StartArgsWeb args) {
            InitializeComponent();

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            // SynchronizationContext.Current 在 WinForms 应用程序启动初期是 null
            // 只有当 第一个窗体（Form）被创建 或者调用了 Application.Run() 之后，WinForms 才会将系统的 WindowsFormsSynchronizationContext 绑定到当前线程
            CrossThreadInvoker.Initialize(new UiSynchronizationContext());
            this.SizeChanged += Form1_SizeChanged;

            if (args.IsDebug) {
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.WindowState = FormWindowState.Normal;
                this.StartPosition = FormStartPosition.Manual;
                this.Size = new Size(1440, 810);
                this.ShowInTaskbar = true;
                this.MaximizeBox = true;
                this.MinimizeBox = true;
            }
            else {
                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Minimized;
                this.StartPosition = FormStartPosition.Manual;
                this.ShowInTaskbar = false;
                this.Location = new Point(-9999, 0);
            }

            _startArgs = args;

            InitializeWebView2Async().Await(() => {
                _scriptExecutor = new WebViewScriptExecutor(_webView2);
                _ = StdInListener();
            },
            (ex) => {
                Program.WriteToParent(new VirtualPaperMessageConsole() {
                    MsgType = ConsoleMessageType.Error,
                    Message = $"Failed to initialize WebView2: {ex}"
                });
                // Exit or display custom error page.
                Environment.Exit(1);
            });
        }

        protected override void OnFormClosing(FormClosingEventArgs e) {
            base.OnFormClosing(e);

            this.Hide();
            _ctsConsoleIn?.Cancel();
            StopParallax();
            _scriptExecutor?.Dispose();
        }

        private void Form1_SizeChanged(object? sender, EventArgs e) {
            _windowRc = new Native.RECT {
                Left = this.Left,
                Top = this.Top,
                Right = this.Right,
                Bottom = this.Bottom
            };
            UpdateRectToWebview();
        }

        #region webview2
        public async Task InitializeWebView2Async() {
            _webView2 = new WebView() {
                DefaultBackgroundColor = Color.Transparent
            };
            _webView2.NavigationCompleted += WebView2_NavigationCompleted;

            // WebView2 does not have in-memory mode, ref: https://github.com/MicrosoftEdge/WebView2Feedback/issues/3637
            // Custom user data folder, ref: https://docs.microsoft.com/en-us/microsoft-edge/webview2/concepts/user-data-folder
            var env = await CoreWebView2Environment.CreateAsync(null, Constants.CommonPaths.TempWebView2Dir, _environmentOptions);
            await _webView2.EnsureCoreWebView2Async(env);

#if DEBUG
            _webView2.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
            _webView2.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _webView2.CoreWebView2.OpenDevToolsWindow();
#else
            // Don't allow contextmenu and devtools.
            _webView2.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            _webView2.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
#endif

            _webView2.CoreWebView2.ProcessFailed += CoreWebView2_ProcessFailed;
            _webView2.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;
            _webView2.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

            string playingFile = GetPlayingFile();
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, playingFile);
            _webView2.NavigateToLocalPath(new Uri(fullPath).LocalPath);

            this.Controls.Add(_webView2);
            _webView2.Dock = DockStyle.Fill;
        }

        private void CoreWebView2_ProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e) {
            // Expected behavior: DebugActiveProcess(CEF_D3DRenderingSubProcess)
            // Ref: https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2processfailedkind
            if (e.Reason == CoreWebView2ProcessFailedReason.Unresponsive)
                return;

            Program.WriteToParent(new VirtualPaperMessageConsole() {
                MsgType = ConsoleMessageType.Error,
                Message = $"CoreWebView2 process failed: {e.Reason}"
            });
        }

        private void CoreWebView2_DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e) {
            // Cancel user requested downloads.
            e.Cancel = true;
        }

        private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e) {
            // Avoid popups.
            e.Handled = true;
        }

        private void WebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) {
            // Restore default.
            _webView2.DefaultBackgroundColor = Color.Gray;

            if (!e.IsSuccess) {
                Program.WriteToParent(new VirtualPaperMessageConsole() {
                    MsgType = ConsoleMessageType.Error,
                    Message = $"WebView navigation failed: {e.WebErrorStatus}"
                });
                return;
            }

            if (!_webView2.TryGetCefD3DRenderingSubProcessId(out cefD3DRenderingSubProcessId)) {
                Program.WriteToParent(new VirtualPaperMessageConsole() {
                    MsgType = ConsoleMessageType.Error,
                    Message = $"Failed to retrieve GetCefD3DRenderingSubProcessId"
                });
            }

            Run();

            Program.WriteToParent(new VirtualPaperMessageHwnd() {
                Hwnd = _webView2.Handle.ToInt64(),
            });
        }

        string GetWallpaperVirtualPath(string? filePath) {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var fileName = Path.GetFileName(filePath);

            return $"https://{CoreWebView2Extensions.WallpaperHost}/{Path.GetFileNameWithoutExtension(fileName)}/{fileName}";
        }

        // RWeb: filePath = {wallpapers}/{wpId}/index.html
        // WallpaperHost → {wallpapers}/
        // 结果: https://wallpaper.localhost/{wpId}/index.html
        // iframe 内部的相对引用（js/css/图片）会自动基于此 URL 解析，整个目录可访问
        string GetWallpaperVirtualPathForWeb(string? filePath) {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var wpFolder = Path.GetFileName(Path.GetDirectoryName(filePath));
            var htmlFile = Path.GetFileName(filePath);
            return $"https://{CoreWebView2Extensions.WallpaperHost}/{wpFolder}/{htmlFile}";
        }

        string GetWallpaperRootVirtualPath(string? fileName, string? filePath) {
            if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            return $"https://{CoreWebView2Extensions.WallpaperHost}/{fileName}/{Path.GetFileName(filePath)}";
        }
        #endregion

        private async Task StdInListener() {
            try {
                await Task.Run(async () => {
                    while (!_ctsConsoleIn.IsCancellationRequested) {
                        var msg = await Console.In.ReadLineAsync(_ctsConsoleIn.Token);
                        if (string.IsNullOrEmpty(msg)) {
                            Program.WriteToParent(new VirtualPaperMessageConsole {
                                MsgType = ConsoleMessageType.Log,
                                Message = "Ipc stdin none, closing"
                            });
                            //When the redirected stream is closed, a null line is sent to the event handler. 
#if !DEBUG
                            break;
#endif
                        }
                        else {
                            HandleIpcMessage(msg);
                        }
                    }
                });
            }
            catch (Exception e) {
                Program.WriteToParent(new VirtualPaperMessageConsole {
                    MsgType = ConsoleMessageType.Error,
                    Message = $"Ipc stdin Error: {e.Message}"
                });
            }
        }

        private void HandleIpcMessage(string message) {
            try {
                var obj = JsonSerializer.Deserialize(message, IpcMessageContext.Default.IpcMessage);
                if (obj == null) return;
                switch (obj.Type) {
                    case MessageType.cmd_close:
                        HandleCloseCommand();
                        break;
                    case MessageType.cmd_apply:
                        _scriptExecutor?.EnqueueEvent(Fields.ApplyFilter);
                        _scriptExecutor?.EnqueueEvent(Fields.Play);
                        break;
                    case MessageType.cmd_reload:
                        CrossThreadInvoker.InvokeOnUIThread(() => {
                            _webView2?.Reload();
                        });
                        break;
                    case MessageType.cmd_reload_effect:
                        LoadWpEffect(_startArgs.WpEffectFilePathUsing);
                        break;
                    case MessageType.cmd_suspend:
                        HandlePlaybackCommand(true);
                        break;
                    case MessageType.cmd_resume:
                        HandlePlaybackCommand(false);
                        break;
                    case MessageType.cmd_muted:
                        HandleMuteCommand((VirtualPaperMutedCmd)obj);
                        break;
                    case MessageType.cmd_update:
                        CrossThreadInvoker.InvokeOnUIThread(() => {
                            HandleUpdateCommand((VirtualPaperUpdateCmd)obj);
                        });
                        break;
                    case MessageType.cmd_suspend_parallax:
                        Interlocked.Exchange(ref _isParallaxOnFromIpc, 0);
                        RunParallax();
                        break;
                    case MessageType.cmd_resume_parallax:
                        Interlocked.Exchange(ref _isParallaxOnFromIpc, 1);
                        RunParallax();
                        break;
                    case MessageType.vp_general_effect:
                        HandleGenerealEffect((VirtualPaperGeneralEffect)obj);
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported message type: {obj.Type}");
                }
            }
            catch (Exception e) {
                Program.WriteToParent(new VirtualPaperMessageConsole {
                    MsgType = ConsoleMessageType.Error,
                    Message = $"Ipc action Error: {e.Message}"
                });
            }
        }

        #region handel_ipcmessage
        private void HandleGenerealEffect(VirtualPaperGeneralEffect ge) {
            switch (ge.EffectValue) {
                case EffectValueChanged<double> d:
                    UpdateEffectValueNumber(d);
                    break;
                case EffectValueChanged<int> i:
                    UpdateEffectValueNumber(i);
                    break;
                case EffectValueChanged<bool> b:
                    UpdateEffectValue(b);
                    break;
                case EffectValueChanged<string> s:
                    UpdateEffectValue(s);
                    break;
            }
        }

        public void UpdateEffectValueNumber<T>(EffectValueChanged<T> e) where T : struct {
            _scriptExecutor?.EnqueueEvent(Fields.PropertyListener, e.PropertyName, e.Value);
        }

        public void UpdateEffectValue(EffectValueChanged<bool> e) {
            ExecuteCheckBoxSet(e.PropertyName, e.Value);
        }

        public void UpdateEffectValue(EffectValueChanged<string> e) {
        }

        private void HandleCloseCommand() {
            CrossThreadInvoker.InvokeOnUIThread(() => {
                this.Close();
            });
        }

        private void HandlePlaybackCommand(bool pause) {
            if (_isPaused == pause) return;

            if (pause) {
                SuspendRenderingSubProcess();
            }
            else {
                ResumeRenderingSubProcess();
            }
            _scriptExecutor?.EnqueueEvent(Fields.PlaybackChanged, pause);
            _isPaused = pause;
        }

        private void HandleMuteCommand(VirtualPaperMutedCmd muted) {
            _scriptExecutor?.EnqueueEvent(Fields.AudioMuteChanged, muted.IsMuted);
        }

        private void HandleUpdateCommand(VirtualPaperUpdateCmd update) {
            if (update.Args != null) {
                var args = JsonSerializer.Deserialize<StartArgsWeb>(update.Args);
                if (args != null && _startArgs.FilePath != args.FilePath) {
                    _startArgs = args;
                    Run();
                }
            }            
        }
        #endregion

        #region utils
        private void Run() {
            switch (_startArgs.RuntimeType) {
                case "RImage":
                case "RVideo":
                    UpdateRectToWebview();
                    _scriptExecutor?.EnqueueEvent(Fields.ResourceLoad, _startArgs.RuntimeType, GetWallpaperVirtualPath(_startArgs.FilePath));
                    break;
                case "RImage3D":
                    UpdateRectToWebview();
                    _scriptExecutor?.EnqueueEvent(Fields.ResourceLoad, GetWallpaperVirtualPath(_startArgs.FilePath), GetWallpaperRootVirtualPath(Path.GetFileNameWithoutExtension(_startArgs.FilePath), _startArgs.DepthFilePath));
                    break;
                case "RWeb":
                    UpdateRectToWebview();
                    _scriptExecutor?.EnqueueEvent(Fields.ResourceLoad, _startArgs.RuntimeType, GetWallpaperVirtualPathForWeb(_startArgs.FilePath));
                    break;
                default:
                    break;
            }
            LoadWpEffect(_startArgs.WpEffectFilePathUsing);
            _scriptExecutor?.EnqueueEvent(Fields.Play);
        }

        /// <summary>
        /// Resumes the suspended CEF Direct3D rendering subprocess by detaching the debugger.
        /// Must be called on the same thread that previously attached, since debugger state is thread-specific.
        /// </summary>
        private void ResumeRenderingSubProcess() {
            if (cefD3DRenderingSubProcessId == 0)
                return;

            _ = Native.DebugActiveProcessStop((uint)cefD3DRenderingSubProcessId);
        }

        /// <summary>
        /// Suspends the CEF Direct3D rendering subprocess by attaching a debugger.
        /// Must be called on the same thread to ensure proper detachment later.
        /// </summary>
        private void SuspendRenderingSubProcess() {
            // The "System Idle Process" is given process ID 0, Kernel is 1.
            if (cefD3DRenderingSubProcessId == 0)
                return;

            // DebugSetProcessKillOnExit by default is TRUE.
            // Ref: https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-debugsetprocesskillonexit
            _ = Native.DebugActiveProcess((uint)cefD3DRenderingSubProcessId);
        }

        private string GetPlayingFile() {
            return _startArgs.RuntimeType switch {
                "RImage" => PlayingFileWeb.PlayerWeb,
                "RImage3D" => PlayingFileWeb.PlayerWeb3D,
                "RVideo" => PlayingFileWeb.PlayerWeb,
                "RWeb" => PlayingFileWeb.PlayerWeb,
                _ => throw new ArgumentException(nameof(_startArgs.RuntimeType)),
            };
        }

        private void LoadWpEffect(string? wpEffectFilePath) {
            try {
                if (wpEffectFilePath == null) return;

                foreach (var item in JsonNodeUtil.GetReadonlyJson(wpEffectFilePath).EnumerateObject()) {
                    string uiElementType = item.Value.GetProperty("Type").ToString();
                    if (!uiElementType.Equals("Button", StringComparison.OrdinalIgnoreCase) && !uiElementType.Equals("Label", StringComparison.OrdinalIgnoreCase)) {
                        if (uiElementType.Equals("Slider", StringComparison.OrdinalIgnoreCase) ||
                            uiElementType.Equals("Dropdown", StringComparison.OrdinalIgnoreCase) ||
                            uiElementType.Equals("Color", StringComparison.OrdinalIgnoreCase) ||
                            uiElementType.Equals("Textbox", StringComparison.OrdinalIgnoreCase)) {
                            _scriptExecutor?.EnqueueEvent(Fields.PropertyListener, item.Name, item.Value.GetProperty("Value").ToString());
                        }
                        else if (uiElementType.Equals("Checkbox", StringComparison.OrdinalIgnoreCase)) {
                            ExecuteCheckBoxSet(item.Name, bool.Parse(item.Value.GetProperty("Value").ToString()));
                        }
                    }
                }
            }
            catch (Exception ex) {
                Program.WriteToParent(new VirtualPaperMessageConsole() {
                    MsgType = ConsoleMessageType.Error,
                    Message = $"Failed to load effect config: {ex.Message}"
                });
            }
        }

        private void ExecuteCheckBoxSet(string propertyName, bool val) {
            switch (propertyName) {
                case "Parallax":
                    Interlocked.Exchange(ref _isParallaxOnFromUser, val ? 1 : 0);
                    RunParallax();
                    break;
                case "TimeAtmoPerception":
                    RunTimePerception(val);
                    break;
                default:
                    break;
            }
        }

        private void UpdateRectToWebview() {
            if (_webView2 == null || _webView2.CoreWebView2 == null) return;

            _scriptExecutor?.EnqueueEvent(Fields.UpdateDimensions, _windowRc.Right - _windowRc.Left, _windowRc.Bottom - _windowRc.Top);
        }
        #endregion

        #region time perception
        private void RunTimePerception(bool isTimePerceptionOn) {
            // 先停止旧任务
            _tpCts?.Cancel();
            _tpCts?.Dispose();
            _tpCts = null;

            if (isTimePerceptionOn) {
                _tpCts = new CancellationTokenSource();
                _ = TimePerceptionLoopAsync(_tpCts.Token);
            }
            else {
                // 通知 JS 关闭
                var payload = JsonSerializer.Serialize(new { enabled = false });
                _scriptExecutor?.EnqueueEvent(Fields.TimePerception, payload);
            }
        }

        /// <summary>
        /// 每天重新计算日出日落，下发参数给 JS
        /// </summary>
        private async Task TimePerceptionLoopAsync(CancellationToken ct) {
            while (!ct.IsCancellationRequested) {
                // 计算今日参数并下发
                await SendTimePerceptionConfigAsync();

                // 等到次日 00:01 再重新计算
                var now = DateTime.Now;
                var nextMidnight = now.Date.AddDays(1).AddMinutes(1);
                var delay = nextMidnight - now;

                try {
                    await Task.Delay(delay, ct);
                }
                catch (TaskCanceledException) {
                    break;
                }
            }
        }

        private async Task SendTimePerceptionConfigAsync() {
            var (latitude, longitude) = await Win32Util.GetSystemLocationAsync();
            var (sunriseLocal, sunsetLocal) = SunCalc.Calculate(DateTime.UtcNow.Date, latitude, longitude);

            var config = new {
                enabled = true,
                sunrise = sunriseLocal.ToString("HH:mm"),
                sunset = sunsetLocal.ToString("HH:mm"),
                transitionMinutes = 30,
                phases = new {
                    night = new { brightness = -0.3, hue = 220, saturate = -0.2 },
                    dawn = new { brightness = 0.1, hue = 30, saturate = 0.3 },
                    day = new { brightness = 0.0, hue = 0, saturate = 0.0 },
                    dusk = new { brightness = -0.1, hue = 20, saturate = 0.2 },
                }
            };

            var payload = JsonSerializer.Serialize(config);
            _scriptExecutor?.EnqueueEvent(Fields.TimePerception, payload);
        }

        private CancellationTokenSource? _tpCts;
        #endregion

        #region parallax

        private void StartParallax() {
            _scriptExecutor?.EnqueueEvent(Fields.StartParallax);
        }

        private void StopParallax() {
            _scriptExecutor?.EnqueueEvent(Fields.StopParallax);
        }

        /// <summary>
        /// 视差启动条件：用户开启 AND 鼠标在本壁纸上 AND IPC 未挂起（无全屏/焦点应用遮盖）
        /// </summary>
        private void RunParallax() {
            if (Interlocked.CompareExchange(ref _isParallaxOnFromUser, 0, 0) == 1 &&
                Interlocked.CompareExchange(ref _isParallaxOnFromMouse, 0, 0) == 1 &&
                Interlocked.CompareExchange(ref _isParallaxOnFromIpc, 0, 0) == 1) {
                StartParallax();
            }
            else {
                StopParallax();
            }
        }

        #endregion

        private StartArgsWeb _startArgs;
        private WebView _webView2 = null!;
        private WebViewScriptExecutor? _scriptExecutor;
        private bool _isPaused = false;
        private Native.RECT _windowRc;
        private int cefD3DRenderingSubProcessId;
        private int _isParallaxOnFromUser;      // 0=关闭, 1=开启（用户效果设置）
        private int _isParallaxOnFromMouse;     // 0=鼠标不在本壁纸上, 1=在（MouseEnter/Leave）
        private int _isParallaxOnFromIpc = 1;   // 0=IPC 挂起（全屏/焦点应用遮盖）, 1=IPC 允许（默认允许）
        private readonly CancellationTokenSource _ctsConsoleIn = new();
        private static readonly CoreWebView2EnvironmentOptions _environmentOptions = new() {
            AdditionalBrowserArguments = "--disk-cache-size=1 --autoplay-policy=no-user-gesture-required"
        };
    }
}
