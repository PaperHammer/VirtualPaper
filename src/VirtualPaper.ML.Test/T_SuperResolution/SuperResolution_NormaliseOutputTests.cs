using VirtualPaper.Common;
using VirtualPaper.ML.SuperResolution;

namespace VirtualPaper.ML.Test.T_SuperResolution {
    // ====================================================================
    //  辅助：生成测试用图片（不依赖任何外部资源）
    // ====================================================================
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

    // ====================================================================
    //  RunAndSave — 异常分支（不需要真实模型）
    // ====================================================================
    [TestClass]
    [TestCategory("Unit")]
    public class Realesrgan_RunAndSaveExceptionTests {
        private Realesrgan _realesrgan = null!;
        private string _tempDir = null!;
        private string _testImagePath = null!;

        [TestInitialize]
        public void Setup() {
            _tempDir = Path.Combine(Path.GetTempPath(), $"sr_unit_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _testImagePath = TestImageHelper.CreateSolidColorJpeg(dir: _tempDir);
            _realesrgan = new Realesrgan();
        }

        [TestCleanup]
        public void Cleanup() {
            _realesrgan?.Dispose();
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [TestMethod]
        [Description("未调用 LoadModel 就 RunAndSave 应抛出 InvalidOperationException")]
        public void RunAndSave_WithoutLoadModel_ThrowsInvalidOperationException() {
            string outputPath = Path.Combine(_tempDir, "out.jpg");

            Assert.Throws<InvalidOperationException>(
                () => _realesrgan.RunAndSave(_testImagePath, outputPath, 256, 256));
        }

        [TestMethod]
        [Description("输入文件不存在时应抛出 InvalidOperationException（session 未初始化优先）")]
        public void RunAndSave_InputFileNotFound_ThrowsException() {
            string outputPath = Path.Combine(_tempDir, "out.jpg");

            Assert.Throws<InvalidOperationException>(
                () => _realesrgan.RunAndSave("C:\\nonexistent.jpg", outputPath, 256, 256));
        }

        [TestMethod]
        [Description("CancellationToken 已取消时应立即抛出 OperationCanceledException")]
        public void RunAndSave_CancelledToken_ThrowsOperationCanceledException() {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            string outputPath = Path.Combine(_tempDir, "out.jpg");

            Assert.Throws<OperationCanceledException>(
                () => _realesrgan.RunAndSave(_testImagePath, outputPath, 256, 256,
                    ct: cts.Token));
        }

        [TestMethod]
        [Description("Dispose 后再 RunAndSave 应抛出异常")]
        public void RunAndSave_AfterDispose_ThrowsException() {
            _realesrgan.Dispose();
            string outputPath = Path.Combine(_tempDir, "out.jpg");

            Assert.Throws<InvalidOperationException>(
                () => _realesrgan.RunAndSave(_testImagePath, outputPath, 256, 256));
        }

        [TestMethod]
        [Description("LoadModel 未指定路径且默认路径不存在时应抛出 FileNotFoundException")]
        public void LoadModel_NoModelFile_ThrowsFileNotFoundException() {
            Assert.Throws<FileNotFoundException>(() => _realesrgan.LoadModel());
        }
    }

    // ====================================================================
    //  LoadModel — 路径校验（不需要真实模型）
    // ====================================================================
    [TestClass]
    [TestCategory("Unit")]
    public class Realesrgan_LoadModelTests {
        [TestMethod]
        [Description("传入不存在的路径应抛出 FileNotFoundException")]
        public void LoadModel_InvalidPath_ThrowsFileNotFoundException() {
            using var sr = new Realesrgan();

            Assert.Throws<FileNotFoundException>(
                () => sr.LoadModel("C:\\nonexistent\\model.onnx"));
        }
    }

    // ====================================================================
    //  RunAndSave — 集成测试（需要真实模型文件 + 测试图片）
    // ====================================================================
    [TestClass]
    [TestCategory("Integration")]
    public class Realesrgan_IntegrationTests {
        private string _tempDir = null!;
        private string _testImagePath = null!;
        private Realesrgan _realesrgan = null!;
        private readonly string _modelPath =
            Path.Combine(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory)),
                Constants.WorkingDir.ML_SuperResolution_AI_Models,
                Utils.Fields.ModelName);

