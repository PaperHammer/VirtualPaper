using VirtualPaper.ML.ObjectDetection.Models;

namespace VirtualPaper.ML.DynamicImage.Models {
    public sealed record DynamicImageSubjectAssessment(
        DetectedObject Detection,
        float SegmentationIoU,
        LayerDepthStatistics Depth,
        float Salience,
        DynamicImageSubjectRole Role,
        bool IsIndependent);
}
