using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Utils.UndoRedo;
using Windows.Foundation;
using Workloads.Creation.StaticImg.Models.Specific;

namespace Workloads.Creation.StaticImg.Core.UndoRedoCommand {
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
    public partial record RegionPixelSnapshotCommand : IUndoableCommand, IMemoryAwareUndoableCommand, IDiskSpillableUndoCommand, IDisposable {
        public string Description { get; }
        public long EstimatedMemoryBytes =>
            _originalPixels.ResidentMemoryBytes +
            _currentPixels.ResidentMemoryBytes +
            CommandOverheadBytes;
        long IDiskSpillableUndoCommand.DiskStorageBytes =>
            _originalPixels.DiskStorageBytes + _currentPixels.DiskStorageBytes;

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
            _originalPixels = new UndoDiskPayload(originalPixels);
            _isCompressed = isCompressed;
            _currentPixels = new UndoDiskPayload(currentPixels);
            Description = description;
            _requestRenderAction = requestRenderAction;
        }

        public async Task ExecuteAsync() {
            await ApplyPixelsAsync(_currentPixels);
        }

        public async Task UndoAsync() {
            await ApplyPixelsAsync(_originalPixels);
        }

        async Task<bool> IDiskSpillableUndoCommand.TrySpillToDiskAsync(
            UndoDiskStore store,
            CancellationToken cancellationToken) {
            bool originalSpilled = await _originalPixels.TrySpillToDiskAsync(store, cancellationToken);
            bool currentSpilled = await _currentPixels.TrySpillToDiskAsync(store, cancellationToken);
            return originalSpilled || currentSpilled;
        }

        private async Task ApplyPixelsAsync(UndoDiskPayload payload) {
            int x = (int)_dirtyRegion.Left;
            int y = (int)_dirtyRegion.Top;
            int w = (int)_dirtyRegion.Width;
            int h = (int)_dirtyRegion.Height;

            byte[] pixels = await payload.ReadAsync();
            if (_isCompressed)
                pixels = await Task.Run(pixels.DecompressPixels);

            // The disk read can yield; refresh the layer reference before touching
            // its WinRT render target in case the layer was rebuilt meanwhile.
            var renderData = _canvasData.Layers.FirstOrDefault(l => l.Tag == _layerId)?.RenderData;
            renderData?.RenderTarget?.SetPixelBytes(pixels, x, y, w, h);

            _requestRenderAction?.Invoke(_dirtyRegion);
            renderData?.HandleOnceRenderCompleted();
        }

        private readonly Guid _layerId;
        private readonly InkCanvasData _canvasData;
        private readonly Rect _dirtyRegion;
        private readonly UndoDiskPayload _originalPixels;
        private readonly bool _isCompressed;
        private readonly UndoDiskPayload _currentPixels;
        private readonly Action<Rect> _requestRenderAction;
        private const long CommandOverheadBytes = 256;

        #region dispose
        private bool _isDisposed;

        protected virtual void Dispose(bool disposing) {
            if (_isDisposed) return;
            if (disposing) {
                _originalPixels.Dispose();
                _currentPixels.Dispose();
            }
            _isDisposed = true;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
