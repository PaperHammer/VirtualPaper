using Grpc.Core;
using GrpcDotNetNamedPipes;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.TwoWay;

namespace VirtualPaper.Grpc.Client {
    public class TwoWayClient : ITwoWayClient {
        public event EventHandler<TwoWayMessage>? MessageReceived;

        public TwoWayClient() {
            _client = new Grpc_TwoWayService.Grpc_TwoWayServiceClient(new NamedPipeChannel(".", Constants.CoreField.GrpcPipeServerName));
            _cts = new CancellationTokenSource();
            _ = StartStreamAsync();
        }

        private async Task StartStreamAsync() {
            _call = _client.TwoWayStream(cancellationToken: _cts.Token);

            // 启动接收消息的任务
            _ = Task.Run(async () => {
                try {
                    await foreach (var message in _call.ResponseStream.ReadAllAsync(_cts.Token)) {
                        MessageReceived?.Invoke(this, message);
                    }
                }
                catch (Exception ex) when (ex is OperationCanceledException || ex is RpcException) {
                    // 正常取消或连接关闭
                }
            });
        }

        public async Task SendMessageAsync(TwoWayMessage message) {
            if (_call != null) {
                await _call.RequestStream.WriteAsync(message);
            }
        }

        #region dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    _cts?.Cancel();
                    _cts?.Dispose();
                }
                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private readonly Grpc_TwoWayService.Grpc_TwoWayServiceClient _client;
        private readonly CancellationTokenSource _cts;
        private AsyncDuplexStreamingCall<TwoWayMessage, TwoWayMessage>? _call;
    }
}
