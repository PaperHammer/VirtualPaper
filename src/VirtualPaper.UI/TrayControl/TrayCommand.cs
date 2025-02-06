using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace VirtualPaper.UI.TrayControl {
    //public partial class TrayCommand : IDisposable {
    //    public TrayCommand(
    //         ScreenSaverViewModel wpNavSettginsViewModel) {
    //        _wpNavSettginsViewModel = wpNavSettginsViewModel;
    //        _cancellationTokenListen = new();

    //        ListenForClients(_cancellationTokenListen.Token);
    //    }

    //    private async void ListenForClients(CancellationToken token) {
    //        App.Log.Info("[PipeServer] Pipe Server is running...");

    //        try {
    //            while (!token.IsCancellationRequested) {
    //                using (var server = new NamedPipeServerStream("TRAY_CMD", PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous)) {
    //                    await server.WaitForConnectionAsync(token);
    //                    using (var reader = new StreamReader(server)) {
    //                        string cmd = await reader.ReadLineAsync(token);
    //                        App.Log.Info($"[PipeServer] Received command: {cmd}");

    //                        if (cmd == "UPDATE_SCRSETTINGS") {
    //                            await _wpNavSettginsViewModel.UpdateScrSettginsAsync();
    //                        }
    //                    }
    //                }
    //            }
    //        }
    //        catch (OperationCanceledException) when (token.IsCancellationRequested) {
    //            App.Log.Warn("[PipeServer] Listening was canceled.");
    //        }
    //        catch (Exception ex) {
    //            App.Log.Error(ex, "[PipeServer] An Error occurred while waiting for or processing client connections.");
    //        }
    //    }

    //    #region Dispose
    //    private bool _isDisposed;
    //    protected virtual void Dispose(bool disposing) {
    //        if (!_isDisposed) {
    //            if (disposing) {
    //                _cancellationTokenListen?.Cancel();
    //            }
    //            _isDisposed = true;
    //        }
    //    }

    //    public void Dispose() {
    //        Dispose(disposing: true);
    //        GC.SuppressFinalize(this);
    //    }
    //    #endregion

    //    private readonly CancellationTokenSource _cancellationTokenListen;
    //    private readonly ScreenSaverViewModel _wpNavSettginsViewModel;
    //}
}
