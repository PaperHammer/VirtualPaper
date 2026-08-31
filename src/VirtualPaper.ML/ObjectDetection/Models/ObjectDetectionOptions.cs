namespace VirtualPaper.ML.ObjectDetection.Models {
    public sealed record ObjectDetectionOptions {
        public int InputWidth { get; init; } = 640;
        public int InputHeight { get; init; } = 640;
        public float ScoreThreshold { get; init; } = 0.25f;

        internal void Validate() {
            if (InputWidth <= 0 || InputWidth % 32 != 0)
                throw new ArgumentOutOfRangeException(
                    nameof(InputWidth),
                    InputWidth,
                    "Input width must be a positive multiple of 32.");

            if (InputHeight <= 0 || InputHeight % 32 != 0)
                throw new ArgumentOutOfRangeException(
                    nameof(InputHeight),
                    InputHeight,
                    "Input height must be a positive multiple of 32.");

            if (!float.IsFinite(ScoreThreshold) || ScoreThreshold < 0f || ScoreThreshold > 1f)
                throw new ArgumentOutOfRangeException(
                    nameof(ScoreThreshold),
                    ScoreThreshold,
                    "Score threshold must be in [0, 1].");
        }
    }
}
