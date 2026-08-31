namespace VirtualPaper.ML.Segmentation.Models {
    public sealed record MobileSamOptions {
        /// <summary>
        /// SAM returns mask logits. Pixels above this value become opaque.
        /// The official SAM default is 0.
        /// </summary>
        public float MaskThreshold { get; init; } = 0f;

        /// <summary>
        /// Protects CPU latency from an unexpectedly large detector result set.
        /// </summary>
        public int MaxBoxes { get; init; } = 20;

        internal void Validate() {
            if (!float.IsFinite(MaskThreshold))
                throw new ArgumentOutOfRangeException(
                    nameof(MaskThreshold),
                    MaskThreshold,
                    "Mask threshold must be finite.");

            if (MaxBoxes <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(MaxBoxes),
                    MaxBoxes,
                    "Maximum box count must be positive.");
        }
    }
}
