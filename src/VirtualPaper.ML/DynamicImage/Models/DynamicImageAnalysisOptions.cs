using VirtualPaper.ML.DepthEstimate.Models;
using VirtualPaper.ML.ObjectDetection.Models;
using VirtualPaper.ML.Segmentation.Models;
using VirtualPaper.ML.Inpainting.Models;

namespace VirtualPaper.ML.DynamicImage.Models {
    public sealed record DynamicImageAnalysisOptions {
        public ObjectDetectionOptions Detection { get; init; } = new() {
            InputWidth = 640,
            InputHeight = 640,
            ScoreThreshold = 0.3f
        };

        public MobileSamOptions Segmentation { get; init; } = new() {
            MaskThreshold = 0f,
            MaxBoxes = 12
        };

        public DepthAnythingOptions Depth { get; init; } = new() {
            InputSize = 518,
            ResizeMode = DepthAnythingResizeMode.FitLongestSide
        };

        public LayerFusionOptions Fusion { get; init; } = new();
        public MIGanOptions Inpainting { get; init; } = new();
        public bool MergeDuplicateObjects { get; init; } = true;
        public float DuplicateMaskContainmentThreshold { get; init; } = 0.9f;

        internal void Validate() {
            ArgumentNullException.ThrowIfNull(Detection);
            ArgumentNullException.ThrowIfNull(Segmentation);
            ArgumentNullException.ThrowIfNull(Depth);
            ArgumentNullException.ThrowIfNull(Fusion);
            ArgumentNullException.ThrowIfNull(Inpainting);
            Detection.Validate();
            Segmentation.Validate();
            Depth.Validate();
            Fusion.Validate();
            Inpainting.Validate();

            if (!float.IsFinite(DuplicateMaskContainmentThreshold) ||
                DuplicateMaskContainmentThreshold <= 0f ||
                DuplicateMaskContainmentThreshold > 1f) {
                throw new ArgumentOutOfRangeException(nameof(DuplicateMaskContainmentThreshold));
            }

            if (Segmentation.MaxBoxes < Fusion.MaxObjects) {
                throw new ArgumentException(
                    "MobileSAM MaxBoxes must be greater than or equal to fusion MaxObjects.");
            }
        }
    }
}
