using System;
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

        public OnlyDetails(DataConfigTab configTab, IWpBasicData wpBasicData) {
            _configTab = configTab;
            _payload = new FrameworkPayload() {
                [NaviPayloadKey.StartArgs.ToString()] = _startArgs,
                [NaviPayloadKey.AvailableConfigTab.ToString()] = _configTab,
                [NaviPayloadKey.IWpBasicData.ToString()] = wpBasicData,
            };
            var arcKey = configTab == DataConfigTab.GeneralInfo ? ArcWindowKey.PlayerWebCoreOnlyDetails : ArcWindowKey.PlayerWebCoreDetailsEdit;
            _windowKey = new ArcWindowManagerKey(arcKey, wpBasicData.FilePath);

            this.InitializeComponent();
            InitializeWindow();
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
        private readonly FrameworkPayload _payload;
    }
}
