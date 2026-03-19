using System;
using System.Threading.Tasks;
using VirtualPaper.Common.Utils.UndoRedo;
using Workloads.Creation.StaticImg.Models.Specific;

namespace Workloads.Creation.StaticImg.Core.UndoRedoCommand {
    /// <summary>
    /// Represents an undoable command that applies a global, lossless transformation (such as rotation or flipping) 
    /// to the entire ink canvas.
    /// </summary>
    /// <remarks>
    /// Use this command to support undo and redo operations for canvas-level structural changes. 
    /// The command automatically calculates and stores the exact integer dimensions for both the forward (new) 
    /// and backward (old) states, mitigating floating-point precision errors. It infers the inverse transformation 
    /// (e.g., mapping RotateLeft to RotateRight) to restore the canvas perfectly when undone. 
    /// Execution inherently triggers the reactive resizing and redrawing of underlying Win2D render targets.
    /// </remarks>
    public record CanvasTransformCommand : IUndoableCommand {
        public string Description { get; } = "CanvasTransform";

        private readonly InkCanvasData _canvasData;
        private readonly ArcSize _newSize;
        private readonly ArcSize _oldSize;

        public CanvasTransformCommand(InkCanvasData canvasData, ArcSize oldSize, ArcSize newSize) {
            _canvasData = canvasData;

            // 去除潜在的误差
            float oldWidth = MathF.Round((float)oldSize.Width);
            float oldHeight = MathF.Round((float)oldSize.Height);
            float newWidth = MathF.Round((float)newSize.Width);
            float newHeight = MathF.Round((float)newSize.Height);

            RebuildMode executeMode = newSize.Rebuild;

            float finalNewWidth = executeMode switch {
                RebuildMode.RotateLeft or RebuildMode.RotateRight => oldHeight,   // 旋转：取旧高度
                RebuildMode.FlipHorizontal or RebuildMode.FlipVertical => oldWidth, // 翻转：保持旧宽度不变
                _ => newWidth // 缩放/裁剪：新尺寸
            };
            float finalNewHeight = executeMode switch {
                RebuildMode.RotateLeft or RebuildMode.RotateRight => oldWidth,    // 旋转：取旧宽度
                RebuildMode.FlipHorizontal or RebuildMode.FlipVertical => oldHeight,// 翻转：保持旧高度不变
                _ => newHeight // 缩放/裁剪：新尺寸
            };
            _newSize = new ArcSize(finalNewWidth, finalNewHeight, newSize.Dpi, executeMode);

            RebuildMode undoMode = executeMode switch {
                RebuildMode.RotateLeft => RebuildMode.RotateRight,
                RebuildMode.RotateRight => RebuildMode.RotateLeft,
                RebuildMode.FlipHorizontal => RebuildMode.FlipHorizontal,
                RebuildMode.FlipVertical => RebuildMode.FlipVertical,
                RebuildMode.ResizeScale => RebuildMode.ResizeScale,
                RebuildMode.ResizeExpand => RebuildMode.ResizeExpand,
                _ => RebuildMode.None
            };
            _oldSize = new ArcSize(oldWidth, oldHeight, oldSize.Dpi, undoMode);
        }

        public async Task ExecuteAsync() {
            _canvasData.CanvasSize = _newSize;
        }

        public async Task UndoAsync() {
            _canvasData.CanvasSize = _oldSize;
        }
    }
}
