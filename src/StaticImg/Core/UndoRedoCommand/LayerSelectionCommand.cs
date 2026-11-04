using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common.Extensions;
using VirtualPaper.Common.Utils.UndoRedo;
using Windows.Foundation;
using Workloads.Creation.StaticImg.Models;
using Workloads.Creation.StaticImg.Models.Specific;

namespace Workloads.Creation.StaticImg.Core.UndoRedoCommand {
    /// <summary>
    /// Represents an undoable command that moves a selected region of pixels from one location to another within an
    /// ink canvas layer.
    /// </summary>
    /// <remarks>Use this command to support undo and redo operations when moving selections on an ink
    /// canvas. The command stores the necessary pixel data and coordinates to perform the move and to restore the
    /// previous state if undone. The move operation is executed asynchronously and triggers a render update for the
    /// affected regions.</remarks>
    public record LayerSelectionCommand : IUndoableCommand, IMemoryAwareUndoableCommand, IDiskSpillableUndoCommand {
        public string Description { get; } = "Layer Selection";
        public long EstimatedMemoryBytes =>
            _selectionPixels.ResidentMemoryBytes +
            _targetOriginalPixels.ResidentMemoryBytes +
            _targetNewPixels.ResidentMemoryBytes +
            256;
        long IDiskSpillableUndoCommand.DiskStorageBytes =>
            _selectionPixels.DiskStorageBytes +
            _targetOriginalPixels.DiskStorageBytes +
            _targetNewPixels.DiskStorageBytes;

        public LayerSelectionCommand(
            Guid layerId,
            InkCanvasData canvasData,
            Rect originalRect,
            Rect newRect,
            byte[] compressedSelectionPixels,
            byte[] compressedTargetOriginalPixels,
            byte[] compressedTargetNewPixels,
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

            _selectionPixels = new UndoDiskPayload(compressedSelectionPixels);
            _targetOriginalPixels = new UndoDiskPayload(compressedTargetOriginalPixels);
            _targetNewPixels = new UndoDiskPayload(compressedTargetNewPixels);
            _requestRenderAction = requestRenderAction;
        }

        public async Task ExecuteAsync() {
            byte[] selPixels = await ReadPixelsAsync(_targetNewPixels);
            var renderData = GetRenderData();
            if (renderData?.RenderTarget == null) return;

            // 将原区域填为透明
            byte[] transparentPixels = new byte[selPixels.Length];
            renderData.RenderTarget.SetPixelBytes(transparentPixels, _ox, _oy, _w, _h);
            // 将内容盖到新区域
            renderData.RenderTarget.SetPixelBytes(selPixels, _nx, _ny, _w, _h);

            renderData.HandleOnceRenderCompleted();
            _requestRenderAction(new Rect(_ox, _oy, _w, _h).UnionRect(new Rect(_nx, _ny, _w, _h)));

        }

        public async Task UndoAsync() {
            Task<byte[]> selectionTask = ReadPixelsAsync(_selectionPixels);
            Task<byte[]> targetTask = ReadPixelsAsync(_targetOriginalPixels);
            await Task.WhenAll(selectionTask, targetTask);
            byte[] selPixels = await selectionTask;
            byte[] targetOriginal = await targetTask;
            var renderData = GetRenderData();
            if (renderData?.RenderTarget == null) return;

            // 先将目标区域恢复
            renderData.RenderTarget.SetPixelBytes(targetOriginal, _nx, _ny, _w, _h);
            // 将移动走的内容放回原处
            renderData.RenderTarget.SetPixelBytes(selPixels, _ox, _oy, _w, _h);

            renderData.HandleOnceRenderCompleted();
            _requestRenderAction(new Rect(_ox, _oy, _w, _h).UnionRect(new Rect(_nx, _ny, _w, _h)));

        }

        async Task<bool> IDiskSpillableUndoCommand.TrySpillToDiskAsync(
            UndoDiskStore store,
            CancellationToken cancellationToken) {
            bool selectionSpilled = await _selectionPixels.TrySpillToDiskAsync(store, cancellationToken);
            bool targetOriginalSpilled = await _targetOriginalPixels.TrySpillToDiskAsync(store, cancellationToken);
            bool targetNewSpilled = await _targetNewPixels.TrySpillToDiskAsync(store, cancellationToken);
            return selectionSpilled || targetOriginalSpilled || targetNewSpilled;
        }

        public void Dispose() {
            _selectionPixels.Dispose();
            _targetOriginalPixels.Dispose();
            _targetNewPixels.Dispose();
        }

        private static async Task<byte[]> ReadPixelsAsync(UndoDiskPayload payload) {
            byte[] compressedPixels = await payload.ReadAsync();
            return await Task.Run(compressedPixels.DecompressPixels);
        }

        private InkRenderData? GetRenderData() {
            var layer = _canvasData.Layers.FirstOrDefault(l => l.Tag == _layerId);
            return layer?.RenderData;
        }

        private readonly Guid _layerId;
        private readonly InkCanvasData _canvasData;
        private readonly Action<Rect> _requestRenderAction;
        private readonly int _ox, _oy, _nx, _ny, _w, _h; // 坐标尺寸数据
        private readonly UndoDiskPayload _selectionPixels; // 被移动的图像内容
        private readonly UndoDiskPayload _targetOriginalPixels; // 目标区域被覆盖前的内容
        private readonly UndoDiskPayload _targetNewPixels; // 目标区域移动后的内容
    }
}
