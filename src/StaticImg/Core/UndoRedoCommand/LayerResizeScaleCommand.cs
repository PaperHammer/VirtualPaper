using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Utils.UndoRedo;
using Workloads.Creation.StaticImg.Models.Specific;

namespace Workloads.Creation.StaticImg.Core.UndoRedoCommand {
    public class LayerResizeScaleCommand : IUndoableCommand {
        public string Description { get; } = "Layer Reisze or Scale";

        private readonly InkCanvasData _canvasData;
        private readonly ArcSize _originalSize;
        private readonly ArcSize _newSize;
        private Dictionary<Guid, byte[]>? _compressedOriginalPixels;
        private Dictionary<Guid, byte[]>? _compressedNewPixels;
        private readonly Action<ArcSize> _requestRenderAction;
        private bool _isFirstExecution = true;

        public LayerResizeScaleCommand(
            InkCanvasData canvasData,
            ArcSize originalSize,
            ArcSize newSize,
            Action<ArcSize> requestRenderAction) {
            _canvasData = canvasData;
            _originalSize = originalSize;
            _newSize = newSize;
            _requestRenderAction = requestRenderAction;
        }

        public async Task ExecuteAsync() {
            if (_isFirstExecution) {
                _isFirstExecution = false;

                var originalPixelsDict = new ConcurrentDictionary<Guid, byte[]>();
                Parallel.ForEach(_canvasData.Layers, item => {
                    if (item.RenderData?.RenderTarget != null) {
                        byte[] compressedOld = item.RenderData.RenderTarget.GetPixelBytes().CompressPixels();
                        originalPixelsDict.TryAdd(item.Tag, compressedOld);
                    }
                });
                _compressedOriginalPixels = originalPixelsDict.ToDictionary();

                var tasks = _canvasData.Layers
                    .Where(ink => ink.RenderData != null)
                    .Select(async (ink) => {
                        await ink.RenderData.ResizeRenderTargetAsync(_newSize);
                        ink.RenderData.HandleOnceRenderCompleted();
                    });
                await Task.WhenAll(tasks);

                var newPixelsDict = new ConcurrentDictionary<Guid, byte[]>();
                Parallel.ForEach(_canvasData.Layers, item => {
                    if (item.RenderData?.RenderTarget != null) {
                        byte[] compressedOld = item.RenderData.RenderTarget.GetPixelBytes().CompressPixels();
                        originalPixelsDict.TryAdd(item.Tag, compressedOld);
                    }
                });
                _compressedNewPixels = newPixelsDict.ToDictionary();

                tasks = _canvasData.Layers
                    .Where(ink => ink.RenderData != null)
                    .Select(async (ink) => {
                        await ink.RenderData.ResizeRenderTargetAsync(_newSize);
                        ink.RenderData.HandleOnceRenderCompleted();
                    });
                await Task.WhenAll(tasks);

                var oldPixels = new List<(Guid Tag, byte[] RawPixels)>();
                foreach (var layer in _canvasData.Layers) {
                    if (layer.RenderData?.RenderTarget != null) {
                        oldPixels.Add((layer.Tag, layer.RenderData.RenderTarget.GetPixelBytes()));
                    }
                }

                _canvasData.CanvasSize = _newSize;
                _requestRenderAction?.Invoke(_newSize);
            }
            else {
                _canvasData.CanvasSize = _newSize;
                foreach (var layer in _canvasData.Layers) {
                    var renderData = layer.RenderData;
                    if (renderData == null) continue;

                    if (_compressedNewPixels != null && _compressedNewPixels.TryGetValue(layer.Tag, out byte[]? compressedNew)) {
                        byte[] decompressedPixels = compressedNew.DecompressPixels();
                        renderData.ResizeAndSetPixels(_newSize, decompressedPixels);
                        renderData.HandleOnceRenderCompleted();
                    }
                }
                _requestRenderAction?.Invoke(_newSize);
            }
        }

        public async Task UndoAsync() {            
            foreach (var layer in _canvasData.Layers) {
                var renderData = layer.RenderData;
                if (renderData == null) continue;

                if (_compressedOriginalPixels != null && _compressedOriginalPixels.TryGetValue(layer.Tag, out byte[]? compressedOld)) {
                    byte[] decompressedPixels = compressedOld.DecompressPixels();
                    renderData.ResizeAndSetPixels(_originalSize, decompressedPixels);
                    renderData.HandleOnceRenderCompleted();
                }
            }
            
            _canvasData.CanvasSize = _originalSize;
            _requestRenderAction?.Invoke(_originalSize);
        }
    }
}
