using System;
using Microsoft.UI.Input;

namespace Workloads.Creation.StaticImg.Models.EventArg {
    internal class CanvasPointerEventArgs : EventArgs {
        public CanvasPointerEventArgs(PointerPoint pointer, InkRenderData renderData) {
            Pointer = pointer;
            RenderData = renderData;
        }

        public PointerPoint Pointer { get; }
        public InkRenderData RenderData { get; }
    }
}
