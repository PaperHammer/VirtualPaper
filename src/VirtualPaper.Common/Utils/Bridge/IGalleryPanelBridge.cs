using VirtualPaper.Common.Utils.Bridge.Base;

namespace VirtualPaper.Common.Utils.Bridge {
    public interface IGalleryPanelBridge : IPanelBridge, ILogBridge {
        IDialogService GetDialog();
        object GetSharedData();
        void ChangePanelState(GalleryPanelState nextPanel, object data);
    }
}
