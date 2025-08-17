using BuiltIn.Events;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Input;
using VirtualPaper.UIComponent.Services;
using Windows.Foundation;

namespace BuiltIn.Tool.Bsae {
    public abstract class CanvasRenderTargetInteract : IUnifiedInputProcessor<CanvasPointerEventArgs> {
        public event EventHandler<CursorChangedEventArgs>? SystemCursorChangeRequested;
        public event EventHandler<RenderTargetChangedEventArgs>? RenderRequest;

        protected Rect Viewport { get; private set; } = Rect.Empty;

        private CanvasRenderTarget? _renderTarget;
        protected CanvasRenderTarget? RenderTarget {
            get => _renderTarget;
            set {
                if (_renderTarget == value) return;
                _renderTarget = value;
                UpdateRelatedVariables();
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

        private void UpdateRelatedVariables() {
            if (RenderTarget == null) return;

            Viewport = new Rect(
                0, 0,
                RenderTarget.SizeInPixels.Width,
                RenderTarget.SizeInPixels.Height);
        }

        public virtual void Dispose() {
            SystemCursorChangeRequested = null;
            RenderRequest = null;
            GC.SuppressFinalize(this);
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
