namespace VirtualPaper.ML.ObjectDetection.Models {
    public sealed record ObjectDetectionModelOutput(
        IReadOnlyList<DetectedObject> Detections,
        int OriginalWidth,
        int OriginalHeight,
        int InputWidth,
        int InputHeight,
        int ResizedWidth,
        int ResizedHeight,
        float ScaleX,
        float ScaleY);
}
