using System;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Input;
using VirtualPaper.UIComponent.Services;
using Windows.Foundation;
using Workloads.Creation.StaticImg.Core.Utils;
using Workloads.Creation.StaticImg.Events;
using Workloads.Creation.StaticImg.ViewModels;

namespace Workloads.Creation.StaticImg.Core.Rendering {
    public abstract class RenderBase : IUnifiedInputProcessor<CanvasPointerEventArgs>, IDisposable {
        public event EventHandler<CursorChangedEventArgs>? SystemCursorChangeRequested;
        public event EventHandler<RenderTargetChangedEventArgs>? RenderRequest;
        public event EventHandler? OnceRenderCompleted;
        public event EventHandler<Exception>? FatalErrorOccurred;

        public InkCanvasViewModel ViewModel { get; set; } = null!;
        protected Guid LayerId { get; private set; }
        protected Rect Viewport { get; private set; } = Rect.Empty;
        protected CanvasRenderTarget RenderTarget => ViewModel.Data.SelectedLayer.RenderData.RenderTarget;
        public virtual bool IsCanvasReady {
            get {
                try {
                    if (RenderTarget == null) return false;
                    var testDevice = RenderTarget.Device; // 探测是否被 Dispose
                    return true;
                }
                catch {
                    return false;
                }
            }
        }

        //public bool IsInteracting { get; protected set; }

        //public Point CurrentPosition { get; protected set; }

        public float InteractionThreshold { get; set; } = 2f;

        internal void OnLayerChanged(ArcSize canvasSize) {
            UpdateViewport(canvasSize);
        }

        protected void ReportFatalError(Exception ex) {
            FatalErrorOccurred?.Invoke(this, ex);
        }

        public virtual void HandleEntered(CanvasPointerEventArgs e) {
            LayerId = e.LayerId;
            SystemCursorChangeRequested?.Invoke(this, new(InputSystemCursor.Create(InputSystemCursorShape.Cross)));
        }

        public virtual void HandlePressed(CanvasPointerEventArgs e) { }

        public virtual void HandleMoved(CanvasPointerEventArgs e) { }

        public virtual void HandleReleased(CanvasPointerEventArgs e) { }

        public virtual void HandleExited(CanvasPointerEventArgs e) {
            SystemCursorChangeRequested?.Invoke(this, new CursorChangedEventArgs(null));
        }

        protected void RequestOnceRender() {
            OnceRenderCompleted?.Invoke(this, EventArgs.Empty);
        }

        public void OnCursorChange(InputSystemCursor cursor) {
            SystemCursorChangeRequested?.Invoke(this, new CursorChangedEventArgs(cursor));
        }

        protected virtual void HandleRender(RenderTargetChangedEventArgs e) {
            RenderRequest?.Invoke(this, e);
        }

        public void RequestCursorChange(InputCursor cursor) { }

        private void UpdateViewport(ArcSize canvasSize) {
            Viewport = canvasSize.ToRect();
        }

        public virtual void Dispose() {
            SystemCursorChangeRequested = null;
            RenderRequest = null;
            BrushManager.ClearCache();
            GC.SuppressFinalize(this);
        }

        protected virtual void HandleDeviceLost() {
        }

        protected static bool IsDeviceLost(Exception ex) {
            return ex.HResult == unchecked((int)0x8899000C); // DXGI_ERROR_DEVICE_REMOVED
        }
    }
}
