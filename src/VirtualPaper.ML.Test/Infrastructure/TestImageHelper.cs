namespace VirtualPaper.ML.Test.Infrastructure {
    /// <summary>
    /// 生成测试用图片的辅助类（不依赖任何外部资源）。
    /// 供 T_DepthEstimate、T_StyleTransfer、T_SuperResolution 等测试类共享使用。
    /// </summary>
    internal static class TestImageHelper {
        /// <summary>
        /// 使用 OpenCvSharp 在临时目录生成一张纯色 JPEG，返回路径。
        /// </summary>
        public static string CreateSolidColorJpeg(
            int width = 64,
            int height = 64,
            string? dir = null) {
            dir ??= Path.GetTempPath();
            string path = Path.Combine(dir, $"test_{Guid.NewGuid():N}.jpg");

            using var mat = new OpenCvSharp.Mat(
                height, width,
                OpenCvSharp.MatType.CV_8UC3,
                new OpenCvSharp.Scalar(128, 64, 32)); // BGR
            mat.SaveImage(path);

            return path;
        }
    }
}
