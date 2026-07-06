namespace VirtualPaper.Grpc.Client.Interfaces {
    public interface ICommandsClient : IDisposable {
        event EventHandler<int>? UIRecieveCmd;

        Task ShowUIAsync();
        Task CloseUIAsync();
        Task RequestInstallAsync();
        Task RestartUIAsync();
        Task ShowDebugViewAsync();
        Task ShutDownAsync();
    }
}
