using System;
using System.Linq;
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
    public record RegionPixelSnapshotCommand : IUndoableCommand {
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

        public async Task ExecuteAsync() {
            ApplyPixels(_currentPixels);
        }

        public async Task UndoAsync() {
            ApplyPixels(_originalPixels);
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
