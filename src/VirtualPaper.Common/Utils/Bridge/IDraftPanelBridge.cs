using VirtualPaper.Common.Utils.Bridge.Base;

namespace VirtualPaper.Common.Utils.Bridge {
    public interface IDraftPanelBridge : IPanelBridge, IOjectProvider {
        void ChangeProjectPanelState(DraftPanelState nextState, object? param = null);
    }
}
