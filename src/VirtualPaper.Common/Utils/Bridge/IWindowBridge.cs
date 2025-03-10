namespace VirtualPaper.Common.Utils.Bridge.Base {
    public interface IWindowBridge : IBaseBridge, ILogBridge {
        INoifyBridge GetNotify();
        // VirtualPaper.Common 非 winui 项目，无法使用 Microsoft.UI.xxx 的命名空间
        object GetMainWindow();
        double GetScale();
        IDialogService GetDialog();
    }
}
