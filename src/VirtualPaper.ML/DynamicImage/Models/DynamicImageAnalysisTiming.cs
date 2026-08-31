namespace VirtualPaper.ML.DynamicImage.Models {
    public sealed record DynamicImageAnalysisTiming(
        TimeSpan Detection,
        TimeSpan Segmentation,
        TimeSpan DepthEstimation,
        TimeSpan LayerFusion,
        TimeSpan BackgroundInpainting) {

        public TimeSpan Total => Detection + Segmentation + DepthEstimation +
            LayerFusion + BackgroundInpainting;
    }
}
