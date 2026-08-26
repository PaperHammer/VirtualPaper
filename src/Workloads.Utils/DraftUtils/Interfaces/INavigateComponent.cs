using VirtualPaper.UIComponent.Utils;

namespace Workloads.Utils.DraftUtils.Interfaces {
    public interface INavigateComponent {
        void NavigateByState(EditPanelState nextState, params NaviPayloadData[] naviPayloadDatas);
        FrameworkPayload? GetPaylaod();
    }

    public enum EditPanelState {
        GetStart,
        EditConfig,
        WorkSpace,
        ConfigSpace,
        ExportConfig,
    }
}
