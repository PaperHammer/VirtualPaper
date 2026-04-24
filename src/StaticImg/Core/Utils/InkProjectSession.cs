using System;
using Microsoft.Graphics.Canvas;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.UndoRedo.Events;
using Windows.Graphics.DirectX;
using Workloads.Creation.StaticImg.InkSystem.Utils;
using Workloads.Creation.StaticImg.Models.SerializableData;

namespace Workloads.Creation.StaticImg.Core.Utils {
    public partial class InkProjectSession : IDisposable {
        public event EventHandler? SessionDisposed;
        public event EventHandler<IsSavedChangedEventArgs>? IsSavedChanged;

        public string SessionId { get; } = Guid.NewGuid().ToString();
        public DirectXPixelFormat SharedFormat { get; private set; }
        public CanvasAlphaMode SharedAlphaMode { get; private set; }
        public static CanvasDevice SharedDevice { get; private set; } = null!;
        public StaticImgUndoRedoUtil UnReUtil { get; private set; } = null!;
        public StaticImgDesignFileUtil DesignFileUtil { get; private set; }

        static InkProjectSession() {
            SharedDevice = CanvasDevice.GetSharedDevice();
        }

        public InkProjectSession(string idnetify, FileType fileType) {
            DesignFileUtil = StaticImgDesignFileUtil.Create(idnetify, fileType);
            Initialize();
        }
            
        private void Initialize() {
            SharedFormat = DirectXPixelFormat.B8G8R8A8UIntNormalized;
            SharedAlphaMode = CanvasAlphaMode.Premultiplied;
            UnReUtil = new StaticImgUndoRedoUtil(DesignFileUtil.IsSaveFromInit);
            UnReUtil.IsSavedChanged += UnReUtil_IsSavedChanged;
        }

        private void UnReUtil_IsSavedChanged(object? sender, IsSavedChangedEventArgs e) {
            IsSavedChanged?.Invoke(this, e);
        }

        #region dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
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
