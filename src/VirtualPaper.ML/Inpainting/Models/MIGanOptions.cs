namespace VirtualPaper.ML.Inpainting.Models {
    public sealed record MIGanOptions {
        /// <summary>
        /// Minimum expansion around the removal mask in source-image pixels.
        /// </summary>
        public int MaskExpansionPixels { get; init; } = 8;

        /// <summary>
        /// Resolution-relative expansion, based on the image's shorter side.
        /// The larger value of this and <see cref="MaskExpansionPixels"/> is used.
        /// </summary>
        public float MaskExpansionRatio { get; init; } = 0.01f;

        public int MaximumMaskExpansionPixels { get; init; } = 64;
        public bool FillSmallEnclosedHoles { get; init; } = true;
        public float MaximumHoleAreaRatio { get; init; } = 0.00025f;
        public int MaximumHoleAreaPixels { get; init; } = 4096;

        internal void Validate() {
            if (MaskExpansionPixels < 0 || MaskExpansionPixels > 128)
                throw new ArgumentOutOfRangeException(nameof(MaskExpansionPixels));
            if (!float.IsFinite(MaskExpansionRatio) ||
                MaskExpansionRatio < 0f || MaskExpansionRatio > 0.05f) {
                throw new ArgumentOutOfRangeException(nameof(MaskExpansionRatio));
            }
            if (MaximumMaskExpansionPixels < MaskExpansionPixels ||
                MaximumMaskExpansionPixels > 256) {
                throw new ArgumentOutOfRangeException(nameof(MaximumMaskExpansionPixels));
            }
            if (!float.IsFinite(MaximumHoleAreaRatio) ||
                MaximumHoleAreaRatio < 0f || MaximumHoleAreaRatio > 0.01f) {
                throw new ArgumentOutOfRangeException(nameof(MaximumHoleAreaRatio));
            }
            if (MaximumHoleAreaPixels < 0)
                throw new ArgumentOutOfRangeException(nameof(MaximumHoleAreaPixels));
        }
    }
}
