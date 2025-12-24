using System;
using System.Text.Json;
using Microsoft.UI.Xaml;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.Models.Cores.Interfaces;
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

        private OnlyDetails(DataConfigTab configTab) {
            _configTab = configTab;
            _payload = new NavigationPayload() {
                [NaviPayLoadKey.StartArgs.ToString()] = _startArgs,
                [NaviPayLoadKey.AvailableConfigTab.ToString()] = _configTab,
            };

            this.InitializeComponent();
            InitializeWindow();
        }

        public OnlyDetails(string jsonString, DataConfigTab configTab) : this(configTab) {
            _startArgs = JsonSerializer.Deserialize<StartArgsWeb>(jsonString);
            _windowKey = new ArcWindowManagerKey(ArcWindowKey.PlayerWebCoreOnlyDetails, _startArgs.FilePath);
            _payload[NaviPayLoadKey.StartArgs.ToString()] = _startArgs;
        }

        public OnlyDetails(DataConfigTab configTab, IWpBasicData wpBasicData) : this(configTab) {
            _windowKey = new ArcWindowManagerKey(ArcWindowKey.PlayerWebCoreOnlyDetails, wpBasicData.FilePath);
            _payload[NaviPayLoadKey.IWpBasicData.ToString()] = wpBasicData;
        }

        private void NaviContent_Loaded(object sender, RoutedEventArgs e) {
            try {
                NaviContent.Navigate(typeof(PageOnlyDataConfig), _payload);
            }
            catch (Exception ex) {
                ArcLog.GetLogger<OnlyDetails>().Error(ex);
            }
        }

        private readonly ArcWindowManagerKey _windowKey;
        private readonly StartArgsWeb _startArgs = null!;
        private readonly DataConfigTab _configTab;
        private readonly NavigationPayload _payload;
    }
}
