using VirtualPaper.Common;
using VirtualPaper.UIComponent.Utils.Extensions;

namespace VirtualPaper.DraftPanel.Model.Interfaces {
    interface INavigateComponent {
        void NavigateByState(DraftPanelState nextState, params NaviPayloadData[] naviPayloadDatas);
        NavigationPayload? GetPaylaod();
    }
}
