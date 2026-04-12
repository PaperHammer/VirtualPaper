using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GrpcDotNetNamedPipes;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
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
            catch (Exception ex) when
                        (ex is OperationCanceledException ||
                        (ex is RpcException rpc && rpc.StatusCode == StatusCode.Cancelled)) {
                return;
            }
            catch (Exception e) {
                ArcLog.GetLogger<CommandsClient>().Error(e);
            }
        }

        #region dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    UIRecieveCmd = null;
                    try {
                        _ctsUIRecievedCmd?.Cancel();
                        
                        _uiRecievedCmdTask?.ContinueWith(t => {
                            if (t.Exception != null)
                                ArcLog.GetLogger<CommandsClient>().Error(t.Exception);
                        }, TaskContinuationOptions.NotOnFaulted);
                    }
                    catch (AggregateException ex) { ArcLog.GetLogger<CommandsClient>().Error("Task cancelled during Dispose", ex); }
                    catch (OperationCanceledException) { }

                    _ctsUIRecievedCmd?.Dispose();
                    _recieveCmdLock?.Dispose();
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
    }
}
