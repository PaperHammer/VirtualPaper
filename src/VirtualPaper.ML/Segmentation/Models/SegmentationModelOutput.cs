namespace VirtualPaper.ML.Segmentation.Models {
    public sealed record SegmentationModelOutput(
        IReadOnlyList<SegmentationMask> Masks,
        int OriginalWidth,
        int OriginalHeight,
        int EncoderInputSize,
        int ResizedWidth,
        int ResizedHeight,
        float ScaleX,
        float ScaleY);
}
