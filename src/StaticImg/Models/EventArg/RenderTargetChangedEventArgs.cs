using System;
using Windows.Foundation;

namespace Workloads.Creation.StaticImg.Models.EventArg {
    public class RenderTargetChangedEventArgs(RenderMode mode, Rect region = default) : EventArgs {
        public Rect Region { get; } = region;
        public RenderMode Mode { get; } = mode;
    }

    public enum RenderMode {
        None, FullRegion, PartialRegion,
    }
}
