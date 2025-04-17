using System;
using Microsoft.UI.Input;

namespace Workloads.Creation.StaticImg.Models.EventArg {
    internal class CanvasPointerEventArgs(PointerPoint pointer, InkRenderData renderData) : EventArgs {
        public PointerPoint Pointer { get;} = pointer;
        public InkRenderData RenderData { get; } = renderData;
    }
}
