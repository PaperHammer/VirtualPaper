namespace VirtualPaper.ML.DynamicImage.Models {
    public sealed record DynamicImageLayerExport(
        string LayerId,
        string SourceAlphaPath,
        string VisibleAlphaPath,
        string SourceCutoutPath,
        string VisibleCutoutPath);
}
