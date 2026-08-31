namespace VirtualPaper.ML.DepthEstimate.Models {
    public sealed record DepthAnythingOptions {
        public int InputSize { get; init; } = 518;
        public DepthAnythingResizeMode ResizeMode { get; init; } =
            DepthAnythingResizeMode.FitLongestSide;

        internal void Validate() {
            if (InputSize < 14 || InputSize % 14 != 0)
                throw new ArgumentOutOfRangeException(
                    nameof(InputSize),
                    InputSize,
                    "Depth Anything input size must be a positive multiple of 14.");

            if (!Enum.IsDefined(ResizeMode))
                throw new ArgumentOutOfRangeException(nameof(ResizeMode));
        }
    }
}
