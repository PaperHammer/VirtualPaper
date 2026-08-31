namespace VirtualPaper.ML.DynamicImage.Models {
    public sealed record LayerFusionOptions {
        public int MaxObjects { get; init; } = 12;
        public float MinimumObjectAreaRatio { get; init; } = 0.001f;
        public float MinimumSegmentationIoU { get; init; } = 0.5f;
        public float BackgroundQuantile { get; init; } = 1f / 3f;
        public float ForegroundQuantile { get; init; } = 2f / 3f;
        public int MaximumQuantileSamples { get; init; } = 250_000;
        public int MaximumLayerDepthSamples { get; init; } = 20_000;
        public int MinimumOcclusionPixels { get; init; } = 1;
        public float MinimumIndependentSubjectSalience { get; init; } = 0.55f;
        public float PrimarySubjectSalience { get; init; } = 0.72f;
        public float SubjectDominantAreaRatio { get; init; } = 0.15f;
        public float MinimumSceneDepthRange { get; init; } = 0.015f;
        public int DepthProcessingMaxDimension { get; init; } = 1024;
        public int DepthSmoothingDiameter { get; init; } = 5;
        public double DepthSmoothingSigmaColor { get; init; } = 0.08;
        public double DepthSmoothingSigmaSpace { get; init; } = 5;

        internal void Validate() {
            if (MaxObjects <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxObjects));
            if (!float.IsFinite(MinimumObjectAreaRatio) ||
                MinimumObjectAreaRatio < 0f || MinimumObjectAreaRatio >= 1f) {
                throw new ArgumentOutOfRangeException(nameof(MinimumObjectAreaRatio));
            }
            if (!float.IsFinite(MinimumSegmentationIoU) ||
                MinimumSegmentationIoU < 0f || MinimumSegmentationIoU > 1f) {
                throw new ArgumentOutOfRangeException(nameof(MinimumSegmentationIoU));
            }
            if (!float.IsFinite(BackgroundQuantile) ||
                !float.IsFinite(ForegroundQuantile) ||
                BackgroundQuantile <= 0f || ForegroundQuantile >= 1f ||
                BackgroundQuantile >= ForegroundQuantile) {
                throw new ArgumentOutOfRangeException(
                    nameof(BackgroundQuantile),
                    "Depth quantiles must satisfy 0 < background < foreground < 1.");
            }
            if (MaximumQuantileSamples <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaximumQuantileSamples));
            if (MaximumLayerDepthSamples <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaximumLayerDepthSamples));
            if (MinimumOcclusionPixels <= 0)
                throw new ArgumentOutOfRangeException(nameof(MinimumOcclusionPixels));
            if (!float.IsFinite(MinimumIndependentSubjectSalience) ||
                MinimumIndependentSubjectSalience < 0f ||
                MinimumIndependentSubjectSalience > 1f) {
                throw new ArgumentOutOfRangeException(nameof(MinimumIndependentSubjectSalience));
            }
            if (!float.IsFinite(PrimarySubjectSalience) ||
                PrimarySubjectSalience < MinimumIndependentSubjectSalience ||
                PrimarySubjectSalience > 1f) {
                throw new ArgumentOutOfRangeException(nameof(PrimarySubjectSalience));
            }
            if (!float.IsFinite(SubjectDominantAreaRatio) ||
                SubjectDominantAreaRatio <= 0f || SubjectDominantAreaRatio >= 1f) {
                throw new ArgumentOutOfRangeException(nameof(SubjectDominantAreaRatio));
            }
            if (!float.IsFinite(MinimumSceneDepthRange) || MinimumSceneDepthRange < 0f)
                throw new ArgumentOutOfRangeException(nameof(MinimumSceneDepthRange));
            if (DepthProcessingMaxDimension <= 0)
                throw new ArgumentOutOfRangeException(nameof(DepthProcessingMaxDimension));
            if (DepthSmoothingDiameter < 0)
                throw new ArgumentOutOfRangeException(nameof(DepthSmoothingDiameter));
            if (!double.IsFinite(DepthSmoothingSigmaColor) || DepthSmoothingSigmaColor <= 0)
                throw new ArgumentOutOfRangeException(nameof(DepthSmoothingSigmaColor));
            if (!double.IsFinite(DepthSmoothingSigmaSpace) || DepthSmoothingSigmaSpace <= 0)
                throw new ArgumentOutOfRangeException(nameof(DepthSmoothingSigmaSpace));
        }
    }
}
