using VirtualPaper.Common.Utils.Bridge.Base;
using Windows.UI;

namespace VirtualPaper.Common.Utils.Bridge {
    public interface IWpSettingsPanel : IPanelBridge, IOjectProvider, ILogBridge {
        INoifyBridge GetNotify();
        object GetCompositor();
        object GetMainWindow();
        IDialogService GetDialog();
        Color GetColorByKey(string key);
    }
}
