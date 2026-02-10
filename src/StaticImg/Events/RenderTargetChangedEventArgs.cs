using System;
using Windows.Foundation;

namespace Workloads.Creation.StaticImg.Events {
    public class RenderTargetChangedEventArgs(RenderMode mode, Rect region = default) : EventArgs {
        public RenderMode Mode { get; } = mode;
        public Rect Region { get; } = region;
    }

    public enum RenderMode {
        None, FullRegion, PartialRegion,
    }
}
