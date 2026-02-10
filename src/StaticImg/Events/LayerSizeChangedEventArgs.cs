namespace Workloads.Creation.StaticImg.Events {
    public class LayerSizeChangedEventArgs(ArcSize newSize) : RenderTargetChangedEventArgs(RenderMode.FullRegion) {
        public ArcSize NewSize { get; } = newSize;
    }
}
