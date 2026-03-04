using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Input;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Utils.UndoRedo;
using VirtualPaper.UIComponent.Services;
using Windows.Foundation;
using Workloads.Creation.StaticImg.Core.Brushes;
using Workloads.Creation.StaticImg.Core.Utils;
using Workloads.Creation.StaticImg.Events;
using Workloads.Creation.StaticImg.Models.Specific;
using Workloads.Creation.StaticImg.ViewModels;

namespace Workloads.Creation.StaticImg.Core.Rendering {
    public abstract class RenderBase : IUnifiedInputProcessor<CanvasPointerEventArgs>, IDisposable {
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

        /// <summary>
        /// Represents an undoable command that captures and restores a snapshot of pixel data within a specified region
        /// of an ink canvas layer.
        /// </summary>
        /// <remarks>This command is typically used to support undo and redo operations for pixel-level
        /// changes on an ink canvas. When executed, it applies the current pixel data to the designated region of the
        /// specified layer. When undone, it restores the original pixel data. Each operation triggers a render request
        /// for the affected region to ensure the canvas display is updated accordingly. The command requires valid
        /// references to the target layer, canvas data, and pixel buffers for both the original and current
        /// states.</remarks>
        protected record RegionPixelSnapshotCommand : IUndoableCommand {
            public string Description { get; }

            public RegionPixelSnapshotCommand(
                Guid layerId,
                InkCanvasData canvasData,
                Rect dirtyRegion,
                byte[] originalPixels,
                byte[] currentPixels,
                bool isCompressed,
                string description,
                Action<Rect> requestRenderAction) {
                _layerId = layerId;
                _canvasData = canvasData;
                _dirtyRegion = dirtyRegion;
                _originalPixels = originalPixels;
                _isCompressed = isCompressed;
                _currentPixels = currentPixels;
                Description = description;
                _requestRenderAction = requestRenderAction;
            }

            public Task ExecuteAsync() {
                ApplyPixels(_currentPixels);
                return Task.CompletedTask;
            }

            public Task UndoAsync() {
                ApplyPixels(_originalPixels);
                return Task.CompletedTask;
            }

            private void ApplyPixels(byte[] pixels) {
                int x = (int)_dirtyRegion.Left;
                int y = (int)_dirtyRegion.Top;
                int w = (int)_dirtyRegion.Width;
                int h = (int)_dirtyRegion.Height;

                var renderData = _canvasData.Layers.FirstOrDefault(l => l.Tag == _layerId)?.RenderData;

                if (_isCompressed) pixels = pixels.DecompressPixels();
                renderData?.RenderTarget?.SetPixelBytes(pixels, x, y, w, h);

                _requestRenderAction?.Invoke(_dirtyRegion);
                renderData?.HandleOnceRenderCompleted();
            }

            private readonly Guid _layerId;
            private readonly InkCanvasData _canvasData;
            private readonly Rect _dirtyRegion;
            private readonly byte[] _originalPixels;
            private readonly bool _isCompressed;
            private readonly byte[] _currentPixels;
            private readonly Action<Rect> _requestRenderAction;
        }
    }
}
