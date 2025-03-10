using VirtualPaper.Common.Utils.Bridge.Base;

namespace VirtualPaper.Common.Utils.Bridge {
    public interface IWpSettingsPanel : IPanelBridge, ILogBridge {
        object GetMainWindow();
        IDialogService GetDialog();
    }
}
