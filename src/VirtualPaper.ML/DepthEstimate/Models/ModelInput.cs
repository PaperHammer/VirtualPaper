namespace VirtualPaper.ML.DepthEstimate.Models
{
    public class ModelInput
    {
        public string ImgPath { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public ModelInput(string imgPath, int width, int height)
        {
            ImgPath = imgPath;
            Width = width;
            Height = height;
        }
    }
}
