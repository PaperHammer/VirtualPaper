using System.IO.Pipes;

namespace VirtualPaper.Common.Utils.Pipe.Interfaces {
    public interface IPipeServer : IDisposable {
        Task WaitForConnectionAsync(CancellationToken cancellationToken);
        Stream GetStream();
        void WaitForPipeDrain();
    }

    public interface IPipeServerFactory {
        IPipeServer Create(string pipeName, PipeSecurity? pipeSecurity = null);
    }
}
