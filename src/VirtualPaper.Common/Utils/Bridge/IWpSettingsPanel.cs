using VirtualPaper.Common.Utils.Bridge.Base;

namespace VirtualPaper.Common.Utils.Bridge {
    public interface IWpSettingsPanel : IPanelBridge, ILogBridge {
        INoifyBridge GetNotify();
        object GetCompositor();
        object GetMainWindow();
        IDialogService GetDialog();
    }
}
