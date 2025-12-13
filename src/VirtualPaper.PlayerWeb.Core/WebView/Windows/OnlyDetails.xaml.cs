using System;
using System.Text.Json;
using Microsoft.UI.Xaml;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.PlayerWeb;
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
    public sealed partial class OnlyDetails : ArcWindow {
        public override ArcWindowHost ContentHost => this.MainHost;
        public override ArcWindowManagerKey Key => _windowKey;

        public OnlyDetails(string jsonString, DataConfigTab configTab) {
            _startArgs = JsonSerializer.Deserialize<StartArgsWeb>(jsonString);
            _configTab = configTab;
            _windowKey = new ArcWindowManagerKey(ArcWindowKey.PlayerWebCoreOnlyDetails, _startArgs.FilePath);
            this.InitializeComponent();
            InitializeWindow();
        }

        private void NaviContent_Loaded(object sender, RoutedEventArgs e) {
            try {
                var payload = new NavigationPayload() {
                    ["StartArgs"] = _startArgs,
                    ["ConfigTab"] = _configTab,
                };
                NaviContent.Navigate(typeof(MainPageWithoutSidePanel), payload);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<OnlyDetails>().Error(ex);
            }
        }

        private readonly ArcWindowManagerKey _windowKey;
        private readonly StartArgsWeb _startArgs = null!;
        private readonly DataConfigTab _configTab;
    }
}
