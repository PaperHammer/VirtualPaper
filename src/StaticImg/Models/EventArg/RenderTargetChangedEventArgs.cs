using System;

namespace Workloads.Creation.StaticImg.Models.EventArg {
    public class RenderTargetChangedEventArgs : EventArgs {
        public InkRenderData RenderData { get; }
        public RenderMode Mode { get; }
        
        public RenderTargetChangedEventArgs(InkRenderData renderData, RenderMode mode) {
            RenderData = renderData;
            Mode = mode;
        }
    }
}
