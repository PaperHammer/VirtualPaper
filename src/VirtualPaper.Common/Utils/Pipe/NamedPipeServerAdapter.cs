using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using VirtualPaper.Common.Utils.Pipe.Interfaces;

namespace VirtualPaper.Common.Utils.Pipe {
    public class NamedPipeServerAdapter : IPipeServer {
        public NamedPipeServerAdapter(string pipeName, PipeSecurity? pipeSecurity = null) {
            // 如果没有提供安全设置，创建默认只允许当前用户访问的设置
            if (pipeSecurity == null) {
                pipeSecurity = new PipeSecurity();
                var currentUserId = WindowsIdentity.GetCurrent()?.User;
                if (currentUserId != null) {
                    pipeSecurity.AddAccessRule(new PipeAccessRule(currentUserId, PipeAccessRights.ReadWrite, AccessControlType.Allow));
                }
            }

            _inner = NamedPipeServerStreamAcl.Create(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous,
                0,
                0,
                pipeSecurity);
        }

        public Task WaitForConnectionAsync(CancellationToken cancellationToken) =>
            _inner.WaitForConnectionAsync(cancellationToken);

        public Stream GetStream() => _inner;

        public void WaitForPipeDrain() => _inner.WaitForPipeDrain();

        public void Dispose() => _inner.Dispose();

        private readonly NamedPipeServerStream _inner;
    }

    public class NamedPipeServerFactory : IPipeServerFactory {
        public IPipeServer Create(string pipeName, PipeSecurity? pipeSecurity = null) =>
            new NamedPipeServerAdapter(pipeName, pipeSecurity);
    }
}
