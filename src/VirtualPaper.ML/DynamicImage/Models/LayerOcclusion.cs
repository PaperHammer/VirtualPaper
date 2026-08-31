namespace VirtualPaper.ML.DynamicImage.Models {
    public sealed record LayerOcclusion(
        string OccluderLayerId,
        string OccludedLayerId,
        int OverlapPixelCount,
        float OccludedAreaRatio);
}
