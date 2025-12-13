using System;
using System.IO;
using System.Text;
using System.Text.Json;
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
using VirtualPaper.PlayerWeb.Core.WebView.Windows;
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
    public sealed partial class MainPage : ArcPage, IEffectService, IMainPage {
        public override ArcPageContext Context { get; }
        public override Type PageType => typeof(MainPage);
        protected override bool IsMultiInstance => true;
        public StartArgsWeb StartArgs { get; private set; }
        public DataConfigTab AvailableConfigTab { get; } = DataConfigTab.GeneralEffect | DataConfigTab.GeneralInfo;

        public MainPage() {
            this.InitializeComponent();
            Context = new ArcPageContext(this, this.MainHost.LoadingControlHost);
        }

        protected override async Task OnEnterAsync(NavigationPayload? payload) {
            await base.OnEnterAsync(payload);
            if(payload != null) {
                payload.Get("PreviewWithWeb", out _instance);
                StartArgs = payload.Get<StartArgsWeb>("StartArgs");
            }
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
                _ = InitializeWebViewAsync();
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

        private void ArcPage_GotFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            ParallaxControl();
        }

        private void ArcPage_LostFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
            ParallaxControl();
        }

        #region parallax
        private void ParallaxControl() {
            try {
                if (_isParallaxOn && this.FocusState == Microsoft.UI.Xaml.FocusState.Pointer) {
                    if (Interlocked.CompareExchange(ref _isParallaxRunning, 1, 0) == 1) return;

                    _ = Task.Run(async () => {
                        try {
                            while (!_parallaxCts.Token.IsCancellationRequested && _isParallaxRunning == 1) {
                                var pos = RawInput.GetMousePos();
                                int mouseX = pos.X, mouseY = pos.Y;

                                if (_pageRegion.Left <= mouseX && mouseX <= _pageRegion.Right &&
                                    _pageRegion.Top <= mouseY && mouseY <= _pageRegion.Bottom) {
                                    ExecuteScript(
                                       Fileds.MouseMove, mouseX, mouseY);
                                }
                                else {
                                    ExecuteScript(Fileds.MouseOut);
                                }

                                await Task.Delay(100);
                            }
                        }
                        catch (Exception e) {
                            ArcLog.GetLogger<MainPage>().Error($"[ParallaxControl] Error: {e.Message}", e);
                        }
                    });
                }
                else {
                    if (Interlocked.CompareExchange(ref _isParallaxRunning, 0, 1) == 0) return;

                    ExecuteScript(Fileds.MouseOut);
                }
            }
            catch (Exception e) {
                ArcLog.GetLogger<MainPage>().Error($"Function ['ParallaxControl'] an Error occured: {e.Message}", e);
            }
        }
        #endregion

        #region webview2 event
        private async Task InitializeWebViewAsync() {
            var env = await CoreWebView2Environment.CreateWithOptionsAsync(null, Constants.CommonPaths.TempWebView2Dir, _environmentOptions);
            await Webview2.EnsureCoreWebView2Async(env);

            Webview2.CoreWebView2.ProcessFailed += (s, e) => {
                ArcLog.GetLogger<MainPage>().Error(e.Reason.ToString());
            };

            string playingFile = GetPlayingFile();
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, playingFile).Replace("\\", "/");
            Webview2.CoreWebView2.Navigate(new Uri(fullPath).AbsoluteUri);
        }

        private void Webview2_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args) {
            switch (StartArgs.RuntimeType) {
                case "RImage":
                case "RVideo":
                    UpdateRectToWebview();
                    ExecuteScript(Fileds.ResourceLoad, StartArgs.RuntimeType, StartArgs.FilePath);
                    break;
                case "RImage3D":
                    UpdateRectToWebview();
                    ExecuteScript(Fileds.ResourceLoad, StartArgs.FilePath, StartArgs.DepthFilePath);
                    break;
                default:
                    break;
            }
            LoadWpEffect(StartArgs.WpEffectFilePathUsing);
            ExecuteScript(Fileds.Play);

#if DEBUG
            Webview2.CoreWebView2.OpenDevToolsWindow();
#endif

            _loadedTcs.TrySetResult();
        }

        private void Webview2_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) {
            e.Handled = true; // 阻止（鼠标等）指针操作
        }

        private void Webview2_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e) {
            e.Handled = true;  // 阻止键盘操作
        }
        #endregion

        private string GetPlayingFile() {
            return StartArgs.RuntimeType switch {
                "RImage" => PlayingFileWeb.PlayerWeb,
                "RImage3D" => PlayingFileWeb.PlayerWeb3D,
                "RVideo" => PlayingFileWeb.PlayerWeb,
                _ => throw new ArgumentException(nameof(StartArgs.RuntimeType)),
            };
        }

        private void UpdateRectToWebview() {
            if (Webview2 == null || Webview2.CoreWebView2 == null) return;

            ExecuteScript(Fileds.UpdateDimensions, _pageRegion.Right - _pageRegion.Left, _pageRegion.Bottom - _pageRegion.Top);
        }

        private string ExecuteScript(string functionName, params object[] parameters) {
            StringBuilder sb_script = new();
            sb_script.Append(functionName);
            sb_script.Append('(');
            for (int i = 0; i < parameters.Length; i++) {
                sb_script.Append(JsonSerializer.Serialize(parameters[i]));
                if (i < parameters.Length - 1) {
                    sb_script.Append(", ");
                }
            }
            sb_script.Append(");");

            string script = string.Empty;
            CrossThreadInvoker.InvokeOnUIThread(async () => {
                if (Webview2.CoreWebView2 == null) { // ???
                    await Webview2.EnsureCoreWebView2Async();
                }
                await Webview2.ExecuteScriptAsync(sb_script.ToString());
            });

            return script;
        }

        private void LoadWpEffect(string wpEffectFilePath) {
            try {
                if (wpEffectFilePath == null) return;

                foreach (var item in JsonNodeUtil.GetReadonlyJson(wpEffectFilePath).EnumerateObject()) {
                    string uiElementType = item.Value.GetProperty("Type").ToString();
                    if (!uiElementType.Equals("Button", StringComparison.OrdinalIgnoreCase) && !uiElementType.Equals("Label", StringComparison.OrdinalIgnoreCase)) {
                        if (uiElementType.Equals("Slider", StringComparison.OrdinalIgnoreCase) ||
                            uiElementType.Equals("Dropdown", StringComparison.OrdinalIgnoreCase)) {
                            ExecuteScript(Fileds.PropertyListener, item.Name, item.Value.GetProperty("Value").ToString());
                        }
                        else if (uiElementType.Equals("Checkbox", StringComparison.OrdinalIgnoreCase)) {
                            ExecuteCheckBoxSet(item.Name, bool.Parse(item.Value.GetProperty("Value").ToString()));
                        }
                        else if (uiElementType.Equals("Color", StringComparison.OrdinalIgnoreCase) || uiElementType.Equals("Textbox", StringComparison.OrdinalIgnoreCase)) {
                            ExecuteScript(Fileds.PropertyListener, item.Name, item.Value.GetProperty("Value").ToString());
                        }
                    }
                }
            }
            catch (Exception ex) {
                ArcLog.GetLogger<MainPage>().Error(ex);
            }
        }

        private void ExecuteCheckBoxSet(string propertyName, bool val) {
            switch (propertyName) {
                case "Parallax":
                    _isParallaxOn = val;
                    break;
                default:
                    break;
            }
        }

        private void OnUnloaded() {
            _ctsConsoleIn?.Cancel();
            _parallaxCts.Cancel();

            CrossThreadInvoker.InvokeOnUIThread(() => {
                Webview2?.Close();
            });
        }

        public void Close() {
            _instance.Close();
        }

        public void UpdateEffectValue(EffectValueChanged<double> e) {
            ExecuteScript(Fileds.PropertyListener, e.PropertyName, e.Value);
        }

        public void UpdateEffectValue(EffectValueChanged<int> e) {
            ExecuteScript(Fileds.PropertyListener, e.PropertyName, e.Value);
        }

        public void UpdateEffectValue(EffectValueChanged<bool> e) {
            ExecuteCheckBoxSet(e.PropertyName, e.Value);
        }

        public void UpdateEffectValue(EffectValueChanged<string> e) {
        }

        private PreviewWithWeb _instance = null!;
        private Rect _pageRegion;
        private static bool _isParallaxOn = false;
        private static int _isParallaxRunning = 0;
        private static readonly CancellationTokenSource _ctsConsoleIn = new();
        private readonly TaskCompletionSource _loadedTcs = new();
        private readonly CancellationTokenSource _parallaxCts = new();
        private static readonly CoreWebView2EnvironmentOptions _environmentOptions = new() {
            AdditionalBrowserArguments = "--disable-web-security --allow-file-access --allow-file-access-from-files --disk-cache-size=1"
        }; // workaround: avoid cache
    }
}
