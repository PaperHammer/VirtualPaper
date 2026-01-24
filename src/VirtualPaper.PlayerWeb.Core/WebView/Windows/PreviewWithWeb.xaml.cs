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
    public sealed partial class PreviewWithWeb : ArcWindow, IApplyService {
        public event EventHandler<ApplyEventArgs>? Applied;

        public override ArcWindowHost ContentHost => this.MainHost;
        public override ArcWindowManagerKey Key => _windowKey;

        public PreviewWithWeb(string jsonString) {
            _startArgs = JsonSerializer.Deserialize<StartArgsWeb>(jsonString);
            _windowKey = new ArcWindowManagerKey(ArcWindowKey.PlayerWebCore, _startArgs.FilePath + _startArgs.RuntimeType);
            this.InitializeComponent();
            InitializeWindow();

            AfterFeReady();
        }

        private void NaviContent_Loaded(object sender, RoutedEventArgs e) {
            try {
                var payload = new NavigationPayload() {
                    [NaviPayLoadKey.StartArgs.ToString()] = _startArgs,
                    [NaviPayLoadKey.IWpBasicData.ToString()] = _wpBasicData,
                    [NaviPayLoadKey.ArcWindow.ToString()] = this,
                    [NaviPayLoadKey.ApplyService.ToString()] = this,
                };
                NaviContent.Navigate(typeof(PageWithPlaying), payload);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<PageWithPlaying>().Error(ex);
            }
        }
        
        private async void AfterFeReady() {
            _wpBasicData ??= await JsonSaver.LoadAsync<WpBasicData>(_startArgs.WpBasicDataFilePath, WpBasicDataContext.Default);
            string windowTitle = !string.IsNullOrEmpty(_wpBasicData.Title) ? $"{_wpBasicData.Title} (Preview)" :
                (!string.IsNullOrEmpty(_startArgs.FilePath) ? $"{Path.GetFileName(_startArgs.FilePath)} (Preview)" : "Virtual Paper PlayerWeb (Preview)");
            this.Title = this.MainHost.Title = windowTitle;
        }

        public void OnApply(ApplyEventArgs args) {
            Applied?.Invoke(this, args);
        }

        private readonly StartArgsWeb? _startArgs;
        private readonly ArcWindowManagerKey _windowKey;
        private WpBasicData _wpBasicData;
    }
}
