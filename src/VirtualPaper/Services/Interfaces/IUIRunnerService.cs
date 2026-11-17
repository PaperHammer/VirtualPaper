using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.Files;

namespace VirtualPaper.Services.Interfaces {
    /// <summary>
    /// 主 UI 设置
    /// </summary>
    public interface IUIRunnerService : IDisposable {
        event EventHandler<MessageType>? UISendCmd;

        bool IsVisibleUI { get; }
        
        Task ShowUIAsync();
        Task OpenProjectsAsync(IEnumerable<string> paths);
        ProjectOpenRequest? GetPendingProjectOpen();
        void CompleteProjectOpen(string requestId);
        void CloseUI();
        void NotifyUIClosing(int processId);
        void SendCloseCmd();
        Task<bool> RequestUICloseAsync();
        Task RestartUIAsync();
        nint GetUIHwnd();
    }
}
