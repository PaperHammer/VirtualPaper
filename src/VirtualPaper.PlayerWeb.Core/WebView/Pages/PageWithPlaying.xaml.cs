using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using VirtualPaper.Common;
using VirtualPaper.Common.Events;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Players;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.PlayerWeb.Core.Utils;
using VirtualPaper.PlayerWeb.Core.Utils.Interfaces;
using VirtualPaper.PlayerWeb.Core.Utils.PreviewSystem;
using VirtualPaper.PlayerWeb.Core.Utils.PreviewSystem.Server;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using Windows.Foundation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.PlayerWeb.Core.WebView.Pages {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PageWithPlaying : ArcPage, IEffectService {
        public override Type ArcType => typeof(PageWithPlaying);
        protected override bool IsMultiInstance => true;

        /// <summary>
        /// File-type-aware hot reload entry point.
        /// The <paramref name="relativePath"/> should be relative to the
        /// content root (e.g. "css/style.css", "js/app.js").  The
        /// <see cref="PreviewManager"/> routes it to the correct handler
        /// which sends a postMessage into the content iframe.
        /// </summary>
        public void OnFileChanged(string relativePath) {
            if (_previewManager == null) {
                ArcLog.GetLogger<PageWithPlaying>().Warn(
                    $"HMR skipped (PreviewManager not ready): {relativePath}");
                return;
            }

            try {
                _previewManager.OnFileChanged(relativePath);
                ArcLog.GetLogger<PageWithPlaying>().Info($"HMR: {relativePath}");
            }
            catch (Exception ex) {
                ArcLog.GetLogger<PageWithPlaying>().Error($"HMR failed for {relativePath}: {ex.Message}");
            }
        }

        public PageWithPlaying() {
            this.InitializeComponent();
            ArcContext.AttachLoadingComponent(this.MainHost.LoadingControlHost);
        }

        protected override void OnEnter(FrameworkPayload? payload) {
            base.OnEnter(payload);
            Payload = payload;
            if (payload != null) {
                payload.TryGet(NaviPayloadKey.StartArgs.ToString(), out _startArgs);
                // 预览窗口使用
                payload.TryGet(NaviPayloadKey.ArcWindow.ToString(), out _arcWindow);
                payload.Set(NaviPayloadKey.IEffectService.ToString(), this);
                payload.Set(NaviPayloadKey.AvailableConfigTab.ToString(), DataConfigTab.GeneralEffect | DataConfigTab.GeneralInfo);
            }

            SidePanel.Visibility = _startArgs.IsPreview ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        }

        protected override void ArcPage_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            base.ArcPage_Unloaded(sender, e);
            OnUnloaded();
        }

        private void ArcPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            var loadingCtx = ArcContext?.LoadingContext;
            if (loadingCtx == null)
                return;

            _ = loadingCtx.RunAsync(async token => {
                await InitializeWebViewAsync();
                _scriptExecutor = new WebViewScriptExecutor(Webview2);
                await _loadedTcs.Task;
            });
        }

        private void ArcPage_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e) {
            _pageRegion = new Rect() {
                Width = e.NewSize.Width,
                Height = e.NewSize.Height
            };
            UpdateRectToWebview();
        }

        private void InputLayer_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) {
            _isPointerInsidePage = true;
        }

        private void InputLayer_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) {
            //if (_isParallaxRunning != 1) return;

            var point = e.GetCurrentPoint(this);
            _mousePos = point.Position;
        }

        private void InputLayer_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) {
            _isPointerInsidePage = false;
        }

        #region effect change from ui
        public void UpdateEffectValue<T>(EffectValueChanged<T> e) {
            switch (e.Value) {
                case double:
                case int:
                    _scriptExecutor?.EnqueueEvent(Fields.PropertyListener, e.PropertyName, e.Value);
                    break;
                case bool b:
                    ExecuteCheckBoxSet(e.PropertyName, b);
                    break;
                case string s:
                    break;
                default:
                    break;
            }
        }
        #endregion

        #region parallax        
        //private void StartParallax() {
        //    if (Interlocked.CompareExchange(ref _isParallaxRunning, 1, 0) == 1) return;

        //    Task.Run(async () => {
        //        try {
        //            int lastX = int.MinValue;
        //            int lastY = int.MinValue;
        //            bool lastInside = false;

        //            while (_isParallaxRunning == 1) {
        //                int x = (int)_mousePos.X;
        //                int y = (int)_mousePos.Y;

        //                if ((_arcWindow?.IsActive ?? false) && _isPointerInsidePage) {
        //                    _scriptExecutor?.EnqueueState(
        //                        key: "MouseMove",
        //                        functionName: Fields.MouseMove,
        //                        x, y
        //                    );
        //                    lastX = x;
        //                    lastY = y;
        //                }
        //                else if (lastInside) {
        //                    _scriptExecutor?.EnqueueState(
        //                        key: "MouseOut",
        //                        functionName: Fields.MouseOut
        //                    );
        //                }

        //                lastInside = _isPointerInsidePage;
        //            }
        //        }
        //        catch (Exception e) {
        //            ArcLog.GetLogger<PageWithPlaying>().Error("[Parallax] Loop error", e);
        //        }
        //    });
        //}

        //private void StopParallax() {
        //    if (Interlocked.CompareExchange(ref _isParallaxRunning, 0, 1) == 0) return;
        //    _scriptExecutor?.EnqueueState(
        //        key: "MouseOut",
        //        functionName: Fields.MouseOut
        //    );
        //}
        private void StartParallax() {
            _scriptExecutor?.EnqueueEvent(Fields.StartParallax);
        }

        private void StopParallax() {
            _scriptExecutor?.EnqueueEvent(Fields.StopParallax);
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

            //DebugUtil.Output($"sunriseLocal: {sunriseLocal:HH:mm}");
            //DebugUtil.Output($"sunriseLocal: {sunsetLocal:HH:mm}");

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

        #region webview2 event
        private async Task InitializeWebViewAsync() {
            var env = await CoreWebView2Environment.CreateWithOptionsAsync(null, Constants.CommonPaths.TempWebView2Dir, _environmentOptions);
            await Webview2.EnsureCoreWebView2Async(env);

            Webview2.CoreWebView2.ProcessFailed += CoreWebView2_ProcessFailed;
            Webview2.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;

            string playingFile = GetPlayingFile();
            var shellDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PLAYER_Web");
            var entry = Path.GetFileName(playingFile); // default.html or 3d_depth_map.html

            _shellServer = new PreviewServer();
            await _shellServer.StartAsync(shellDir);
            var shellUrl = _shellServer.GetUrl(entry);
            Webview2.CoreWebView2.Navigate(shellUrl);
        }

        private void CoreWebView2_ProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e) {
            // Expected behavior: DebugActiveProcess(CEF_D3DRenderingSubProcess)
            // Ref: https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2processfailedkind
            if (e.Reason == CoreWebView2ProcessFailedReason.Unresponsive)
                return;

            ArcLog.GetLogger<PageWithPlaying>().Error($"CoreWebView2 process failed: {e.Reason}");
        }

        private void CoreWebView2_DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e) {
            // Cancel user requested downloads.
            e.Cancel = true;
        }

        private async void Webview2_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args) {
            if (!args.IsSuccess) {
                ArcLog.GetLogger<PageWithPlaying>().Error($"WebView navigation failed: {args.WebErrorStatus}");
                return;
            }

            // Must be called BEFORE enqueuing ResourceLoad so the
            // WebResourceRequested filter is registered in time to
            // intercept the iframe's HTML response and inject hotreload.js.
            if (EnableHmr) {
                InitPreviewManager();
            }

            switch (_startArgs.RuntimeType) {
                case "RImage":
                    Webview2.IsHitTestVisible = true;
                    UpdateRectToWebview();
                    _scriptExecutor?.EnqueueEvent(Fields.ResourceLoad, _startArgs.RuntimeType,
                        await ServeFileAsync(_startArgs.FilePath));
                    break;
                case "RWeb":
                    Webview2.IsHitTestVisible = true;
                    UpdateRectToWebview();
                    {
                        string url;
                        if (EnableHmr && !string.IsNullOrEmpty(_hotReloadScriptContent)) {
                            // Serve project via PreviewServer with HMR injection
                            // middleware.  The shell page creates an iframe to load
                            // this URL, preserving the shell UI.
                            var dir = Path.GetDirectoryName(_startArgs.FilePath);
                            var entry = Path.GetFileName(_startArgs.FilePath);
                            _projectServer = new PreviewServer();
                            await _projectServer.StartAsync(dir!, new PreviewServerOptions {
                                InjectionScript = _hotReloadScriptContent
                            });
                            url = _projectServer.GetUrl(entry!);
                        }
                        else {
                            url = Webview2.CoreWebView2.GetVirtualHostUrl(_startArgs.FilePath);
                        }
                        if (!string.IsNullOrEmpty(url))
                            url += $"?_t={DateTime.Now.Ticks}";
                        _scriptExecutor?.EnqueueEvent(Fields.ResourceLoad, _startArgs.RuntimeType, url);
                    }
                    break;
                case "RVideo":
                    UpdateRectToWebview();
                    _scriptExecutor?.EnqueueEvent(Fields.ResourceLoad, _startArgs.RuntimeType,
                        await ServeFileAsync(_startArgs.FilePath));
                    break;
                case "RImage3D":
                    Webview2.IsHitTestVisible = true;
                    UpdateRectToWebview();
                    _scriptExecutor?.EnqueueEvent(Fields.ResourceLoad,
                        await ServeFileAsync(_startArgs.FilePath),
                        await ServeFileAsync(_startArgs.DepthFilePath));
                    break;
                default:
                    break;
            }
            LoadWpEffect(_startArgs.WpEffectFilePathUsing);
            _scriptExecutor?.EnqueueEvent(Fields.Play);

            if (!_devToolsOpened) {
                _devToolsOpened = true;
#if DEBUG
                Webview2.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
                Webview2.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                Webview2.CoreWebView2.OpenDevToolsWindow();
#else
                // Don't allow contextmenu and devtools.
                Webview2.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                Webview2.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
#endif
                // Runtime debug mode: open DevTools even in Release builds (for editor Debug button)
                if (_startArgs.IsDebug) {
                    Webview2.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
                    Webview2.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                    Webview2.CoreWebView2.OpenDevToolsWindow();
                }
            }

            _loadedTcs.TrySetResult();
        }

        private void CoreWebView2_ProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs args) {
            ArcLog.GetLogger<PageWithPlaying>().Error(args.Reason.ToString());
        }
        #endregion

        #region preview / hmr
        /// <summary>
        /// Creates the PreviewManager and wires up hotreload.js injection
        /// for the content iframe.  Must be called after the WebView2 has
        /// finished its initial navigation.
        ///
        /// This is <b>not</b> called automatically — normal library previews
        /// do not need HMR injection.  The WebBackdrop editor debug session
        /// calls this via <see cref="PreviewWithWeb"/> after the page is
        /// fully loaded.
        /// </summary>
        public void InitPreviewManager() {
            if (_previewManager != null) return; // already initialised
            if (Webview2?.CoreWebView2 == null) return;

            try {
                var bridge = new WebView2Bridge(Webview2.CoreWebView2);
                _previewManager = new PreviewManager(bridge);
                _bridge = bridge;

                // Wire up WebResourceRequested filter to inject hotreload.js
                // into HTML documents loaded via *.localhost virtual hosts.
                InjectHotReloadScript();

                // Inject shell-page listener that forwards WebView2
                // postMessage → iframe postMessage for HMR.
                _bridge.InitHmrBridge();

                ArcLog.GetLogger<PageWithPlaying>().Info("PreviewManager initialised");
            }
            catch (Exception ex) {
                ArcLog.GetLogger<PageWithPlaying>().Error($"PreviewManager init failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Uses WebResourceRequested to intercept HTML responses on
        /// *.localhost virtual hosts and injects hotreload.js.  This works
        /// because WebView2's internal virtual host resolution triggers the
        /// WebResourceRequested event (unlike real localhost HTTP requests).
        /// </summary>
        private void InjectHotReloadScript() {
            if (Webview2?.CoreWebView2 == null) return;

            try {
                // Read hotreload.js from the assembly's embedded resources.
                var asm = typeof(PageWithPlaying).Assembly;
                var resourceName = $"{asm.GetName().Name}.Utils.PreviewSystem.HotReload.hotreload.js";

                using var stream = asm.GetManifestResourceStream(resourceName);
                if (stream == null) {
                    var legacyNames = asm.GetManifestResourceNames();
                    var match = legacyNames.FirstOrDefault(
                        n => n.EndsWith("hotreload.js", StringComparison.OrdinalIgnoreCase));
                    if (match == null) {
                        ArcLog.GetLogger<PageWithPlaying>().Warn(
                            "hotreload.js not found in embedded resources; HMR injection disabled");
                        return;
                    }
                    resourceName = match;
                    using var s2 = asm.GetManifestResourceStream(resourceName);
                    if (s2 == null) return;
                    using var r2 = new StreamReader(s2, Encoding.UTF8);
                    _hotReloadScriptContent = r2.ReadToEnd();
                }
                else {
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    _hotReloadScriptContent = reader.ReadToEnd();
                }

                ArcLog.GetLogger<PageWithPlaying>().Info("HMR script loaded");
            }
            catch (Exception ex) {
                ArcLog.GetLogger<PageWithPlaying>().Warn($"HMR injection setup failed: {ex.Message}");
            }
        }

        #endregion

        #region utils
        private string GetPlayingFile() {
            return _startArgs.RuntimeType switch {
                "RImage" => PlayingFileWeb.PlayerWeb,
                "RImage3D" => PlayingFileWeb.PlayerWeb3D,
                "RVideo" => PlayingFileWeb.PlayerWeb,
                "RWeb" => PlayingFileWeb.PlayerWeb,   // RWeb 同样用 default.html 作为宿主页
                _ => throw new ArgumentException(nameof(_startArgs.RuntimeType)),
            };
        }

        private void UpdateRectToWebview() {
            if (Webview2 == null || Webview2.CoreWebView2 == null) return;

            _scriptExecutor?.EnqueueEvent(Fields.UpdateDimensions, _pageRegion.Right - _pageRegion.Left, _pageRegion.Bottom - _pageRegion.Top);
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
                ArcLog.GetLogger<PageWithPlaying>().Error(ex);
            }
        }

        private void ExecuteCheckBoxSet(string propertyName, bool val) {
            switch (propertyName) {
                case "Parallax":
                    RunParallax(val);
                    break;
                case "TimeAtmoPerception":
                    RunTimePerception(val);
                    break;
                default:
                    break;
            }
        }

        private void OnUnloaded() {
            Payload = null;
            _ = _shellServer?.StopAsync();
            _ = _previewManager?.DisposeAsync();
            _ = _projectServer?.StopAsync();
            foreach (var s in _resourceServers) _ = s.StopAsync();
            _resourceServers.Clear();
            CrossThreadInvoker.InvokeOnUIThread(() => {
                Webview2?.Close();
            });
        }
        #endregion

        private WebViewScriptExecutor? _scriptExecutor;
        private StartArgsWeb _startArgs = null!;
        private ArcWindow? _arcWindow;
        private Point _mousePos;
        private Rect _pageRegion;
        //private volatile int _isParallaxRunning = 0; // 0 = stopped, 1 = running
        private bool _isPointerInsidePage;
        private bool _devToolsOpened;
        private readonly TaskCompletionSource _loadedTcs = new();

        /// <summary>
        /// Completes when the WebView2 has finished its initial navigation.
        /// External callers (e.g. <see cref="PreviewWithWeb"/>) can await
        /// this to safely call <see cref="InitPreviewManager"/>.
        /// </summary>
        public Task LoadedTask => _loadedTcs.Task;

        private static readonly CoreWebView2EnvironmentOptions _environmentOptions = new() {
            AdditionalBrowserArguments = "--disk-cache-size=1 --autoplay-policy=no-user-gesture-required"
        };

        // HMR / Preview system
        private PreviewManager? _previewManager;
        private WebView2Bridge? _bridge;
        private string? _hotReloadScriptContent;
        private PreviewServer? _shellServer;
        private PreviewServer? _projectServer;
        private readonly List<PreviewServer> _resourceServers = [];

        /// <summary>
        /// Starts a PreviewServer for the file's directory and returns an
        /// http://127.0.0.1:{port}/{filename} URL.  The server is tracked
        /// for cleanup in <see cref="OnUnloaded"/>.
        /// </summary>
        private async Task<string> ServeFileAsync(string? filePath) {
            if (string.IsNullOrEmpty(filePath)) return string.Empty;
            var dir = Path.GetDirectoryName(filePath)!;
            var entry = Path.GetFileName(filePath);
            var server = new PreviewServer();
            await server.StartAsync(dir);
            _resourceServers.Add(server);
            return server.GetUrl(entry);
        }

        /// <summary>
        /// Set by <see cref="PreviewWithWeb"/> before the page finishes
        /// loading to enable hot-reload script injection for WebBackdrop
        /// editor debug sessions.
        /// </summary>
        internal bool EnableHmr { get; set; }
    }
}
