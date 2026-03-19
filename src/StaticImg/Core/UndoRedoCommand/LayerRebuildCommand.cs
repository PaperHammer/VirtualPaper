using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        public string Description { get; } = "Global Layer Rebuild";

        private readonly InkCanvasData _canvasData;
        private readonly ArcSize _originalSize;
        private readonly ArcSize _newSize;
        private readonly Dictionary<Guid, byte[]> _compressedOriginalPixelsDict;
        private readonly Dictionary<Guid, byte[]> _compressedNewPixelsDict;
        private readonly Action _requestRenderAction;

        public LayerRebuildCommand(
            InkCanvasData canvasData,
            ArcSize originalSize,
            ArcSize newSize,
            Dictionary<Guid, byte[]> compressedOriginalPixelsDict,
            Dictionary<Guid, byte[]> compressedNewPixelsDict,
            Action requestRenderAction) {

            _canvasData = canvasData;
            _originalSize = originalSize;
            _newSize = newSize;
            _compressedOriginalPixelsDict = compressedOriginalPixelsDict;
            _compressedNewPixelsDict = compressedNewPixelsDict;
            _requestRenderAction = requestRenderAction;
        }

        public async Task ExecuteAsync() {
            var uncompressedDict = new ConcurrentDictionary<Guid, byte[]>();
            await Task.Run(() => {
                Parallel.ForEach(_compressedNewPixelsDict, kvp => {
                    uncompressedDict[kvp.Key] = kvp.Value.DecompressPixels();
                });
            });

            foreach (var layer in _canvasData.Layers) {
                var renderData = layer.RenderData;
                if (renderData == null) continue;

                if (uncompressedDict.TryGetValue(layer.Tag, out byte[]? uncompressedPixels)) {
                    renderData.ResizeAndSetPixels(_newSize, uncompressedPixels);
                    renderData.HandleOnceRenderCompleted();
                }
            }

            _canvasData.CanvasSize = _newSize;
            _requestRenderAction?.Invoke();
        }

        public async Task UndoAsync() {
            var uncompressedDict = new ConcurrentDictionary<Guid, byte[]>();
            await Task.Run(() => {
                Parallel.ForEach(_compressedOriginalPixelsDict, kvp => {
                    uncompressedDict[kvp.Key] = kvp.Value.DecompressPixels();
                });
            });

            foreach (var layer in _canvasData.Layers) {
                var renderData = layer.RenderData;
                if (renderData == null) continue;

                if (uncompressedDict.TryGetValue(layer.Tag, out byte[]? uncompressedPixels)) {
                    renderData.ResizeAndSetPixels(_originalSize, uncompressedPixels);
                    renderData.HandleOnceRenderCompleted();
                }
            }

            _canvasData.CanvasSize = _originalSize;
            _requestRenderAction?.Invoke();
        }
    }
}
