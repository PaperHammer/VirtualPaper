using System.IO;

namespace VirtualPaper.Utils.Interfcaes {
    public interface IPipeClient : IDisposable {
        Task ConnectAsync(CancellationToken ct = default);
        void WaitForPipeDrain();
        StreamWriter CreateWriter();
    }

    public interface IPipeClientFactory {
        IPipeClient Create(string serverName, string pipeName);
    }
}
