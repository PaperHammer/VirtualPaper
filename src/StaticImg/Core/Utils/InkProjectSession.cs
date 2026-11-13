using System;
using System.Threading;
using Microsoft.Graphics.Canvas;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.UndoRedo.Events;
using Windows.Graphics.DirectX;
using Workloads.Creation.StaticImg.InkSystem.Utils;
using Workloads.Creation.StaticImg.Models.SerializableData;

namespace Workloads.Creation.StaticImg.Core.Utils {
    public partial class InkProjectSession : IDisposable {
        public event EventHandler? SessionDisposed;
        public event EventHandler? DrawingDeviceLost;
        public event EventHandler<IsSavedChangedEventArgs>? IsSavedChanged;

        public string SessionId { get; } = Guid.NewGuid().ToString();
        public DirectXPixelFormat SharedFormat { get; private set; }
        public CanvasAlphaMode SharedAlphaMode { get; private set; }
        public CanvasDevice Device { get; }
        public bool IsDrawingUnavailable => Volatile.Read(ref _drawingUnavailable) != 0;
        internal BrushManager Brushes { get; }
        public StaticImgUndoRedoUtil UnReUtil { get; private set; } = null!;
        public StaticImgDesignFileUtil DesignFileUtil { get; private set; }

        public InkProjectSession(string idnetify, FileType fileType)
            : this(idnetify, fileType, DrawingDeviceManager.Current.GetDevice()) { }

        internal InkProjectSession(string idnetify, FileType fileType, CanvasDevice device) {
            Device = device;
            Brushes = new BrushManager(device);
            DesignFileUtil = StaticImgDesignFileUtil.Create(idnetify, fileType);
            Initialize();
            Device.DeviceLost += OnDeviceLost;
        }

        public void EnsureDrawingAvailable() {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (IsDrawingUnavailable)
                throw new InvalidOperationException("The drawing device was lost. Reopen the saved project to continue.");
        }

        internal bool IsDeviceLostException(Exception error) => Device.IsDeviceLost(error.HResult);

        internal void ReportDeviceLost() => Device.RaiseDeviceLost();

        internal void OnDeviceLost(CanvasDevice sender, object args) {
            if (!_isDisposed && Interlocked.Exchange(ref _drawingUnavailable, 1) == 0)
                DrawingDeviceLost?.Invoke(this, EventArgs.Empty);
        }
            
        private void Initialize() {
            SharedFormat = DirectXPixelFormat.B8G8R8A8UIntNormalized;
            SharedAlphaMode = CanvasAlphaMode.Premultiplied;
            UnReUtil = new StaticImgUndoRedoUtil(DesignFileUtil.IsSaveFromInit, sessionId: SessionId);
            UnReUtil.IsSavedChanged += UnReUtil_IsSavedChanged;
        }

        private void UnReUtil_IsSavedChanged(object? sender, IsSavedChangedEventArgs e) {
            IsSavedChanged?.Invoke(this, e);
        }

        #region dispose
        private bool _isDisposed;
        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                _isDisposed = true;
                if (disposing) {
                    Device.DeviceLost -= OnDeviceLost;
                    UnReUtil?.Dispose();
                    Brushes.Dispose();
                    SessionDisposed?.Invoke(this, EventArgs.Empty);
                    DrawingDeviceLost = null;
                }
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        private int _drawingUnavailable;
    }
}
