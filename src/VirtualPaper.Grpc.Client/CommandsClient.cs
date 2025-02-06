using Google.Protobuf.WellKnownTypes;
using GrpcDotNetNamedPipes;
using NLog;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.Commands;

namespace VirtualPaper.Grpc.Client {
    public class CommandsClient : ICommandsClient {
        public event EventHandler<int>? UIRecieveCmd;

        public CommandsClient() {
            _client = new Grpc_CommandsService.Grpc_CommandsServiceClient(new NamedPipeChannel(".", Constants.CoreField.GrpcPipeServerName));

            _ctsUIRecievedCmd = new CancellationTokenSource();
            _uiRecievedCmdTask = Task.Run(() => SubscribeUIRecievedCmdTaskStream(_ctsUIRecievedCmd.Token));
        }

        public async Task ShowUI() {
            await _client.ShowUIAsync(new Empty());
        }

        public async Task CloseUI() {
            await _client.CloseUIAsync(new Empty());
        }

        public async Task RestartUI() {
            await _client.RestartUIAsync(new Empty());
        }

        public async Task ShowDebugView() {
            await _client.ShowDebugViewAsync(new Empty());
        }

        public async Task ShutDown() {
            await _client.ShutDownAsync(new Empty());
        }

        public void SaveRectUI() {
            _client.SaveRectUI(new Empty());
        }

        public async Task SaveRectUIAsync() {
            await _client.SaveRectUIAsync(new Empty());
        }

        private async Task SubscribeUIRecievedCmdTaskStream(CancellationToken token) {
            try {
                using var call = _client.SubscribeUIRecievedCmd(new Empty(), cancellationToken: token);
                while (await call.ResponseStream.MoveNext(token)) {
                    await _recieveCmdLock.WaitAsync(token);
                    try {
                        var response = call.ResponseStream.Current;

                        UIRecieveCmd?.Invoke(this, response.IpcMsg);
                    }
                    finally {
                        _recieveCmdLock.Release();
                    }
                }
            }
            catch (Exception e) {
                _logger.Error(e);
            }
        }

        #region dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    _ctsUIRecievedCmd?.Cancel();
                    _uiRecievedCmdTask?.Wait();
                }

                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private readonly Grpc_CommandsService.Grpc_CommandsServiceClient _client;
        private readonly SemaphoreSlim _recieveCmdLock = new(1, 1);
        private readonly CancellationTokenSource _ctsUIRecievedCmd;
        private readonly Task _uiRecievedCmdTask;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    }
}
