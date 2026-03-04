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
using Workloads.Creation.StaticImg.Models;
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

        /// <summary>
        /// Represents an undoable command that rebuilds a specific layer in the ink canvas by updating its size and
        /// pixel data.
        /// </summary>
        /// <remarks>This command enables asynchronous execution and supports undo functionality, allowing
        /// changes to a layer's dimensions and pixel content to be reverted. It is typically used to apply or revert
        /// modifications to a layer's visual state within the canvas, ensuring that rendering updates are properly
        /// requested after each operation.</remarks>
        protected record LayerRebuildCommand : IUndoableCommand {
            public string Description { get; } = "Layer Rebuild";

            public LayerRebuildCommand(
                InkCanvasData canvasData,
                Guid layerId,
                ArcSize originalSize,
                ArcSize newSize,
                byte[] compressedOriginalPixels,
                byte[] compressedNewPixels,
                Action requestRenderAction) {
                _canvasData = canvasData;
                _layerId = layerId;
                _originalSize = originalSize;
                _newSize = newSize;
                _compressedOriginalPixels = compressedOriginalPixels;
                _compressedNewPixels = compressedNewPixels;
                _requestRenderAction = requestRenderAction;
            }

            public Task ExecuteAsync() {
                var renderData = _canvasData.Layers.FirstOrDefault(l => l.Tag == _layerId)?.RenderData;
                if (renderData != null) {
                    byte[] uncompressedPixels = _compressedNewPixels.DecompressPixels();
                    renderData.ResizeAndSetPixels(_newSize, uncompressedPixels);
                    _canvasData.CanvasSize = _newSize;
                }
                _requestRenderAction?.Invoke();
                renderData?.HandleOnceRenderCompleted();

                return Task.CompletedTask;
            }

            public Task UndoAsync() {
                var renderData = _canvasData.Layers.FirstOrDefault(l => l.Tag == _layerId)?.RenderData;
                if (renderData != null) {
                    byte[] uncompressedPixels = _compressedOriginalPixels.DecompressPixels();
                    renderData.ResizeAndSetPixels(_originalSize, uncompressedPixels);
                    _canvasData.CanvasSize = _originalSize;
                }
                _requestRenderAction?.Invoke();
                renderData?.HandleOnceRenderCompleted();

                return Task.CompletedTask;
            }

            private readonly InkCanvasData _canvasData;
            private readonly Guid _layerId;
            private readonly ArcSize _originalSize;
            private readonly ArcSize _newSize;
            private readonly byte[] _compressedOriginalPixels;
            private readonly byte[] _compressedNewPixels;
            private readonly Action _requestRenderAction;
        }

        /// <summary>
        /// Represents an undoable command that moves a selected region of pixels from one location to another within an
        /// ink canvas layer.
        /// </summary>
        /// <remarks>Use this command to support undo and redo operations when moving selections on an ink
        /// canvas. The command stores the necessary pixel data and coordinates to perform the move and to restore the
        /// previous state if undone. The move operation is executed asynchronously and triggers a render update for the
        /// affected regions.</remarks>
        public record SelectionMoveCommand : IUndoableCommand {            
            public string Description { get; }

            public SelectionMoveCommand(
                Guid layerId,
                InkCanvasData canvasData,
                Rect originalRect,
                Rect newRect,
                byte[] compressedSelectionPixels,
                byte[] compressedTargetOriginalPixels,
                string description,
                Action<Rect> requestRenderAction
            ) {
                _layerId = layerId;
                _canvasData = canvasData;

                _ox = (int)originalRect.X;
                _oy = (int)originalRect.Y;
                _nx = (int)newRect.X;
                _ny = (int)newRect.Y;
                _w = (int)originalRect.Width;
                _h = (int)originalRect.Height;

                _compressedSelectionPixels = compressedSelectionPixels;
                _compressedTargetOriginalPixels = compressedTargetOriginalPixels;
                Description = description;
                _requestRenderAction = requestRenderAction;
            }

            public async Task ExecuteAsync() {
                var renderData = GetRenderData();
                if (renderData?.RenderTarget == null) return;

                byte[] selPixels = _compressedSelectionPixels.DecompressPixels();

                // 将原区域填为透明
                byte[] transparentPixels = new byte[selPixels.Length];
                renderData.RenderTarget.SetPixelBytes(transparentPixels, _ox, _oy, _w, _h);
                // 将内容盖到新区域
                renderData.RenderTarget.SetPixelBytes(selPixels, _nx, _ny, _w, _h);

                renderData.HandleOnceRenderCompleted();
                _requestRenderAction(new Rect(_ox, _oy, _w, _h).UnionRect(new Rect(_nx, _ny, _w, _h)));
            }

            public async Task UndoAsync() {
                var renderData = GetRenderData();
                if (renderData?.RenderTarget == null) return;

                byte[] selPixels = _compressedSelectionPixels.DecompressPixels();
                byte[] targetOriginal = _compressedTargetOriginalPixels.DecompressPixels();

                // 先将目标区域恢复
                renderData.RenderTarget.SetPixelBytes(targetOriginal, _nx, _ny, _w, _h);
                // 将移动走的内容放回原处
                renderData.RenderTarget.SetPixelBytes(selPixels, _ox, _oy, _w, _h);

                renderData.HandleOnceRenderCompleted();
                _requestRenderAction(new Rect(_ox, _oy, _w, _h).UnionRect(new Rect(_nx, _ny, _w, _h)));
            }

            private InkRenderData? GetRenderData() {
                var layer = _canvasData.Layers.FirstOrDefault(l => l.Tag == _layerId);
                return layer?.RenderData;
            }

            private readonly Guid _layerId;
            private readonly InkCanvasData _canvasData;
            private readonly Action<Rect> _requestRenderAction;
            private readonly int _ox, _oy, _nx, _ny, _w, _h; // 坐标尺寸数据
            private readonly byte[] _compressedSelectionPixels; // 被移动的图像内容
            private readonly byte[] _compressedTargetOriginalPixels; // 目标区域被覆盖前的底图
        }
    }
}
