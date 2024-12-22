using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using NLog;
using VirtualPaper.UI.ViewModels.WpSettingsComponents;

namespace VirtualPaper.UI.TrayControl {
    public partial class TrayCommand : IDisposable {
        public TrayCommand(
             ScreenSaverViewModel wpNavSettginsViewModel) {
            _wpNavSettginsViewModel = wpNavSettginsViewModel;
            _cancellationTokenListen = new();

            ListenForClients(_cancellationTokenListen.Token);
        }

        private async void ListenForClients(CancellationToken token) {
            _logger.Info("[PipeServer] Pipe Server is running...");

            try {
                while (!token.IsCancellationRequested) {
                    using (var server = new NamedPipeServerStream("TRAY_CMD", PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous)) {
                        await server.WaitForConnectionAsync(token);
                        using (var reader = new StreamReader(server)) {
                            string cmd = await reader.ReadLineAsync(token);
                            _logger.Info($"[PipeServer] Received command: {cmd}");

                            if (cmd == "UPDATE_SCRSETTINGS") {
                                await _wpNavSettginsViewModel.UpdateScrSettginsAsync();
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) {
                _logger.Warn("[PipeServer] Listening was canceled.");
            }
            catch (Exception ex) {
                _logger.Error(ex, "[PipeServer] An error occurred while waiting for or processing client connections.");
            }
        }

        #region Dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    _cancellationTokenListen?.Cancel();
                }
                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly CancellationTokenSource _cancellationTokenListen;
        private readonly ScreenSaverViewModel _wpNavSettginsViewModel;
    }
}
