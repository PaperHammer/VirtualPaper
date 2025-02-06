using VirtualPaper.Common.Utils.Bridge.Base;

namespace VirtualPaper.Common.Utils.Bridge
{
    public interface IAppSettingsPanel : IPanelBridge, IOjectProvider, ILogBridge {
        INoifyBridge GetNotify();
    }
}
