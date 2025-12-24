using System;
using System.Threading.Tasks;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils.Extensions;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.PlayerWeb.Core.WebView.Pages {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PageOnlyDataConfig : ArcPage {
        public override ArcPageContext Context { get; }
        public override Type PageType => typeof(PageOnlyDataConfig);
        protected override bool IsMultiInstance => true;
        public NavigationPayload? Payload { get; private set; }

        public PageOnlyDataConfig() {
            this.InitializeComponent();
            Context = new ArcPageContext(this, this.MainHost.LoadingControlHost);
        }

        protected override async Task OnEnterAsync(NavigationPayload? payload) {
            await base.OnEnterAsync(payload);
            Payload = payload;
        }
    }
}
