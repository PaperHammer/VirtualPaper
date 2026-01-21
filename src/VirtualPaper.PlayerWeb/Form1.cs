using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using VirtualPaper.Common;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.PlayerWeb.Extensions;
using VirtualPaper.PlayerWeb.Utils;
using WebView = Microsoft.Web.WebView2.WinForms.WebView2;

namespace VirtualPaper.PlayerWeb {
    public partial class Form1 : Form {
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
                this.Size = new Size(1920, 1080);
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
            args.TryGetDataFromExtra("WindowRect", out _windowRc);

            InitializeWebView2Async().Await(() => {
                _ = StdInListener();
                _scriptExecutor = new WebViewScriptExecutor(_webView2);
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
            _webView2.DefaultBackgroundColor = Color.White;

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

            switch (_startArgs.RuntimeType) {
                case "RImage":
                case "RVideo":
                    UpdateRectToWebview();
                    _scriptExecutor?.EnqueueEvent(Fileds.ResourceLoad, _startArgs.RuntimeType, GetWallpaperVirtualPath(_startArgs.FilePath));
                    break;
                case "RImage3D":
                    UpdateRectToWebview();
                    _scriptExecutor?.EnqueueEvent(Fileds.ResourceLoad, _startArgs.FilePath, _startArgs.DepthFilePath);
                    break;
                default:
                    break;
            }
            LoadWpEffect(_startArgs.WpEffectFilePathUsing);
            _scriptExecutor?.EnqueueEvent(Fileds.Play);

            Program.WriteToParent(new VirtualPaperMessageHwnd() {
                Hwnd = _webView2.Handle.ToInt64(),
            });
        }

        string GetWallpaperVirtualPath(string? filePath) {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var fileName = Path.GetFileName(filePath);

            return $"https://{CoreWebView2Extensions.WallpaperHost}/{Path.GetFileNameWithoutExtension(fileName)}/{fileName}";
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

        private async void HandleIpcMessage(string message) {
            try {
                var obj = JsonSerializer.Deserialize(message, IpcMessageContext.Default.IpcMessage);
                switch (obj.Type) {
                    case MessageType.cmd_close:
                        HandleCloseCommand();
                        break;
                    case MessageType.cmd_apply:
                        _scriptExecutor?.EnqueueEvent(Fileds.ApplyFilter);
                        _scriptExecutor?.EnqueueEvent(Fileds.Play);
                        break;
                    //case MessageType.cmd_active:
                    //    HandleActiveCommand((VirtualPaperActiveCmd)obj);
                    //    break;
                    case MessageType.cmd_reload:
                        CrossThreadInvoker.InvokeOnUIThread(() => {
                            _webView2?.Reload();
                        });
                        break;
                    case MessageType.cmd_suspend:
                        await HandlePlaybackCommandAsync(true);
                        break;
                    case MessageType.cmd_resume:
                        await HandlePlaybackCommandAsync(false);
                        break;
                    case MessageType.cmd_muted:
                        await HandleMuteCommandAsync((VirtualPaperMutedCmd)obj);
                        break;
                    case MessageType.cmd_update:
                        await HandleUpdateCommandAsync((VirtualPaperUpdateCmd)obj);
                        break;
                    case MessageType.cmd_suspend_parallax:
                        _isFocusOnDesk = false;
                        break;
                    case MessageType.cmd_resume_parallax:
                        _isFocusOnDesk = true;
                        break;

                    case MessageType.vp_slider:
                        var sl = (VirtualPaperSlider)obj;
                        HandleUIElementMsg(sl.Name, sl.Value);
                        break;
                    case MessageType.vp_textbox:
                        var tb = (VirtualPaperTextBox)obj;
                        HandleUIElementMsg(tb.Name, tb.Value);
                        break;
                    case MessageType.vp_dropdown:
                        var dd = (VirtualPaperDropdown)obj;
                        HandleUIElementMsg(dd.Name, dd.Value);
                        break;
                    case MessageType.vp_cpicker:
                        var cp = (VirtualPaperColorPicker)obj;
                        HandleUIElementMsg(cp.Name, cp.Value);
                        break;
                    case MessageType.vp_chekbox:
                        var cb = (VirtualPaperCheckbox)obj;
                        ExecuteCheckBoxSet(cb.Name, cb.Value);
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
        private void HandleUIElementMsg(string propertyName, object propertyValue) {
            _scriptExecutor?.EnqueueEvent(Fileds.PropertyListener, propertyName, propertyValue);
        }

        private void HandleCloseCommand() {
            CrossThreadInvoker.InvokeOnUIThread(() => {
                this.Close();
            });
        }

        private async Task HandlePlaybackCommandAsync(bool pause) {
            if (_isPaused == pause) return;

            if (pause) {
                SuspendRenderingSubProcess();
            }
            else {
                ResumeRenderingSubProcess();
            }
            _scriptExecutor?.EnqueueEvent(Fileds.PlaybackChanged, pause);
            _isPaused = pause;
        }

        private async Task HandleMuteCommandAsync(VirtualPaperMutedCmd muted) {
            _scriptExecutor?.EnqueueEvent(Fileds.AudioMuteChanged, muted.IsMuted);
        }

        private async Task HandleUpdateCommandAsync(VirtualPaperUpdateCmd update) {
            if (_startArgs.FilePath != update.FilePath) {
                _startArgs.FilePath = update.FilePath;
                _startArgs.RuntimeType = update.RType;
                _startArgs.WpEffectFilePathTemplate = update.WpEffectFilePathTemplate;
                _startArgs.WpEffectFilePathTemporary = update.WpEffectFilePathTemporary;
                _startArgs.WpEffectFilePathUsing = update.WpEffectFilePathUsing;
                _scriptExecutor?.EnqueueEvent(Fileds.ResourceLoad, _startArgs.RuntimeType, _startArgs.FilePath);
            }

            LoadWpEffect(_startArgs.WpEffectFilePathUsing);
        }
        #endregion

        #region utils
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
                            _scriptExecutor?.EnqueueEvent(Fileds.PropertyListener, item.Name, item.Value.GetProperty("Value").ToString());
                        }
                        else if (uiElementType.Equals("Checkbox", StringComparison.OrdinalIgnoreCase)) {
                            ExecuteCheckBoxSet(item.Name, bool.Parse(item.Value.GetProperty("Value").ToString()));
                        }
                    }
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<Form1>().Error(ex);
            }
        }

        private void ExecuteCheckBoxSet(string propertyName, bool val) {
            switch (propertyName) {
                case "Parallax":
                    RunParallax(val);
                    break;
                default:
                    break;
            }
        }

        private void UpdateRectToWebview() {
            if (_webView2 == null || _webView2.CoreWebView2 == null) return;

            _scriptExecutor?.EnqueueEvent(Fileds.UpdateDimensions, _windowRc.Right - _windowRc.Left, _windowRc.Bottom - _windowRc.Top);
        }
        #endregion

        #region parallax
        private void StartParallax() {
            if (Interlocked.CompareExchange(ref _isParallaxRunning, 1, 0) == 1) return;

            Task.Run(async () => {
                try {
                    int lastX = int.MinValue;
                    int lastY = int.MinValue;
                    bool lastInside = false;

                    while (_isParallaxRunning == 1) {
                        var pos = RawInput.GetMousePos();
                        int mouseX = pos.X, mouseY = pos.Y;

                        int x = (int)_mousePos.X;
                        int y = (int)_mousePos.Y;

                        bool inside = _windowRc.Left <= mouseX && mouseX <= _windowRc.Right &&
                                    _windowRc.Top <= mouseY && mouseY <= _windowRc.Bottom;
                        if (_isFocusOnDesk && inside) {
                            _scriptExecutor?.EnqueueState(
                                key: "MouseMove",
                                functionName: Fileds.MouseMove,
                                x, y
                            );
                            lastX = x;
                            lastY = y;
                        }
                        else if (lastInside) {
                            _scriptExecutor?.EnqueueState(
                                key: "MouseOut",
                                functionName: Fileds.MouseOut
                            );
                        }

                        lastInside = inside;
                    }
                }
                catch (Exception ex) when (ex is OperationCanceledException) { }
                catch (Exception e) {
                    ArcLog.GetLogger<Form1>().Error("[Parallax] Loop error", e);
                }
            });
        }

        private void StopParallax() {
            if (Interlocked.CompareExchange(ref _isParallaxRunning, 0, 1) == 0) return;
            _scriptExecutor?.EnqueueState(
                key: "MouseOut",
                functionName: Fileds.MouseOut
            );
        }

        private void RunParallax(bool isParallaxOn) {
            if (isParallaxOn) {
                StartParallax();
            }
            else {
                StopParallax();
            }
        }
        #endregion

        private readonly StartArgsWeb _startArgs;
        private WebView _webView2 = null!;
        private WebViewScriptExecutor? _scriptExecutor;
        private bool _isFocusOnDesk;
        private bool _isPaused = false;
        private Point _mousePos;
        private Native.RECT _windowRc;
        private volatile int _isParallaxRunning = 0; // 0 = stopped, 1 = running
        private int cefD3DRenderingSubProcessId;
        private readonly CancellationTokenSource _ctsConsoleIn = new();
        private static readonly CoreWebView2EnvironmentOptions _environmentOptions = new() {
            AdditionalBrowserArguments = "--disable-web-security --allow-file-access --allow-file-access-from-files --disk-cache-size=1 --autoplay-policy=no-user-gesture-required "
        }; // workaround: avoid cache
    }
}
