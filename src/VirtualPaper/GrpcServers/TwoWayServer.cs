using Grpc.Core;
using System.Collections.Concurrent;
using VirtualPaper.Grpc.Service.TwoWay;

namespace VirtualPaper.GrpcServers {
    public class TwoWayServer : Grpc_TwoWayService.Grpc_TwoWayServiceBase {
        public override async Task TwoWayStream(
            IAsyncStreamReader<TwoWayMessage> requestStream,
            IServerStreamWriter<TwoWayMessage> responseStream,
            ServerCallContext context) {

            var clientId = Guid.NewGuid().ToString();
            _clients[clientId] = responseStream;

            try {
                await foreach (var message in requestStream.ReadAllAsync()) {
                    // 处理接收到的消息
                    await HandleMessageAsync(clientId, message, responseStream);
                }
            }
            finally {
                _clients.TryRemove(clientId, out _);
            }
        }

        private async Task HandleMessageAsync(string clientId, TwoWayMessage message, IServerStreamWriter<TwoWayMessage> responseStream) {
            switch (message.Type) {
                case "UI_CLOSE_RESULT":
                    if (_closeRequestTcs != null && message.RequestId == _closeRequestId) {
                        var canClose = bool.Parse(message.Payload);
                        _closeRequestTcs.TrySetResult(canClose);
                    }
                    break;
            }

            await Task.CompletedTask;
        }

        #region 静态方法用于发送消息和等待响应

        /// <summary>
        /// 向所有连接的客户端发送消息
        /// </summary>
        public static async Task BroadcastAsync(TwoWayMessage message) {
            foreach (var client in _clients.Values) {
                await client.WriteAsync(message);
            }
        }

        /// <summary>
        /// 请求 UI 关闭并等待响应
        /// </summary>
        public static async Task<bool> RequestUICloseAsync(TimeSpan? timeout = null) {
            timeout ??= TimeSpan.FromSeconds(30);
            _closeRequestTcs = new TaskCompletionSource<bool>();
            _closeRequestId = Guid.NewGuid().ToString();

            var message = new TwoWayMessage {
                Type = "REQUEST_CLOSE",
                RequestId = _closeRequestId,
                Payload = ""
            };

            await BroadcastAsync(message);

            using var cts = new CancellationTokenSource(timeout.Value);
            using var registration = cts.Token.Register(() => _closeRequestTcs.TrySetResult(false));

            try {
                return await _closeRequestTcs.Task;
            }
            finally {
                _closeRequestTcs = null;
                _closeRequestId = null;
            }
        }

        #endregion
		
        private static readonly ConcurrentDictionary<string, IServerStreamWriter<TwoWayMessage>> _clients = new();
        private static TaskCompletionSource<bool>? _closeRequestTcs;
        private static string? _closeRequestId;
    }
}
