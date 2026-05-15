using VirtualPaper.Grpc.Client.Interfaces;

namespace VirtualPaper.Grpc.Client {
    public class StyleTransferClient : IStyleTransferClient {


        #region Dispose
        private bool _disposed;
        protected virtual void Dispose(bool disposing) {
            if (_disposed) return;

            if (disposing) {
            }

            _disposed = true;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
