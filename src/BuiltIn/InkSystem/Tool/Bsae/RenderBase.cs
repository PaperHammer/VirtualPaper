using BuiltIn.Events;
using BuiltIn.InkSystem.Core.Services;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Input;
using VirtualPaper.UIComponent.Services;
using Windows.Foundation;

namespace BuiltIn.InkSystem.Tool.Bsae {
    public abstract class RenderBase : IUnifiedInputProcessor<CanvasPointerEventArgs> {
        public event EventHandler<CursorChangedEventArgs>? SystemCursorChangeRequested;
        public event EventHandler<RenderTargetChangedEventArgs>? RenderRequest;

        protected Rect Viewport { get; private set; } = Rect.Empty;

        protected CanvasRenderTarget? TempRenderTarget { get; private set; }
        protected CanvasRenderTarget? SnapshotRenderTarget { get; private set; }

        private CanvasRenderTarget? _renderTarget;
        protected CanvasRenderTarget? RenderTarget {
            get => _renderTarget;
            set {
                if (_renderTarget == value) return;
                _renderTarget = value;
                UpdateViewport();
                UpdateOtherRenderTarget();
            }
        }

        //public bool IsInteracting { get; protected set; }

        //public Point CurrentPosition { get; protected set; }

        public float InteractionThreshold { get; set; } = 2f;

        public virtual void HandleEntered(CanvasPointerEventArgs e) {
            RenderTarget = e.RenderTarget;
            SystemCursorChangeRequested?.Invoke(this, new(InputSystemCursor.Create(InputSystemCursorShape.Cross)));
        }

        public virtual void HandlePressed(CanvasPointerEventArgs e) { }

        public virtual void HandleMoved(CanvasPointerEventArgs e) { }

        public virtual void HandleReleased(CanvasPointerEventArgs e) { }

        public virtual void HandleExited(CanvasPointerEventArgs e) {
            SystemCursorChangeRequested?.Invoke(this, new CursorChangedEventArgs(null));
        }

        public void OnCursorChange(InputSystemCursor cursor) {
            SystemCursorChangeRequested?.Invoke(this, new CursorChangedEventArgs(cursor));
        }

        protected virtual void HandleRender(RenderTargetChangedEventArgs e) {
            RenderRequest?.Invoke(this, e);
        }

        public void RequestCursorChange(InputCursor cursor) { }

        private void UpdateViewport() {
            if (RenderTarget == null || Viewport == RenderTarget.Bounds) return;

            Viewport = RenderTarget.Bounds;
        }

        private void UpdateOtherRenderTarget() {
            if (RenderTarget == null) {
                return;
            }

            TempRenderTarget?.Dispose();
            TempRenderTarget = new CanvasRenderTarget(
                RenderTarget.Device,
                (float)RenderTarget.Size.Width,
                (float)RenderTarget.Size.Height,
                RenderTarget.Dpi,
                RenderTarget.Format,
                RenderTarget.AlphaMode);

            SnapshotRenderTarget?.Dispose();
            SnapshotRenderTarget = new CanvasRenderTarget(
                RenderTarget.Device,
                (float)RenderTarget.Size.Width,
                (float)RenderTarget.Size.Height,
                RenderTarget.Dpi,
                RenderTarget.Format,
                RenderTarget.AlphaMode);
        }

        public virtual void Dispose() {
            GC.SuppressFinalize(this);
            SystemCursorChangeRequested = null;
            RenderTarget?.Dispose();
            RenderRequest = null;
            BrushManager.ClearCache();
        }

        protected virtual void HandleDeviceLost() {
            RenderTarget?.Dispose();
            RenderTarget = null;
        }

        protected static bool IsDeviceLost(Exception ex) {
            return ex.HResult == unchecked((int)0x8899000C); // DXGI_ERROR_DEVICE_REMOVED
        }
    }
}
