using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using VirtualPaper.Common;
using VirtualPaper.Common.Events.EffectValue.Base;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.IPC.Interfaces;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.PlayerWeb.Core.Utils;
using VirtualPaper.PlayerWeb.Core.Utils.Interfaces;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils.Extensions;
using Windows.Foundation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.PlayerWeb.Core.WebView.Pages {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PageWithPlaying : ArcPage, IEffectService,
        IIpcSubscribe<VirtualPaperApplyCmd>,
        IIpcSubscribe<VirtualPaperReloadCmd>,
        IIpcSubscribe<VirtualPaperSuspendCmd>,
        IIpcSubscribe<VirtualPaperResumeCmd>,
        IIpcSubscribe<VirtualPaperMutedCmd>,
        IIpcSubscribe<VirtualPaperUpdateCmd>,
        IIpcSubscribe<VirtualPaperParallaxSuspendCmd>,
        IIpcSubscribe<VirtualPaperParallaxResumeCmd> {
        public override ArcPageContext Context { get; }
        public override Type PageType => typeof(PageWithPlaying);
        protected override bool IsMultiInstance => true;
        public NavigationPayload? Payload { get; private set; }

        public PageWithPlaying() {
            this.InitializeComponent();
            Context = new ArcPageContext(this, this.MainHost.LoadingControlHost);
        }

        protected override async Task OnEnterAsync(NavigationPayload? payload) {
            await base.OnEnterAsync(payload);
            Payload = payload;
            if (payload != null) {
                payload.TryGet(NaviPayLoadKey.StartArgs.ToString(), out _startArgs);
                // 作为运行时桌面壁纸使用
                if (payload.TryGet(NaviPayLoadKey.IIpcObserver.ToString(), out IIpcObserver ipcObserver)) {
                    ipcObserver.Register(this);
                }
                // 预览窗口使用
                payload.TryGet(NaviPayLoadKey.ArcWindow.ToString(), out _arcWindow);
                payload.Set(NaviPayLoadKey.IEffectService.ToString(), this);
                payload.Set(NaviPayLoadKey.AvailableConfigTab.ToString(), DataConfigTab.GeneralEffect | DataConfigTab.GeneralInfo);
            }

            SidePanel.Visibility = _startArgs.IsPreview ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        }

        protected override void ArcPage_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            base.ArcPage_Unloaded(sender, e);
            OnUnloaded();
        }

        private void ArcPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            var loadingCtx = Context?.LoadingContext;
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
            Interlocked.CompareExchange(ref _isPointerInsidePage, 1, 0);
        }

        private void InputLayer_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) {
            if (_isParallaxRunning != 1) return;

            var point = e.GetCurrentPoint(this);
            _mousePos = point.Position;
        }

        private void InputLayer_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) {
            Interlocked.CompareExchange(ref _isPointerInsidePage, 0, 1);
        }

        #region ipc
        ValueTask IIpcSubscribe<VirtualPaperApplyCmd>.OnIpcAsync(VirtualPaperApplyCmd message) {
            _scriptExecutor?.EnqueueEvent(Fileds.ApplyFilter);
            _scriptExecutor?.EnqueueEvent(Fileds.Play);
            return ValueTask.CompletedTask;
        }

        ValueTask IIpcSubscribe<VirtualPaperReloadCmd>.OnIpcAsync(VirtualPaperReloadCmd message) {
            CrossThreadInvoker.InvokeOnUIThread(() => {
                Webview2?.Reload();
            });
            return ValueTask.CompletedTask;
        }

        ValueTask IIpcSubscribe<VirtualPaperSuspendCmd>.OnIpcAsync(VirtualPaperSuspendCmd message) {
            HandlePlaybackCommand(true);
            return ValueTask.CompletedTask;
        }

        ValueTask IIpcSubscribe<VirtualPaperResumeCmd>.OnIpcAsync(VirtualPaperResumeCmd message) {
            HandlePlaybackCommand(true);
            return ValueTask.CompletedTask;
        }

        ValueTask IIpcSubscribe<VirtualPaperMutedCmd>.OnIpcAsync(VirtualPaperMutedCmd message) {
            HandleMuteCommand(message);
            return ValueTask.CompletedTask;
        }

        ValueTask IIpcSubscribe<VirtualPaperUpdateCmd>.OnIpcAsync(VirtualPaperUpdateCmd message) {
            HandleUpdateCommand(message);
            return ValueTask.CompletedTask;
        }

        ValueTask IIpcSubscribe<VirtualPaperParallaxSuspendCmd>.OnIpcAsync(VirtualPaperParallaxSuspendCmd message) {
            _isFocusOnDesk = false;
            return ValueTask.CompletedTask;
        }

        ValueTask IIpcSubscribe<VirtualPaperParallaxResumeCmd>.OnIpcAsync(VirtualPaperParallaxResumeCmd message) {
            _isFocusOnDesk = true;
            return ValueTask.CompletedTask;
        }

        private void HandlePlaybackCommand(bool pause) {
            _scriptExecutor?.EnqueueEvent(Fileds.PlaybackChanged, pause);
        }

        private void HandleMuteCommand(VirtualPaperMutedCmd muted) {
            _scriptExecutor?.EnqueueEvent(Fileds.AudioMuteChanged, muted.IsMuted);
        }

        private void HandleUpdateCommand(VirtualPaperUpdateCmd update) {
            if (_startArgs == null) return;

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

        #region effect change from ui
        public void UpdateEffectValue(EffectValueChanged<double> e) {
            //ExecuteScript(Fileds.PropertyListener, e.PropertyName, e.Value);
            _scriptExecutor?.EnqueueEvent(Fileds.PropertyListener, e.PropertyName, e.Value);
        }

        public void UpdateEffectValue(EffectValueChanged<int> e) {
            //ExecuteScript(Fileds.PropertyListener, e.PropertyName, e.Value);
            _scriptExecutor?.EnqueueEvent(Fileds.PropertyListener, e.PropertyName, e.Value);
        }

        public void UpdateEffectValue(EffectValueChanged<bool> e) {
            ExecuteCheckBoxSet(e.PropertyName, e.Value);
        }

        public void UpdateEffectValue(EffectValueChanged<string> e) {
        }
        #endregion

        #region parallax
        private void StartParallaxLoop() {
            if (Interlocked.CompareExchange(ref _isParallaxRunning, 1, 0) == 1) return;

            Task.Run(() => {
                try {
                    int lastX = int.MinValue;
                    int lastY = int.MinValue;
                    bool lastInside = false;

                    while (_isParallaxRunning == 1) {
                        int x = (int)_mousePos.X;
                        int y = (int)_mousePos.Y;
#if DEBUG
                        //Debug.WriteLine($"mouse at {_mousePos}");
#endif
                        bool inside = _isPointerInsidePage == 1;
                        if (inside) {
                            //ExecuteScript(Fileds.MouseMove, x, y);
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
                            //ExecuteScript(Fileds.MouseOut);
                        }

                        lastInside = inside;
                        //await Task.Delay(42, _parallaxCts.Token);
                    }
                }
                catch (Exception ex) when (ex is OperationCanceledException) {
                }
                catch (Exception e) {
                    ArcLog.GetLogger<PageWithPlaying>().Error("[Parallax] Loop error", e);
                }
            }, _parallaxCts.Token);
        }

        private void StopParallax() {
            if (Interlocked.CompareExchange(ref _isParallaxRunning, 0, 1) == 0) return;
            _scriptExecutor?.EnqueueState(
                key: "MouseOut",
                functionName: Fileds.MouseOut
            );
            //ExecuteScript(Fileds.MouseOut);
        }

        private void UpdateParallaxState() {
            bool enable = _isParallaxOn &&
                ((_startArgs.IsPreview && (_arcWindow?.IsActive ?? false)) || (!_startArgs.IsPreview && _isFocusOnDesk));

            if (enable) {
                StartParallaxLoop();
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

            Webview2.CoreWebView2.ProcessFailed += (s, e) => {
                ArcLog.GetLogger<PageWithPlaying>().Error(e.Reason.ToString());
            };

            string playingFile = GetPlayingFile();
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, playingFile).Replace("\\", "/");
            Webview2.CoreWebView2.Navigate(new Uri(fullPath).AbsoluteUri);
        }

        private void Webview2_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args) {
            switch (_startArgs.RuntimeType) {
                case "RImage":
                case "RVideo":
                    UpdateRectToWebview();
                    //ExecuteScript(Fileds.ResourceLoad, _startArgs.RuntimeType, _startArgs.FilePath);
                    _scriptExecutor?.EnqueueEvent(Fileds.ResourceLoad, _startArgs.RuntimeType, _startArgs.FilePath);
                    break;
                case "RImage3D":
                    UpdateRectToWebview();
                    //ExecuteScript(Fileds.ResourceLoad, _startArgs.FilePath, _startArgs.DepthFilePath);
                    _scriptExecutor?.EnqueueEvent(Fileds.ResourceLoad, _startArgs.FilePath, _startArgs.DepthFilePath);
                    break;
                default:
                    break;
            }
            LoadWpEffect(_startArgs.WpEffectFilePathUsing);
            //ExecuteScript(Fileds.Play);
            _scriptExecutor?.EnqueueEvent(Fileds.Play);

#if DEBUG
            Webview2.CoreWebView2.OpenDevToolsWindow();
#endif

            _loadedTcs.TrySetResult();
        }

        private void Webview2_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e) {
            e.Handled = true;  // 阻止键盘操作
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
                    _isParallaxOn = val;
                    UpdateParallaxState();
                    break;
                default:
                    break;
            }
        }

        private void OnUnloaded() {
            _ctsConsoleIn?.Cancel();
            _parallaxCts.Cancel();
            if (Payload != null && Payload.TryGet(NaviPayLoadKey.IIpcObserver.ToString(), out IIpcObserver ipcObserver)) {
                ipcObserver.Register(this);
            }
            CrossThreadInvoker.InvokeOnUIThread(() => {
                Webview2?.Close();
            });
        }
        #endregion

        private WebViewScriptExecutor? _scriptExecutor;
        private StartArgsWeb _startArgs = null!;
        private ArcWindow? _arcWindow;
        private bool _isParallaxOn = false;
        private bool _isFocusOnDesk;
        private Point _mousePos;
        private Rect _pageRegion;
        private volatile int _isParallaxRunning = 0; // 0 = stopped, 1 = running        
        private volatile int _isPointerInsidePage; // 0 = false, 1 = true   
        private static readonly CancellationTokenSource _ctsConsoleIn = new();
        private readonly TaskCompletionSource _loadedTcs = new();
        private readonly CancellationTokenSource _parallaxCts = new();
        private static readonly CoreWebView2EnvironmentOptions _environmentOptions = new() {
            AdditionalBrowserArguments = "--disable-web-security --allow-file-access --allow-file-access-from-files --disk-cache-size=1"
        }; // workaround: avoid cache
    }
}
