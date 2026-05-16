namespace VirtualPaper.ML.DepthEstimate.Models {
    public class DepthEstimateModelInput {
        public string ImgPath { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public DepthEstimateModelInput(string imgPath, int width, int height) {
            ImgPath = imgPath;
            Width = width;
            Height = height;
        }
    }
}
