using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using VirtualPaper.Utils.Interfcaes;

namespace VirtualPaper.Utils.Services {
    public class NamedPipeClientAdapter : IPipeClient {
        public NamedPipeClientAdapter(string serverName, string pipeName) {
            _inner = new NamedPipeClientStream(
                serverName, pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous,
                TokenImpersonationLevel.None);
        }

        public Task ConnectAsync(CancellationToken ct = default) =>
            _inner.ConnectAsync(ct);

        public void WaitForPipeDrain() => _inner.WaitForPipeDrain();

        public StreamWriter CreateWriter() =>
            new StreamWriter(_inner) { AutoFlush = true };

        public void Dispose() => _inner.Dispose();

        private readonly NamedPipeClientStream _inner;
    }

    public class NamedPipeClientFactory : IPipeClientFactory {
        public IPipeClient Create(string serverName, string pipeName) =>
            new NamedPipeClientAdapter(serverName, pipeName);
    }
}
