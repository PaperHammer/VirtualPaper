using VirtualPaper.Common.Utils.Bridge.Base;

namespace VirtualPaper.Common.Utils.Bridge {
    public interface IDraftPanelBridge : IPanelBridge {
        void ChangePanelState(DraftPanelState nextState, object? param = null);
    }
}
