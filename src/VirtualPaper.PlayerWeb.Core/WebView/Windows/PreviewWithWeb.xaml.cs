using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.PlayerWeb.Core.Interfaces;
using VirtualPaper.PlayerWeb.Core.WebView.Pages;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;
using VirtualPaper.UIComponent.Utils.Extensions;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.PlayerWeb.Core.WebView.Windows {
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PreviewWithWeb : ArcWindow, IApplyService {
        public event Func<PreviewWithWeb, object?, ValueTask>? Applied;

        public override ArcWindowHost ContentHost => this.MainHost;
        public override ArcWindowManagerKey Key => _windowKey;

        public PreviewWithWeb(string jsonString) {
            _startArgs = JsonSerializer.Deserialize<StartArgsWeb>(jsonString);
            _windowKey = new ArcWindowManagerKey(ArcWindowKey.PlayerWebCore, _startArgs.FilePath + _startArgs.RuntimeType);
            this.InitializeComponent();
            InitializeWindow();
        }

        private void NaviContent_Loaded(object sender, RoutedEventArgs e) {
            try {
                var payload = new NavigationPayload() {
                    [NaviPayLoadKey.StartArgs.ToString()] = _startArgs,
                    [NaviPayLoadKey.ArcWindow.ToString()] = this,
                    [NaviPayLoadKey.ApplyService.ToString()] = this,
                };
                NaviContent.Navigate(typeof(PageWithPlaying), payload);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<PageWithPlaying>().Error(ex);
            }
        }

        public async ValueTask ApplyAsync(object? context = null) {
            if (Applied is not null) {
                foreach (var handler in Applied.GetInvocationList()) {
                    if (handler is Func<PreviewWithWeb, object?, ValueTask> asyncHandler) {
                        await asyncHandler(this, context);
                    }
                }
            }
        }

        private readonly StartArgsWeb? _startArgs;
        private readonly ArcWindowManagerKey _windowKey;
    }
}
