using System;
using Microsoft.Graphics.Canvas;

namespace Workloads.Creation.StaticImg.Events {
    public class CanvasPointerEventArgs : EventArgs {
        public CanvasPointerEventArgs(Microsoft.UI.Input.PointerPoint pointerPoint, CanvasRenderTarget renderTarget, PointerPosition pointerPos, Guid layerId) {
            Pointer = pointerPoint;
            Position = pointerPoint.Position;
            RenderTarget = renderTarget;
            PointerPos = pointerPos;
            LayerId = layerId;
        }

        public Windows.Foundation.Point Position { get; }
        public Microsoft.UI.Input.PointerPoint Pointer { get; }
        public CanvasRenderTarget RenderTarget { get; }
        public PointerPosition PointerPos { get; }
        public Guid LayerId { get; }
    }

    /// <summary>
    /// 描述指针相对于画布和容器的位置状态
    /// </summary>
    public enum PointerPosition {
        /// <summary>
        /// 无位置状态（默认/未识别）
        /// </summary>
        None,

        /// <summary>
        /// 在画布内部（有效绘制区域）
        /// </summary>
        InsideCanvas,

        /// <summary>
        /// 在画布外但在容器内（绘制区域外, 可滚动区域内）
        /// </summary>
        InsideContainer,

        /// <summary>
        /// 完全在容器外部（无效区域）
        /// </summary>
        OutsideContainer
    }
}
