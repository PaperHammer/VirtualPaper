using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using VirtualPaper.Common;
using VirtualPaper.Common.Events.EffectValue.Base;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Common.Utils;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.PlayerWeb.Core.Utils;
using VirtualPaper.PlayerWeb.Core.Utils.Interfaces;
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
            if (_isParallaxRunning != 1) return;

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
        private void StartParallax() {
            if (Interlocked.CompareExchange(ref _isParallaxRunning, 1, 0) == 1) return;

            Task.Run(async () => {
                try {
                    int lastX = int.MinValue;
                    int lastY = int.MinValue;
                    bool lastInside = false;

                    while (_isParallaxRunning == 1) {
                        int x = (int)_mousePos.X;
                        int y = (int)_mousePos.Y;

                        if ((_arcWindow?.IsActive ?? false) && _isPointerInsidePage) {
                            _scriptExecutor?.EnqueueState(
                                key: "MouseMove",
                                functionName: Fields.MouseMove,
                                x, y
                            );
                            lastX = x;
                            lastY = y;
                        }
                        else if (lastInside) {
                            _scriptExecutor?.EnqueueState(
                                key: "MouseOut",
                                functionName: Fields.MouseOut
                            );
                        }

                        lastInside = _isPointerInsidePage;
                    }
                }
                catch (Exception e) {
                    ArcLog.GetLogger<PageWithPlaying>().Error("[Parallax] Loop error", e);
                }
            });
        }

        private void StopParallax() {
            if (Interlocked.CompareExchange(ref _isParallaxRunning, 0, 1) == 0) return;
            _scriptExecutor?.EnqueueState(
                key: "MouseOut",
                functionName: Fields.MouseOut
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
                _scriptExecutor?.EnqueueEvent(Fields.PropertyListener, Fields.TimePerception, payload);
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

            DebugUtil.Output($"sunriseLocal: {sunriseLocal:HH:mm}");
            DebugUtil.Output($"sunriseLocal: {sunsetLocal:HH:mm}");

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
            _scriptExecutor?.EnqueueEvent(Fields.PropertyListener, Fields.TimePerception, payload);
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
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, playingFile).Replace("\\", "/");
            Webview2.CoreWebView2.Navigate(new Uri(fullPath).AbsoluteUri);
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

        private void Webview2_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args) {
            if (!args.IsSuccess) {
                ArcLog.GetLogger<PageWithPlaying>().Error($"WebView navigation failed: {args.WebErrorStatus}");
                return;
            }

            switch (_startArgs.RuntimeType) {
                case "RImage":
                case "RVideo":
                    UpdateRectToWebview();
                    _scriptExecutor?.EnqueueEvent(Fields.ResourceLoad, _startArgs.RuntimeType, _startArgs.FilePath);
                    break;
                case "RImage3D":
                    UpdateRectToWebview();
                    _scriptExecutor?.EnqueueEvent(Fields.ResourceLoad, _startArgs.FilePath, _startArgs.DepthFilePath);
                    break;
                default:
                    break;
            }
            LoadWpEffect(_startArgs.WpEffectFilePathUsing);
            _scriptExecutor?.EnqueueEvent(Fields.Play);

#if DEBUG
            Webview2.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
            Webview2.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            Webview2.CoreWebView2.OpenDevToolsWindow();
#else
            // Don't allow contextmenu and devtools.
            Webview2.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            Webview2.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
#endif

            _loadedTcs.TrySetResult();
        }

        private void CoreWebView2_ProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs args) {
            ArcLog.GetLogger<PageWithPlaying>().Error(args.Reason.ToString());
        }
        #endregion

        #region utils
        private string GetPlayingFile() {
            return _startArgs.RuntimeType switch {
                "RImage" => PlayingFileWeb.PlayerWeb,
                "RImage3D" => PlayingFileWeb.PlayerWeb3D,
                "RVideo" => PlayingFileWeb.PlayerWeb,
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
        private volatile int _isParallaxRunning = 0; // 0 = stopped, 1 = running
        private bool _isPointerInsidePage;
        private readonly TaskCompletionSource _loadedTcs = new();
        private static readonly CoreWebView2EnvironmentOptions _environmentOptions = new() {
            AdditionalBrowserArguments = "--disable-web-security --allow-file-access --allow-file-access-from-files --disk-cache-size=1 --autoplay-policy=no-user-gesture-required "
        }; // workaround: avoid cache
    }
}
