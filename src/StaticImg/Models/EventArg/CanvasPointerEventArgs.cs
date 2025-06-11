using System;
using Microsoft.UI.Input;

namespace Workloads.Creation.StaticImg.Models.EventArg {
    public class CanvasPointerEventArgs : EventArgs {
        public PointerPoint Pointer { get; }
        public InkRenderData RenderData { get; }
        
        public CanvasPointerEventArgs(PointerPoint pointer, InkRenderData renderData) {
            Pointer = pointer;
            RenderData = renderData;
        }
    }
}
