using VirtualPaper.Common;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.DraftPanel.Model.Interfaces {
    interface INavigateComponent {
        void NavigateByState(DraftPanelState nextState, params NaviPayloadData[] naviPayloadDatas);
        FrameworkPayload? GetPaylaod();
    }
}
