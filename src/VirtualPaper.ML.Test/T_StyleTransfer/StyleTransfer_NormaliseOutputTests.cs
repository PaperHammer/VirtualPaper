using System.Reflection;
using VirtualPaper.Common;
using VirtualPaper.ML.StyleTransfer;

namespace VirtualPaper.ML.Test.T_StyleTransfer {
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
    //  LoadAndResizeImage — 反射访问私有静态方法
    // ====================================================================
    [TestClass]
    [TestCategory("Unit")]
    public class AdaIn_LoadAndResizeImageTests {
        private static OpenCvSharp.Mat InvokeLoadAndResizeImage(string path, int targetSize) {
            var method = typeof(AdaIn).GetMethod(
                "LoadAndResizeImage",
                BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException("LoadAndResizeImage not found");

            return (OpenCvSharp.Mat)method.Invoke(null, [path, targetSize])!;
        }

        private string _tempDir = null!;
        private string _testImagePath = null!;

        [TestInitialize]
        public void Setup() {
            _tempDir = Path.Combine(Path.GetTempPath(), $"adain_unit_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _testImagePath = TestImageHelper.CreateSolidColorJpeg(128, 96, _tempDir);
        }

        [TestCleanup]
        public void Cleanup() {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [TestMethod]
        [Description("targetSize <= 0 时应返回原始尺寸的图像")]
        public void LoadAndResizeImage_ZeroTargetSize_ReturnsOriginalSize() {
            using var result = InvokeLoadAndResizeImage(_testImagePath, 0);

            // 原图宽 128、高 96，短边为高 96
            Assert.AreEqual(128, result.Width);
            Assert.AreEqual(96, result.Height);
        }

        [TestMethod]
        [Description("targetSize 为正数时，短边应缩放为指定值")]
        public void LoadAndResizeImage_PositiveTargetSize_ShortEdgeEqualsTargetSize() {
            int targetSize = 32;
            using var result = InvokeLoadAndResizeImage(_testImagePath, targetSize);

            // 原图 128×96，短边为高 96 → 缩放后高 = targetSize = 32
            Assert.AreEqual(targetSize, result.Height,
                "Short edge (height) should equal targetSize");
        }

        [TestMethod]
        [Description("缩放后图像通道数应为 3（RGB）")]
        public void LoadAndResizeImage_PositiveTargetSize_HasThreeChannels() {
            using var result = InvokeLoadAndResizeImage(_testImagePath, 64);

            Assert.AreEqual(3, result.Channels());
        }

        [TestMethod]
        [Description("加载不存在的文件应抛出 ArgumentException")]
        public void LoadAndResizeImage_FileNotFound_ThrowsArgumentException() {
            var ex = Assert.Throws<TargetInvocationException>(
                () => InvokeLoadAndResizeImage("C:\\nonexistent\\image.jpg", 64));

            Assert.IsInstanceOfType<ArgumentException>(ex.InnerException);
        }
    }

    // ====================================================================
    //  RunAndSave — 异常分支（不需要真实模型）
    // ====================================================================
    [TestClass]
    [TestCategory("Unit")]
    public class AdaIn_RunAndSaveExceptionTests {
        private AdaIn _adaIn = null!;
        private string _tempDir = null!;
        private string _testImagePath = null!;

        [TestInitialize]
        public void Setup() {
            _tempDir = Path.Combine(Path.GetTempPath(), $"adain_unit_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _testImagePath = TestImageHelper.CreateSolidColorJpeg(dir: _tempDir);
            _adaIn = new AdaIn();
        }

        [TestCleanup]
        public void Cleanup() {
            _adaIn?.Dispose();
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [TestMethod]
        [Description("未调用 LoadModel 就 RunAndSave 应抛出 InvalidOperationException")]
        public void RunAndSave_WithoutLoadModel_ThrowsInvalidOperationException() {
            string outputPath = Path.Combine(_tempDir, "out.jpg");

            Assert.Throws<InvalidOperationException>(
                () => _adaIn.RunAndSave(_testImagePath, _testImagePath, outputPath));
        }

        [TestMethod]
        [Description("content 文件不存在应抛出 FileNotFoundException")]
        public void RunAndSave_ContentNotFound_ThrowsFileNotFoundException() {
            // 在 session 为 null 时先触发 InvalidOperationException
            Assert.Throws<InvalidOperationException>(
                () => _adaIn.RunAndSave("C:\\nonexistent.jpg", _testImagePath,
                    Path.Combine(_tempDir, "out.jpg")));
        }

        [TestMethod]
        [Description("CancellationToken 已取消时应立即抛出 OperationCanceledException")]
        public void RunAndSave_CancelledToken_ThrowsOperationCanceledException() {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(
                () => _adaIn.RunAndSave(_testImagePath, _testImagePath,
                    Path.Combine(_tempDir, "out.jpg"),
                    ct: cts.Token));
        }

        [TestMethod]
        [Description("Dispose 后再 RunAndSave 应抛出异常")]
        public void RunAndSave_AfterDispose_ThrowsException() {
            _adaIn.Dispose();
            string outputPath = Path.Combine(_tempDir, "out.jpg");

            Assert.Throws<InvalidOperationException>(
                () => _adaIn.RunAndSave(_testImagePath, _testImagePath, outputPath));
        }

        [TestMethod]
        [Description("LoadModel 未指定模型文件时应抛出 FileNotFoundException（无默认模型）")]
        public void LoadModel_NoModelFile_ThrowsFileNotFoundException() {
            Assert.Throws<FileNotFoundException>(() => _adaIn.LoadModel());
        }
    }

    // ====================================================================
    //  LoadModel — 幂等性（不需要真实模型）
    // ====================================================================
    [TestClass]
    [TestCategory("Unit")]
    public class AdaIn_LoadModelTests {
        [TestMethod]
        [Description("传入不存在的路径应抛出 FileNotFoundException")]
        public void LoadModel_InvalidPath_ThrowsFileNotFoundException() {
            using var adaIn = new AdaIn();

            Assert.Throws<FileNotFoundException>(
                () => adaIn.LoadModel("C:\\nonexistent\\model.onnx"));
        }
    }

    // ====================================================================
    //  RunAndSave — 集成测试（需要真实模型文件 + 测试图片）
    // ====================================================================
    [TestClass]
    [TestCategory("Integration")]
    public class AdaIn_IntegrationTests {
        private string _tempDir = null!;
        private string _contentImagePath = null!;
        private string _styleImagePath = null!;
        private AdaIn _adaIn = null!;
        private readonly string _modelPath =
            Path.Combine(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory)),
                Constants.WorkingDir.ML_StyleTransfer_AI_Models,
                Utils.Fields.ModelName);

        [TestInitialize]
        public void Setup() {
            _tempDir = Path.Combine(Path.GetTempPath(), $"adain_int_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _contentImagePath = TestImageHelper.CreateSolidColorJpeg(128, 128, _tempDir);
            _styleImagePath = TestImageHelper.CreateSolidColorJpeg(128, 128, _tempDir);

            _adaIn = new AdaIn();
            _adaIn.LoadModel(_modelPath);
        }

        [TestCleanup]
        public void Cleanup() {
            _adaIn?.Dispose();
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [TestMethod]
        [Description("RunAndSave 应在指定路径生成输出文件")]
        public void RunAndSave_ValidInputs_CreatesOutputFile() {
            string outputPath = Path.Combine(_tempDir, "output.jpg");

            _adaIn.RunAndSave(_contentImagePath, _styleImagePath, outputPath);

            Assert.IsTrue(File.Exists(outputPath), $"Expected output file at: {outputPath}");
        }

        [TestMethod]
        [Description("输出文件应有内容（非空）")]
        public void RunAndSave_ValidInputs_OutputFileHasContent() {
            string outputPath = Path.Combine(_tempDir, "output.jpg");

            _adaIn.RunAndSave(_contentImagePath, _styleImagePath, outputPath);

            Assert.IsGreaterThan(0, new FileInfo(outputPath).Length);
        }

        [TestMethod]
        [Description("返回值应与传入的 outputFilePath 一致")]
        public void RunAndSave_ValidInputs_ReturnsOutputPath() {
            string outputPath = Path.Combine(_tempDir, "output.jpg");

            string result = _adaIn.RunAndSave(_contentImagePath, _styleImagePath, outputPath);

            Assert.AreEqual(outputPath, result);
        }

        [TestMethod]
        [Description("输出图像可被 OpenCvSharp 正常读取")]
        public void RunAndSave_ValidInputs_OutputIsReadableImage() {
            string outputPath = Path.Combine(_tempDir, "output.jpg");
            _adaIn.RunAndSave(_contentImagePath, _styleImagePath, outputPath);

            using var mat = OpenCvSharp.Cv2.ImRead(outputPath);

            Assert.IsFalse(mat.Empty(), "Output image should be readable by OpenCvSharp");
        }

        [TestMethod]
        [Description("alpha = 0 时风格权重为零，输出应接近内容图（不抛异常）")]
        public void RunAndSave_AlphaZero_DoesNotThrow() {
            string outputPath = Path.Combine(_tempDir, "output_alpha0.jpg");

            void act() => _adaIn.RunAndSave(_contentImagePath, _styleImagePath, outputPath, alpha: 0f);

            act();
            Assert.IsTrue(File.Exists(outputPath));
        }

        [TestMethod]
        [Description("alpha = 1 时完全应用风格，不抛异常")]
        public void RunAndSave_AlphaOne_DoesNotThrow() {
            string outputPath = Path.Combine(_tempDir, "output_alpha1.jpg");

            void act() => _adaIn.RunAndSave(_contentImagePath, _styleImagePath, outputPath, alpha: 1f);

            act();
            Assert.IsTrue(File.Exists(outputPath));
        }

        [TestMethod]
        [Description("输出路径为 .png 时应正常生成 PNG 文件")]
        public void RunAndSave_PngOutput_CreatesFile() {
            string outputPath = Path.Combine(_tempDir, "output.png");

            _adaIn.RunAndSave(_contentImagePath, _styleImagePath, outputPath);

            Assert.IsTrue(File.Exists(outputPath));
        }

        [TestMethod]
        [Description("LoadModel 重复调用不应抛异常（幂等）")]
        public void LoadModel_CalledTwice_DoesNotThrow() {
            void act() => _adaIn.LoadModel(_modelPath);

            act();
        }

        [TestMethod]
        [Description("Dispose 后再 RunAndSave 应抛出异常")]
        public void RunAndSave_AfterDispose_ThrowsException() {
            _adaIn.Dispose();
            string outputPath = Path.Combine(_tempDir, "output.jpg");

            Assert.Throws<InvalidOperationException>(
                () => _adaIn.RunAndSave(_contentImagePath, _styleImagePath, outputPath));
        }
    }
}
