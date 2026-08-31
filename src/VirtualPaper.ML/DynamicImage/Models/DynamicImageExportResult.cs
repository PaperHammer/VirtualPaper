namespace VirtualPaper.ML.DynamicImage.Models {
    public sealed record DynamicImageExportResult(
        string OutputDirectory,
        string ManifestPath,
        string DepthMapPath,
        string InpaintingMaskPath,
        string LayerOrderPreviewPath,
        IReadOnlyList<DynamicImageLayerExport> Layers) {

        /// <summary>
        /// Unprocessed model output retained for diagnostics. DepthMapPath is
        /// the stabilized map intended for animation.
        /// </summary>
        public string RawDepthMapPath { get; init; } = string.Empty;
        public string BackgroundPlatePath { get; init; } = string.Empty;
        public string BackgroundDepthMapPath { get; init; } = string.Empty;
        public string AppliedInpaintingMaskPath { get; init; } = string.Empty;
        public int InpaintingSafetyMarginPixels { get; init; }
        public string MotionConfigurationPath { get; init; } = string.Empty;
        public string WebPackagePath { get; init; } = string.Empty;
    }
}
