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
                //Parallel.ForEach(_canvasData.Layers, item => {
                //    if (item.RenderData?.RenderTarget != null) {
                //        byte[] compressedOld = item.RenderData.RenderTarget.GetPixelBytes().CompressPixels();
                //        originalPixelsDict.TryAdd(item.Tag, compressedOld);
                //    }
                //});
                //_compressedOriginalPixels = originalPixelsDict.ToDictionary();
                //_compressedOriginalPixels = [];
                //foreach (var layer in _canvasData.Layers) {
                //    if (layer.RenderData?.RenderTarget != null) {
                //        byte[] compressedOld = layer.RenderData.RenderTarget.GetPixelBytes().CompressPixels();
                //        _compressedOriginalPixels.TryAdd(layer.Tag, compressedOld);
                //    }
                //}

                //foreach (var layer in _canvasData.Layers) {
                //    var renderData = layer.RenderData;
                //    if (renderData == null) continue;

                //    await renderData.ResizeRenderTargetAsync(_newSize);
                //    renderData.HandleOnceRenderCompleted();
                //}
                var tasks = _canvasData.Layers
                    .Where(ink => ink.RenderData != null)
                    .Select(async ink => {
                        byte[] compressedOld = ink.RenderData.RenderTarget.GetPixelBytes().CompressPixels();
                        originalPixelsDict.TryAdd(ink.Tag, compressedOld);
                        await ink.RenderData.ResizeRenderTargetAsync(_newSize);
                        ink.RenderData.HandleOnceRenderCompleted();
                    });
                await Task.WhenAll(tasks);
                _compressedOriginalPixels = originalPixelsDict.ToDictionary();

                var newPixelsDict = new ConcurrentDictionary<Guid, byte[]>();
                Parallel.ForEach(_canvasData.Layers, item => {
                    if (item.RenderData?.RenderTarget != null) {
                        byte[] compressedNew = item.RenderData.RenderTarget.GetPixelBytes().CompressPixels();
                        newPixelsDict.TryAdd(item.Tag, compressedNew);
                    }
                });
                _compressedNewPixels = newPixelsDict.ToDictionary();
                //_compressedNewPixels = [];
                //foreach (var layer in _canvasData.Layers) {
                //    if (layer.RenderData?.RenderTarget != null) {
                //        byte[] compressedNew = layer.RenderData.RenderTarget.GetPixelBytes().CompressPixels();
                //        _compressedNewPixels.TryAdd(layer.Tag, compressedNew);
                //    }
                //}

                _canvasData.CanvasSize = _newSize;
                _requestRenderAction?.Invoke(_newSize);
            }
            else {
                //foreach (var layer in _canvasData.Layers) {
                //    var renderData = layer.RenderData;
                //    if (renderData == null) continue;

                //    if (_compressedNewPixels != null && _compressedNewPixels.TryGetValue(layer.Tag, out byte[]? compressedNew)) {
                //        renderData.ResizeAndSetPixels(_newSize, compressedNew.DecompressPixels());
                //        renderData.HandleOnceRenderCompleted();
                //    }
                //}
                if (_compressedNewPixels == null) return;
                var tasks = _canvasData.Layers
                .Where(ink => ink.RenderData != null)
                .Select(async ink => await Task.Run(() => {
                    if (_compressedNewPixels.TryGetValue(ink.Tag, out byte[]? compressedPixels)) {
                        byte[] pixels = compressedPixels.DecompressPixels();
                        ink.RenderData.ResizeAndSetPixels(_newSize, pixels);
                        ink.RenderData.HandleOnceRenderCompleted();
                    }
                }));
                await Task.WhenAll(tasks);

                _canvasData.CanvasSize = _newSize;
                _requestRenderAction?.Invoke(_newSize);
            }
        }

        public async Task UndoAsync() {
            //foreach (var layer in _canvasData.Layers) {
            //    var renderData = layer.RenderData;
            //    if (renderData == null) continue;

            //    if (_compressedOriginalPixels != null && _compressedOriginalPixels.TryGetValue(layer.Tag, out byte[]? compressedOld)) {
            //        renderData.ResizeAndSetPixels(_originalSize, compressedOld.DecompressPixels());
            //        renderData.HandleOnceRenderCompleted();
            //    }
            //}
            if (_compressedOriginalPixels == null) return;
            var tasks = _canvasData.Layers
            .Where(ink => ink.RenderData != null)
            .Select(async ink => await Task.Run(() => {
                if (_compressedOriginalPixels.TryGetValue(ink.Tag, out byte[]? compressedPixels)) {
                    byte[] pixels = compressedPixels.DecompressPixels();
                    ink.RenderData.ResizeAndSetPixels(_originalSize, pixels);
                    ink.RenderData.HandleOnceRenderCompleted();
                }
            }));
            await Task.WhenAll(tasks);

            _canvasData.CanvasSize = _originalSize;
            _requestRenderAction?.Invoke(_originalSize);
        }
    }
}
