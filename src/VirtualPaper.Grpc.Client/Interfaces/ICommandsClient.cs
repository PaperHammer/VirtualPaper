namespace VirtualPaper.Grpc.Client.Interfaces {
    public interface ICommandsClient : IDisposable {
        event EventHandler<int>? UIRecieveCmd;

        Task ShowUIAsync();
        Task CloseUI();
        Task RestartUI();
        Task ShowDebugView();
        Task ShutDown();
    }
}
