using System;
using System.Collections.Generic;
using Windows.Foundation;

namespace Workloads.Creation.StaticImg.Events {
    public class RenderTargetChangedEventArgs(
        RenderMode mode,
        Rect region = default,
        StrokeTileDebugInfo? strokeTileDebugInfo = null) : EventArgs {
        public RenderMode Mode { get; } = mode;
        public Rect Region { get; } = region;
        public StrokeTileDebugInfo? StrokeTileDebugInfo { get; } = strokeTileDebugInfo;
    }

    public sealed record StrokeTileDebugInfo(
        Rect DirtyBounds,
        Rect ActiveStrokeBounds,
        IReadOnlyList<Rect> AllocatedTiles,
        IReadOnlyList<Rect> UpdatedTiles,
        int ActivePointCount);

    public enum RenderMode {
        None, FullRegion, PartialRegion,
    }
}
