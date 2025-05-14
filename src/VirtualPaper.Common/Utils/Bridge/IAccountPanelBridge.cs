using VirtualPaper.Common.Utils.Bridge.Base;

namespace VirtualPaper.Common.Utils.Bridge {
    public interface IAccountPanelBridge : IPanelBridge, ILogBridge {
        IDialogService GetDialog();
        object GetSharedData();
        void ChangePanelState(AccountPanelState nextPanel, object data);
    }
}
