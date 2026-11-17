using VirtualPaper.Common.Utils.Files;

namespace VirtualPaper.Grpc.Client.Interfaces {
    public interface ICommandsClient : IDisposable {
        event EventHandler<int>? UIRecieveCmd;

        Task ShowUIAsync();
        Task<ProjectOpenRequest?> GetPendingProjectOpenAsync();
        Task CompleteProjectOpenAsync(string requestId);
        Task CloseUIAsync();
        Task NotifyUIClosingAsync();
        Task RequestInstallAsync();
        Task RestartUIAsync();
        Task ShowDebugViewAsync();
        Task ShutDownAsync();
    }
}
