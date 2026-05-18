namespace VirtualPaper.ML.DepthEstimate.Models {
    public record DepthEstimateModelOutput(
        float[] Depth,
        int Width,
        int Height,
        int OriginalWidth,
        int OriginalHeight);
}
