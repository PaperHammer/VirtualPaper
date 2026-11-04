namespace VirtualPaper.Common.Utils.Pipe.Interfaces {
    public interface IPipeClient : IDisposable {
        Task ConnectAsync(CancellationToken ct = default);
        void WaitForPipeDrain();
        StreamWriter CreateWriter();
        StreamReader CreateReader();
    }

    public interface IPipeClientFactory {
        IPipeClient Create(string serverName, string pipeName);
    }
}
