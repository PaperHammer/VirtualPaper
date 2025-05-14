namespace VirtualPaper.Grpc.Client.Interfaces {
    public interface ICommandsClient : IDisposable {
        event EventHandler<int>? UIRecieveCmd;

        Task ShowUI();
        Task CloseUI();
        Task RestartUI();
        Task ShowDebugView();
        Task ShutDown();
        void SaveRectUI();
        Task SaveRectUIAsync();
    }
}
