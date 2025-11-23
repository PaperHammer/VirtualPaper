using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.PlayerWeb.Utils;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using Windows.Foundation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.PlayerWeb.Views {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : ArcPage {
        public override ArcPageContext Context { get; }
        public override Type PageType => typeof(MainPage);

        public MainPage() {
            this.InitializeComponent();
            Context = new ArcPageContext(this, this.MainHost.LoadingControlHost);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e) {
            _startArgs = (e.Parameter as MainWindow)?.Args;
        }

        private void ArcPage_Unloaded(object sender, RoutedEventArgs e) {
            OnUnloaded();
        }

        private async void ArcPage_Loaded(object sender, RoutedEventArgs e) {
            var ctx = PageContextManager.GetContext<MainPage>();
            var loadingCtx = ctx?.LoadingContext;
            if (loadingCtx == null)
                return;

            await loadingCtx.RunAsync(async token => {
                await _loadedTcs.Task;
            });

            await InitializeWebViewAsync();
        }

        private void ArcPage_SizeChanged(object sender, SizeChangedEventArgs e) {
            _pageRegion = new Rect() {
                Width = e.NewSize.Width,
                Height = e.NewSize.Height
            };
            UpdateRectToWebview();
        }
        private async Task InitializeWebViewAsync() {
            var env = await CoreWebView2Environment.CreateWithOptionsAsync(null, Constants.CommonPaths.TempWebView2Dir, _environmentOptions);
            await Webview2.EnsureCoreWebView2Async(env);

            Webview2.CoreWebView2.ProcessFailed += (s, e) => {
                App.WriteToParent(new VirtualPaperMessageConsole() {
                    MsgType = ConsoleMessageType.Error,
                    Message = $"Process fail: {e.Reason}",
                });
                CrossThreadInvoker.InvokeOnUIThread(App.AppInstance.Exit);
            };

            Webview2.NavigationCompleted += Webview2_NavigationCompleted;

            string playingFile = GetPlayingFile();
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, playingFile).Replace("\\", "/");
            Webview2.CoreWebView2.Navigate(new Uri(fullPath).AbsoluteUri);
        }

        private void Webview2_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args) {
            switch (_startArgs.RuntimeType) {
                case "RImage":
                case "RVideo":
                    UpdateRectToWebview();
                    ExecuteScript(Fileds.ResourceLoad, _startArgs.RuntimeType, _startArgs.FilePath);
                    break;
                case "RImage3D":
                    UpdateRectToWebview();
                    ExecuteScript(Fileds.ResourceLoad, _startArgs.FilePath, _startArgs.DepthFilePath);
                    break;
                default:
                    break;
            }
            //LoadWpEffect(_startArgs.WpEffectFilePathUsing);
            ExecuteScript(Fileds.Play);

            App.WriteToParent(new VirtualPaperMessageProcId() {
                ProcId = Webview2.CoreWebView2.BrowserProcessId,
            });

#if DEBUG
            Webview2.CoreWebView2.OpenDevToolsWindow();
#endif

            _loadedTcs.TrySetResult();
        }

        private string GetPlayingFile() {
            return _startArgs.RuntimeType switch {
                "RImage" => Constants.PlayingFile.PlayerWeb,
                "RImage3D" => Constants.PlayingFile.PlayerWeb3D,
                "RVideo" => Constants.PlayingFile.PlayerWeb,
                _ => throw new ArgumentException(nameof(_startArgs.RuntimeType)),
            };
        }

        private void UpdateRectToWebview() {
            if (Webview2 == null || Webview2.CoreWebView2 == null) return;

            ExecuteScript(Fileds.UpdateDimensions, _pageRegion.Right - _pageRegion.Left, _pageRegion.Bottom - _pageRegion.Top);
        }

        private void Webview2_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) {
            e.Handled = true; // 阻止（鼠标等）指针操作
        }

        private void Webview2_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e) {
            e.Handled = true;  // 阻止键盘操作
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

        private void OnUnloaded() {
            //_ctsConsoleIn?.Cancel();
            //StopParallaxLoop();

            CrossThreadInvoker.InvokeOnUIThread(() => {
                Webview2?.Close();
            });
        }

        private Rect _pageRegion;
        private StartArgs _startArgs;
        private readonly TaskCompletionSource _loadedTcs;
        private static readonly CoreWebView2EnvironmentOptions _environmentOptions = new() {
            AdditionalBrowserArguments = "--disable-web-security --allow-file-access --allow-file-access-from-files --disk-cache-size=1"
        }; // workaround: avoid cache
    }
}
