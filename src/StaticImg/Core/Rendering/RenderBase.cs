using System;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Input;
using VirtualPaper.UIComponent.Services;
using Windows.Foundation;
using Workloads.Creation.StaticImg.Core.Brushes;
using Workloads.Creation.StaticImg.Core.Utils;
using Workloads.Creation.StaticImg.Events;
using Workloads.Creation.StaticImg.ViewModels;

namespace Workloads.Creation.StaticImg.Core.Rendering {
    public abstract class RenderBase : IUnifiedInputProcessor<CanvasPointerEventArgs> {
        public event EventHandler<CursorChangedEventArgs>? SystemCursorChangeRequested;
        public event EventHandler<RenderTargetChangedEventArgs>? RenderRequest;
        public event EventHandler? OnceRenderCompleted;

        public InkCanvasViewModel ViewModel { get; set; } = null!;
        protected Guid LayerId { get; private set; }
        protected Rect Viewport { get; private set; } = Rect.Empty;
        protected StrokeBase CurrentStroke { get; set; } = null!;
        protected CanvasRenderTarget TempRenderTarget { get; private set; } = null!;
        protected CanvasRenderTarget SnapshotRenderTarget { get; private set; } = null!;

        private CanvasRenderTarget _renderTarget = null!;
        protected CanvasRenderTarget RenderTarget {
            get => _renderTarget;
            set {
                if (_renderTarget == value) return;
                _renderTarget = value;
                UpdateViewport();
                UpdateOtherRenderTarget();
            }
        }

        public bool IsCanvasReady => RenderTarget != null &&
            SnapshotRenderTarget != null &&
            TempRenderTarget != null;

        protected bool IsRenderReady =>
            IsCanvasReady &&
            CurrentStroke != null;

        //public bool IsInteracting { get; protected set; }

        //public Point CurrentPosition { get; protected set; }

        public float InteractionThreshold { get; set; } = 2f;

        public virtual void HandleEntered(CanvasPointerEventArgs e) {
            LayerId = e.LayerId;
            RenderTarget = e.RenderTarget;
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
