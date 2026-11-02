using Grpc.Core;
using GrpcDotNetNamedPipes;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.TwoWay;

namespace VirtualPaper.Grpc.Client {
    public class TwoWayClient : ITwoWayClient, IAsyncDisposable {
        public event EventHandler<TwoWayMessage>? MessageReceived;

        public TwoWayClient() : this(new NamedPipeChannel(".", Constants.CoreField.GrpcPipeServerName)) { }

        internal TwoWayClient(NamedPipeChannel channel) {
            var client = new Grpc_TwoWayService.Grpc_TwoWayServiceClient(channel);
            _call = client.TwoWayStream(cancellationToken: _cts.Token);
            _receiveTask = Task.Run(ReceiveAsync);
        }

        private async Task ReceiveAsync() {
            try {
                await foreach (var message in _call.ResponseStream.ReadAllAsync(_cts.Token).ConfigureAwait(false)) {
                    MessageReceived?.Invoke(this, message);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or RpcException) {
                // Cancellation or a disconnected backend ends the stream.
            }
        }

        public async Task SendMessageAsync(TwoWayMessage message) {
            await _writeLock.WaitAsync().ConfigureAwait(false);
            try {
                ObjectDisposedException.ThrowIf(_isDisposed != 0, this);
                await _call.RequestStream.WriteAsync(message).ConfigureAwait(false);
            }
            finally { _writeLock.Release(); }
        }

        #region dispose
        private int _isDisposed;
        public async ValueTask DisposeAsync() {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0) return;
            MessageReceived = null;
            try {
                // Half-close first, allowing the server to remove this client and send
                // its trailers before the pipe disappears. Cancellation alone races
                // the server's completion write with transport disposal.
                await _writeLock.WaitAsync().ConfigureAwait(false);
                try { await _call.RequestStream.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
                finally { _writeLock.Release(); }
                await _receiveTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is RpcException or OperationCanceledException or TimeoutException) {
                ArcLog.GetLogger<TwoWayClient>().Warn($"Two-way stream could not close gracefully: {ex.Message}");
            }
            finally {
                _cts.Cancel();
                _call.Dispose();
                // Always observe completion, including a handler failure during shutdown.
                try { await _receiveTask.ConfigureAwait(false); }
                finally { _cts.Dispose(); }
            }
            GC.SuppressFinalize(this);
        }

        public void Dispose() {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        #endregion

        private readonly CancellationTokenSource _cts = new();
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly AsyncDuplexStreamingCall<TwoWayMessage, TwoWayMessage> _call;
        private readonly Task _receiveTask;
    }
}
