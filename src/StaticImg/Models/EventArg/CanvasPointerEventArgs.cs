using System;
using Microsoft.UI.Input;

namespace Workloads.Creation.StaticImg.Models.EventArg {
    public class CanvasPointerEventArgs(PointerPoint pointer, InkRenderData renderData, PointerPosition pointerPos) : EventArgs {
        public PointerPoint Pointer { get; } = pointer;
        public InkRenderData RenderData { get; } = renderData;
        public PointerPosition PointerPos { get; } = pointerPos;
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
