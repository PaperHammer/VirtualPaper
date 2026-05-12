namespace VirtualPaper.ML.DepthEstimate.Models {
    public class DepthEstimateModelOutput {
        public float[] Depth { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int OriginalWidth { get; set; }
        public int OriginalHeight { get; set; }

        public DepthEstimateModelOutput(
            float[] depth,
            int width,
            int height,
            int originalWidth,
            int originalHeight) {
            Depth = depth;
            Width = width;
            Height = height;
            OriginalWidth = originalWidth;
            OriginalHeight = originalHeight;
        }
    }
}
