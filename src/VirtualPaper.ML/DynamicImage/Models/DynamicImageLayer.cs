using VirtualPaper.ML.ObjectDetection.Models;

namespace VirtualPaper.ML.DynamicImage.Models {
    public sealed record DynamicImageLayer(
        string Id,
        DynamicImageLayerKind Kind,
        SceneDepthBand DepthBand,
        LayerDepthStatistics Depth,
        byte[] SourceAlpha,
        byte[] VisibleAlpha,
        DetectedObject? Detection,
        float? SegmentationIoU,
        IReadOnlyList<string> OccludedByLayerIds) {

        public bool IsObject => Kind == DynamicImageLayerKind.DetectedObject;
        public float? SubjectSalience { get; init; }
        public DynamicImageSubjectRole? SubjectRole { get; init; }
    }
}
