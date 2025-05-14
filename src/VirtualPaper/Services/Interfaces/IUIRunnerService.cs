using VirtualPaper.Common.Utils.IPC;

namespace VirtualPaper.Services.Interfaces {
    /// <summary>
    /// 主 UI 设置
    /// </summary>
    public interface IUIRunnerService : IDisposable {
        event EventHandler<MessageType>? UISendCmd;

        bool IsVisibleUI { get; }
        
        void ShowUI();
        void CloseUI();
        void RestartUI();
        void SaveRectUI();
        nint GetUIHwnd();
    }
}
