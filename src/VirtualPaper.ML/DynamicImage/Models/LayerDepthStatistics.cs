namespace VirtualPaper.ML.DynamicImage.Models {
    public sealed record LayerDepthStatistics(
        float Minimum,
        float Maximum,
        float Mean,
        float Median,
        int PixelCount);
}
