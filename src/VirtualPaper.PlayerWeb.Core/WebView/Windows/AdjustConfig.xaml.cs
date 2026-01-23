using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Models.Cores;
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
    public sealed partial class AdjustConfig : ArcWindow, IApplyService {
        public event Func<AdjustConfig, object?, ValueTask>? Applied;

        public override ArcWindowHost ContentHost => this.MainHost;
        public override ArcWindowManagerKey Key => _windowKey;

        public AdjustConfig(string jsonString) {
            _startArgs = JsonSerializer.Deserialize<StartArgsWeb>(jsonString);
            _windowKey = new ArcWindowManagerKey(ArcWindowKey.PlayerWebCoreAdjust, _startArgs.FilePath + _startArgs.RuntimeType);
            this.InitializeComponent();
            InitializeWindow();

            AfterFeReady();
        }

        private void NaviContent_Loaded(object sender, RoutedEventArgs e) {
            try {
                var payload = new NavigationPayload() {
                    [NaviPayLoadKey.StartArgs.ToString()] = _startArgs,
                    [NaviPayLoadKey.IWpBasicData.ToString()] = _wpBasicData,
                    [NaviPayLoadKey.ApplyService.ToString()] = this,
                };
                NaviContent.Navigate(typeof(PageOnlyDataConfig), payload);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<AdjustConfig>().Error(ex);
            }
        }

        private async void AfterFeReady() {
            _wpBasicData ??= await JsonSaver.LoadAsync<WpBasicData>(_startArgs.WpBasicDataFilePath, WpBasicDataContext.Default);
            string windowTitle = !string.IsNullOrEmpty(_wpBasicData.Title) ? $"{_wpBasicData.Title} (Adjust)" :
                (!string.IsNullOrEmpty(_startArgs.FilePath) ? $"{Path.GetFileName(_startArgs.FilePath)} (Adjust)" : "Virtual Paper PlayerWeb (Adjust)");
            this.Title = this.MainHost.Title = windowTitle;
        }

        public async ValueTask ApplyAsync(object? context = null) {
            if (Applied is not null) {
                foreach (var handler in Applied.GetInvocationList()) {
                    if (handler is Func<AdjustConfig, object?, ValueTask> asyncHandler) {
                        await asyncHandler(this, context);
                    }
                }
            }
        }

        private readonly StartArgsWeb? _startArgs;
        private readonly ArcWindowManagerKey _windowKey;
        private WpBasicData _wpBasicData;
    }
}