        [TestInitialize]
        public void Setup() {
            _tempDir = Path.Combine(Path.GetTempPath(), $"sr_int_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _testImagePath = TestImageHelper.CreateSolidColorJpeg(64, 64, _tempDir);

            _realesrgan = new Realesrgan();
            _realesrgan.LoadModel(_modelPath);
        }

        [TestCleanup]
        public void Cleanup() {
            _realesrgan?.Dispose();
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [TestMethod]
        [Description("RunAndSave 应在指定路径生成输出文件")]
        public void RunAndSave_ValidInput_CreatesOutputFile() {
            string outputPath = Path.Combine(_tempDir, "output.jpg");

            _realesrgan.RunAndSave(_testImagePath, outputPath, 256, 256);

            Assert.IsTrue(File.Exists(outputPath), $"Expected output file at: {outputPath}");
        }

        [TestMethod]
        [Description("输出文件应有内容（非空）")]
        public void RunAndSave_ValidInput_OutputFileHasContent() {
            string outputPath = Path.Combine(_tempDir, "output.jpg");

            _realesrgan.RunAndSave(_testImagePath, outputPath, 256, 256);

            Assert.IsGreaterThan(0, new FileInfo(outputPath).Length);
        }

        [TestMethod]
        [Description("返回值应与传入的 outputFilePath 一致")]
        public void RunAndSave_ValidInput_ReturnsOutputPath() {
            string outputPath = Path.Combine(_tempDir, "output.jpg");

            string result = _realesrgan.RunAndSave(_testImagePath, outputPath, 256, 256);

            Assert.AreEqual(outputPath, result);
        }

        [TestMethod]
        [Description("输出图像可被 OpenCvSharp 正常读取")]
        public void RunAndSave_ValidInput_OutputIsReadableImage() {
            string outputPath = Path.Combine(_tempDir, "output.jpg");
            _realesrgan.RunAndSave(_testImagePath, outputPath, 256, 256);

            using var mat = OpenCvSharp.Cv2.ImRead(outputPath);

            Assert.IsFalse(mat.Empty(), "Output image should be readable by OpenCvSharp");
        }

        [TestMethod]
        [Description("输出图像尺寸应与 targetWidth/targetHeight 一致")]
        public void RunAndSave_ValidInput_OutputDimensionsMatchTarget() {
            uint targetW = 128, targetH = 128;
            string outputPath = Path.Combine(_tempDir, "output.jpg");

            _realesrgan.RunAndSave(_testImagePath, outputPath, targetW, targetH);

            using var mat = OpenCvSharp.Cv2.ImRead(outputPath);
            Assert.AreEqual((int)targetW, mat.Width, "Output width should match targetWidth");
            Assert.AreEqual((int)targetH, mat.Height, "Output height should match targetHeight");
        }

        [TestMethod]
        [Description("输出路径为 .png 时应正常生成 PNG 文件")]
        public void RunAndSave_PngOutput_CreatesFile() {
            string outputPath = Path.Combine(_tempDir, "output.png");

            _realesrgan.RunAndSave(_testImagePath, outputPath, 256, 256);

            Assert.IsTrue(File.Exists(outputPath));
        }

        [TestMethod]
        [Description("输出路径为 .webp 时应正常生成 WebP 文件")]
        public void RunAndSave_WebpOutput_CreatesFile() {
            string outputPath = Path.Combine(_tempDir, "output.webp");

            _realesrgan.RunAndSave(_testImagePath, outputPath, 256, 256);

            Assert.IsTrue(File.Exists(outputPath));
        }

        [TestMethod]
        [Description("连续两次调用不同输出路径不应抛异常（无状态残留）")]
        public void RunAndSave_CalledTwice_BothSucceed() {
            string output1 = Path.Combine(_tempDir, "output1.jpg");
            string output2 = Path.Combine(_tempDir, "output2.jpg");

            _realesrgan.RunAndSave(_testImagePath, output1, 128, 128);
            _realesrgan.RunAndSave(_testImagePath, output2, 128, 128);

            Assert.IsTrue(File.Exists(output1));
            Assert.IsTrue(File.Exists(output2));
        }

        [TestMethod]
        [Description("LoadModel 重复调用不应抛异常（幂等）")]
        public void LoadModel_CalledTwice_DoesNotThrow() {
            void act() => _realesrgan.LoadModel(_modelPath);

            act();
        }

        [TestMethod]
        [Description("Dispose 后再 RunAndSave 应抛出异常")]
        public void RunAndSave_AfterDispose_ThrowsException() {
            _realesrgan.Dispose();
            string outputPath = Path.Combine(_tempDir, "output.jpg");

            Assert.Throws<InvalidOperationException>(
                () => _realesrgan.RunAndSave(_testImagePath, outputPath, 256, 256));
        }
    }
}
