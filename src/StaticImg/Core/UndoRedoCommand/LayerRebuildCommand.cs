using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Utils.UndoRedo;
using Workloads.Creation.StaticImg.Models.Specific;

namespace Workloads.Creation.StaticImg.Core.UndoRedoCommand {
    /// <summary>
    /// Represents an undoable command that rebuilds a specific layer in the ink canvas by updating its size and
    /// pixel data.
    /// </summary>
    /// <remarks>This command enables asynchronous execution and supports undo functionality, allowing
    /// changes to a layer's dimensions and pixel content to be reverted. It is typically used to apply or revert
    /// modifications to a layer's visual state within the canvas, ensuring that rendering updates are properly
    /// requested after each operation.</remarks>
    public record LayerRebuildCommand : IUndoableCommand {
        public string Description { get; } = "Layer Rebuild";

        public LayerRebuildCommand(
            InkCanvasData canvasData,
            ArcSize originalSize,
            ArcSize newSize,
            Dictionary<Guid, byte[]> compressedOriginalPixels,
            Dictionary<Guid, byte[]> compressedNewPixels,
            Action requestRenderAction) {

            _canvasData = canvasData;
            _originalSize = originalSize;
            _newSize = newSize;
            _compressedOriginalPixels = compressedOriginalPixels;
            _compressedNewPixels = compressedNewPixels;
            _requestRenderAction = requestRenderAction;
        }

        public async Task ExecuteAsync() {
            var tasks = _canvasData.Layers
                .Where(ink => ink.RenderData != null)
                .Select(async ink => await Task.Run(() => {
                    if (_compressedNewPixels.TryGetValue(ink.Tag, out byte[]? compressedPixels)) {
                        ink.RenderData.ResizeAndSetPixels(_newSize, compressedPixels.DecompressPixels());
                        ink.RenderData.HandleOnceRenderCompleted();
                    }
                }));
            await Task.WhenAll(tasks);

            _canvasData.CanvasSize = _newSize;
            _requestRenderAction?.Invoke();
        }

        public async Task UndoAsync() {
            var tasks = _canvasData.Layers
                .Where(ink => ink.RenderData != null)
                .Select(async ink => await Task.Run(() => {
                    if (_compressedOriginalPixels.TryGetValue(ink.Tag, out byte[]? compressedPixels)) {
                        ink.RenderData.ResizeAndSetPixels(_originalSize, compressedPixels.DecompressPixels());
                        ink.RenderData.HandleOnceRenderCompleted();
                    }
                }));
            await Task.WhenAll(tasks);

            _canvasData.CanvasSize = _originalSize;
            _requestRenderAction?.Invoke();
        }

        private readonly InkCanvasData _canvasData;
        private readonly ArcSize _originalSize;
        private readonly ArcSize _newSize;
        private readonly Dictionary<Guid, byte[]> _compressedOriginalPixels;
        private readonly Dictionary<Guid, byte[]> _compressedNewPixels;
        private readonly Action _requestRenderAction;
    }
}
