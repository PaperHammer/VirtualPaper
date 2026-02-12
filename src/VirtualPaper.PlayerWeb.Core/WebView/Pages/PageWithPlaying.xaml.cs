using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using VirtualPaper.Common;
using VirtualPaper.Common.Events.EffectValue.Base;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.PlayerWeb;
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
                    _scriptExecutor?.EnqueueEvent(Fileds.PropertyListener, e.PropertyName, e.Value);
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
                    _scriptExecutor?.EnqueueEvent(Fileds.ResourceLoad, _startArgs.RuntimeType, _startArgs.FilePath);
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

            _scriptExecutor?.EnqueueEvent(Fileds.UpdateDimensions, _pageRegion.Right - _pageRegion.Left, _pageRegion.Bottom - _pageRegion.Top);
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
                ArcLog.GetLogger<PageWithPlaying>().Error(ex);
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
