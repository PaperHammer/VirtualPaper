namespace VirtualPaper.ML.DynamicImage.Models {
    public sealed record DynamicImageLayerPlan(
        IReadOnlyList<DynamicImageLayer> Layers,
        IReadOnlyList<LayerOcclusion> Occlusions,
        byte[] InpaintingMask,
        float[] RenderDepth,
        int Width,
        int Height,
        float BackgroundDepthThreshold,
        float ForegroundDepthThreshold,
        DynamicImageSceneKind SceneKind,
        IReadOnlyList<DynamicImageSubjectAssessment> Subjects) {

        /// <summary>
        /// Layers are stored from farthest to nearest for back-to-front rendering.
        /// </summary>
        public IReadOnlyList<DynamicImageLayer> BackToFrontLayers => Layers;

        public bool RequiresBackgroundReconstruction =>
            InpaintingMask.Any(value => value != 0);
    }
}
