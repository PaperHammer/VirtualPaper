using VirtualPaper.UIComponent.Utils;

namespace Workloads.Utils.DraftUtils.Interfaces {
    public interface INavigateComponent {
        void NavigateByState(DraftPanelState nextState, params NaviPayloadData[] naviPayloadDatas);
        FrameworkPayload? GetPaylaod();
    }

    public enum DraftPanelState {
        GetStart,
        DraftConfig,
        WorkSpace,
        ConfigSpace,
        ExportConfig,
    }
}
