using VirtualPaper.Common.Utils.Bridge.Base;
using Windows.Graphics;

namespace VirtualPaper.Common.Utils.Bridge {
    public interface IDraftPanelBridge : IPanelBridge {
        void ChangeProjectPanelState(DraftPanelState nextState, object? param = null);
        PointInt32 GetWindowLocation();
    }
}
