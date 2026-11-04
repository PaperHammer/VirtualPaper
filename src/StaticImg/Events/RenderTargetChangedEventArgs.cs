using System;
using Windows.Foundation;

namespace Workloads.Creation.StaticImg.Events {
    public class RenderTargetChangedEventArgs(
        RenderMode mode,
        Rect region = default,
        StrokeCacheDebugInfo? strokeCacheDebugInfo = null) : EventArgs {
        public RenderMode Mode { get; } = mode;
        public Rect Region { get; } = region;
        public StrokeCacheDebugInfo? StrokeCacheDebugInfo { get; } = strokeCacheDebugInfo;
    }

    public sealed record StrokeCacheDebugInfo(
        Rect DirtyBounds,
        Rect ActiveStrokeBounds,
        Rect CacheBounds,
        Rect UpdatedCacheBounds,
        int ActivePointCount);

    public enum RenderMode {
        None, FullRegion, PartialRegion,
    }
}
