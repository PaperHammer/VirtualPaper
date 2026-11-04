using Grpc.Core;
using System.Collections.Concurrent;
using System.IO;
using VirtualPaper.Grpc.Service.TwoWay;

namespace VirtualPaper.GrpcServers {
    public class TwoWayServer : Grpc_TwoWayService.Grpc_TwoWayServiceBase {
        public override async Task TwoWayStream(
            IAsyncStreamReader<TwoWayMessage> requestStream,
            IServerStreamWriter<TwoWayMessage> responseStream,
            ServerCallContext context) {

            var clientId = Guid.NewGuid().ToString();
            _clients[clientId] = new ClientConnection(responseStream);

            try {
                await foreach (var message in requestStream.ReadAllAsync()) {
                    // 处理接收到的消息
                    HandleMessage(message);
                }
            }
            finally {
                _clients.TryRemove(clientId, out _);
            }
        }

        private static void HandleMessage(TwoWayMessage message) {
            if (message.Type == "UI_CLOSE_RESULT"
                && bool.TryParse(message.Payload, out var canClose)
                && _closeRequests.TryGetValue(message.RequestId, out var request)) {
                request.TrySetResult(canClose);
            }
        }

        #region 静态方法用于发送消息和等待响应

        /// <summary>
        /// 向所有连接的客户端发送消息
        /// </summary>
        public static async Task BroadcastAsync(TwoWayMessage message) {
            foreach (var client in _clients) {
                try {
                    await client.Value.WriteAsync(message);
                }
                catch (Exception ex) when (ex is IOException or InvalidOperationException or RpcException) {
                    _clients.TryRemove(client.Key, out _);
                }
            }
        }

        /// <summary>
        /// 请求 UI 关闭并等待响应
        /// </summary>
        public static async Task<bool> RequestUICloseAsync(TimeSpan? timeout = null) {
            timeout ??= TimeSpan.FromSeconds(30);
            var request = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var requestId = Guid.NewGuid().ToString();
            _closeRequests[requestId] = request;

            var message = new TwoWayMessage {
                Type = "REQUEST_CLOSE",
                RequestId = requestId,
                Payload = ""
            };

            await BroadcastAsync(message);

            using var cts = new CancellationTokenSource(timeout.Value);
            using var registration = cts.Token.Register(() => request.TrySetResult(false));

            try {
                return await request.Task;
            }
            finally {
                _closeRequests.TryRemove(requestId, out _);
            }
        }

        #endregion
		
        private sealed class ClientConnection(IServerStreamWriter<TwoWayMessage> writer) {
            public async Task WriteAsync(TwoWayMessage message) {
                await _writeLock.WaitAsync();
                try {
                    await writer.WriteAsync(message);
                }
                finally {
                    _writeLock.Release();
                }
            }

            private readonly SemaphoreSlim _writeLock = new(1, 1);
        }

		private static readonly ConcurrentDictionary<string, ClientConnection> _clients = new();
        private static readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _closeRequests = new();
    }
}
