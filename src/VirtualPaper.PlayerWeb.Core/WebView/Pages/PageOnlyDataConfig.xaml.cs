using System;
using System.Threading.Tasks;
using VirtualPaper.Common.Events.EffectValue.Base;
using VirtualPaper.Common.Runtime.PlayerWeb;
using VirtualPaper.PlayerWeb.Core.Utils.Interfaces;
using VirtualPaper.UIComponent.Context;
using VirtualPaper.UIComponent.Templates;
using VirtualPaper.UIComponent.Utils.Extensions;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VirtualPaper.PlayerWeb.Core.WebView.Pages {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PageOnlyDataConfig : ArcPage, IEffectService {
        public event EventHandler<EffectValueChangedBase>? EffectChanged;

        public override ArcPageContext Context { get; }
        public override Type PageType => typeof(PageOnlyDataConfig);
        protected override bool IsMultiInstance => true;

        public PageOnlyDataConfig() {
            this.InitializeComponent();
            Context = new ArcPageContext(this, this.MainHost.LoadingControlHost);            
        }

        protected override async Task OnEnterAsync(NavigationPayload? payload) {
            await base.OnEnterAsync(payload);
            Payload = payload;
            if (payload != null) {
                payload.Set(NaviPayLoadKey.IEffectService.ToString(), this);
                payload.Set(NaviPayLoadKey.AvailableConfigTab.ToString(), DataConfigTab.GeneralEffect | DataConfigTab.GeneralInfo);
            }
        }

        #region effect change from ui
        private void RaiseEffectChanged(EffectValueChangedBase e) {
            EffectChanged?.Invoke(this, e);
        }

        public void UpdateEffectValue(EffectValueChanged<double> e) => RaiseEffectChanged(e);

        public void UpdateEffectValue(EffectValueChanged<int> e) => RaiseEffectChanged(e);

        public void UpdateEffectValue(EffectValueChanged<bool> e) => RaiseEffectChanged(e);

        public void UpdateEffectValue(EffectValueChanged<string> e) => RaiseEffectChanged(e);
        #endregion
    }
}
