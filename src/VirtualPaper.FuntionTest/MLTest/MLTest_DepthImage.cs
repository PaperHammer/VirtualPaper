using VirtualPaper.ML.DepthEstimate;

namespace VirtualPaper.FuntionTest.MLTest {
    internal class MLTest_DepthImage {
        public void GetDepthImage() {
            MiDaS.LoadModel("D:\\Virtuals\\VirtualPaper\\src\\SourceCode\\VirtualPaper.ML\\Models\\model-small.onnx");
            var v0 = MiDaS.Run("C:\\Users\\PaperHammer\\Desktop\\img29.jpg");
            MiDaS.SaveDepthMap(v0.Depth, v0.Width, v0.Height, v0.OriginalWidth, v0.OriginalHeight, "C:\\Users\\PaperHammer\\Desktop\\_img29.jpg");
        }
    }
}
