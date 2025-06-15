using System;
using Windows.Foundation;

namespace Workloads.Creation.StaticImg.Models.EventArg {
    public class RenderTargetChangedEventArgs : EventArgs {
        public Rect Region { get; }
        public RenderMode Mode { get; }
        
        public RenderTargetChangedEventArgs(RenderMode mode, Rect region = default) {
            Region = region;
            Mode = mode;
        }
    }

    public enum RenderMode {
        FullRegion,
        PartialRegion,
    }
}
