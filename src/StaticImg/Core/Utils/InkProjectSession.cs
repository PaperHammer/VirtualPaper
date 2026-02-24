using System;
using Microsoft.Graphics.Canvas;
using Windows.Graphics.DirectX;
using Workloads.Creation.StaticImg.InkSystem.Utils;
using Workloads.Creation.StaticImg.Models.SerializableData;

namespace Workloads.Creation.StaticImg.Core.Utils {
    public partial class InkProjectSession : IDisposable {
        public event EventHandler? SessionDisposed;

        public string SessionId { get; } = Guid.NewGuid().ToString();
        public DirectXPixelFormat SharedFormat { get; private set; }
        public CanvasAlphaMode SharedAlphaMode { get; private set; }
        public CanvasDevice SharedDevice { get; private set; } = null!;
        public StaticImgUndoRedoUtil UnReUtil { get; private set; } = null!;
        public StaticImgDesignFileUtil DesignFileUtil { get; private set; }

        public InkProjectSession(string idnetify) {
            DesignFileUtil = StaticImgDesignFileUtil.Create(idnetify);
            Initialize();
        }

        private void Initialize() {
            SharedFormat = DirectXPixelFormat.B8G8R8A8UIntNormalized;
            SharedAlphaMode = CanvasAlphaMode.Premultiplied;
            SharedDevice = CanvasDevice.GetSharedDevice();
            UnReUtil = new StaticImgUndoRedoUtil();
        }

        #region dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    SharedDevice?.Dispose();
                    UnReUtil?.Dispose();
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
