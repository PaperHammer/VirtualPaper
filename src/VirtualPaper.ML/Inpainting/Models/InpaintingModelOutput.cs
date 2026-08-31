namespace VirtualPaper.ML.Inpainting.Models {
    public sealed record InpaintingModelOutput(
        byte[] BgrPixels,
        int Width,
        int Height,
        bool WasApplied) {

        /// <summary>
        /// Actual white=remove mask used after hole filling and expansion.
        /// </summary>
        public byte[] AppliedMask { get; init; } = [];

        /// <summary>
        /// Expansion radius available as a conservative parallax safety margin.
        /// </summary>
        public int SafetyMarginPixels { get; init; }
    }
}
