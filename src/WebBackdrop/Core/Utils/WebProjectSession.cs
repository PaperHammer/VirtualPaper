using System;
using VirtualPaper.Common.Utils.UndoRedo.Events;

namespace Workloads.Creation.WebBackdrop.Core.Utils {
    public partial class WebProjectSession : IDisposable {
        public event EventHandler? SessionDisposed;
        public event EventHandler<IsSavedChangedEventArgs>? IsSavedChanged;

        public string SessionId { get; } = Guid.NewGuid().ToString();

        private void UnReUtil_IsSavedChanged(object? sender, IsSavedChangedEventArgs e) {
            IsSavedChanged?.Invoke(this, e);
        }

        #region dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    //UnReUtil?.Dispose();
                    SessionDisposed?.Invoke(this, EventArgs.Empty);
                }
                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
