using Windows.Foundation;

namespace BuiltIn.Events {
    public class RenderTargetChangedEventArgs(RenderMode mode, Rect region = default) : EventArgs {
        public Rect Region { get; } = region;
        public RenderMode Mode { get; } = mode;
    }

    public enum RenderMode {
        None, FullRegion, PartialRegion,
    }
}
