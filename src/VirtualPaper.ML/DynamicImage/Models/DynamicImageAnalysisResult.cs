using VirtualPaper.ML.DepthEstimate.Models;
using VirtualPaper.ML.ObjectDetection.Models;
using VirtualPaper.ML.Segmentation.Models;
using VirtualPaper.ML.Inpainting.Models;

namespace VirtualPaper.ML.DynamicImage.Models {
    public sealed record DynamicImageAnalysisResult(
        ObjectDetectionModelOutput Detection,
        IReadOnlyList<DetectedObject> SelectedDetections,
        SegmentationModelOutput Segmentation,
        DepthEstimateModelOutput Depth,
        DynamicImageLayerPlan LayerPlan,
        InpaintingModelOutput BackgroundPlate,
        DynamicImageAnalysisTiming Timing);
}
