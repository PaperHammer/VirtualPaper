using System;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.PlayerWeb.Core.WebView.Pages {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PageOnlyDataConfig : ArcPage {
        public override ArcPageContext ArcContext { get; set; }
        public override Type ArcType => typeof(PageOnlyDataConfig);
        protected override bool IsMultiInstance => true;

        public PageOnlyDataConfig() {
            this.InitializeComponent();
            ArcContext = new ArcPageContext(this, this.MainHost.LoadingControlHost);            
        }

        protected override void OnEnter(FrameworkPayload? payload) {
            base.OnEnter(payload);
            Payload = payload;
            payload?.Set(NaviPayloadKey.AvailableConfigTab.ToString(), DataConfigTab.GeneralEffect | DataConfigTab.GeneralInfo);
        }        
    }
}
