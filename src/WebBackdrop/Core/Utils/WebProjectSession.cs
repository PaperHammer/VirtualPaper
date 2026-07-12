using System;
using VirtualPaper.Common.Utils.UndoRedo.Events;
using Workloads.Creation.WebBackdrop.Models.SerializableData;

namespace Workloads.Creation.WebBackdrop.Core.Utils {
    public partial class WebProjectSession : IDisposable {
        public event EventHandler? SessionDisposed;
        public event EventHandler<IsSavedChangedEventArgs>? IsSavedChanged;

        public string SessionId { get; } = Guid.NewGuid().ToString();
        public WebDesignFileUtil DesignFileUtil { get; private set; }

        public WebProjectSession(string identify) {
            DesignFileUtil = WebDesignFileUtil.Create(identify);
            DesignFileUtil.EnsureProjectStructure();
        }

        internal void RaiseIsSavedChanged(bool isSaved) {
            IsSavedChanged?.Invoke(this, new IsSavedChangedEventArgs(isSaved));
        }

        #region dispose
        private bool _isDisposed;

        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
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
