using System;
using System.Linq;
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
    public record LayerSelectionCommand : IUndoableCommand {
        public string Description { get; } = "Layer Selection";

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

            _compressedSelectionPixels = compressedSelectionPixels;
            _compressedTargetOriginalPixels = compressedTargetOriginalPixels;
            _compressedTargetNewPixels = compressedTargetNewPixels;
            _requestRenderAction = requestRenderAction;
        }

        public async Task ExecuteAsync() {
            var renderData = GetRenderData();
            if (renderData?.RenderTarget == null) return;

            byte[] selPixels = _compressedTargetNewPixels.DecompressPixels();

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
        private readonly byte[] _compressedTargetOriginalPixels; // 目标区域被覆盖前的内容
        private readonly byte[] _compressedTargetNewPixels; // 目标区域被覆盖前的内容
    }
}
