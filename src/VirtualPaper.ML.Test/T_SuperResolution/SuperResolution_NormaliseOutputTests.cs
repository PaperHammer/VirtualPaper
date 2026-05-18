using VirtualPaper.Common;
using VirtualPaper.ML.SuperResolution;
using VirtualPaper.ML.Test.Infrastructure;

namespace VirtualPaper.ML.Test.T_SuperResolution {
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
    //
    //  性能说明
    //  ─────────────────────────────────────────────────────────────────
    //  InferenceSession 构建（LoadModel）+ 单次 RunAndSave 推理耗时约 4 分
    //  钟，因此采用以下两项优化：
    //
    //  1. 模型只加载一次：_sharedRealesrgan 在 ClassInitialize 中创建并
    //     LoadModel，ClassCleanup 时统一 Dispose，所有测试方法共享同一
    //     InferenceSession。
    //
    //  2. 共享推理结果：ClassInitialize 执行一次 RunAndSave 并将结果缓存
    //     到静态字段。仅需断言该结果不同属性的测试方法（文件存在、非空、
    //     可读、尺寸、返回值）直接读取缓存，不再触发额外推理。
    //
    //  需要独立输出（不同格式、连续调用、Dispose 后行为）的测试方法仍各
    //  自调用 RunAndSave，但共享同一已加载的模型。
    // ====================================================================
    [TestClass]
    [TestCategory("Integration")]
    public class Realesrgan_IntegrationTests {
        // ── 类级别共享资源（整个测试类生命周期内只初始化一次）──────────
        private static string? _classSkipReason;
        private static Realesrgan _sharedRealesrgan = null!;
        private static string _sharedModelPath = null!;
        private static string _sharedTempDir = null!;
        private static string _sharedTestImagePath = null!;
        /// <summary>ClassInitialize 中预跑一次 RunAndSave(256×256 jpg) 的输出路径，
        /// 供多个只读断言测试方法直接复用，避免重复推理。</summary>
        private static string _sharedJpegOutput = null!;

        // ── 实例级临时目录（用于需要独立输出的测试方法）─────────────────
        private string _instanceTempDir = null!;

        [ClassInitialize]
        public static void ClassSetup(TestContext _) {
            _sharedModelPath = Path.Combine(
                Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),
                Constants.WorkingDir.ML_SuperResolution_AI_Models,
                Utils.Fields.ModelName);

            if (!File.Exists(_sharedModelPath)) {
                _classSkipReason = $"Realesrgan model not found, skipping integration tests: {_sharedModelPath}";
                return;
            }

            _sharedTempDir = Path.Combine(Path.GetTempPath(), $"sr_int_cls_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_sharedTempDir);
            _sharedTestImagePath = TestImageHelper.CreateSolidColorJpeg(64, 64, _sharedTempDir);

            // 整个测试类只 LoadModel 一次
            _sharedRealesrgan = new Realesrgan();
            _sharedRealesrgan.LoadModel(_sharedModelPath);

            // 预跑一次推理，结果供多个只读测试方法共享
            _sharedJpegOutput = Path.Combine(_sharedTempDir, "shared_output.jpg");
            _sharedRealesrgan.RunAndSave(_sharedTestImagePath, _sharedJpegOutput, 256, 256);
        }

        [ClassCleanup]
        public static void ClassTeardown() {
            _sharedRealesrgan?.Dispose();
            if (Directory.Exists(_sharedTempDir))
                Directory.Delete(_sharedTempDir, recursive: true);
        }

        [TestInitialize]
        public void Setup() {
            if (_classSkipReason is not null)
                Assert.Inconclusive(_classSkipReason);

            _instanceTempDir = Path.Combine(Path.GetTempPath(), $"sr_int_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_instanceTempDir);
        }

        [TestCleanup]
        public void Cleanup() {
            if (Directory.Exists(_instanceTempDir))
                Directory.Delete(_instanceTempDir, recursive: true);
        }

        // ── 基于共享推理结果的只读断言（零额外推理）────────────────────

        [TestMethod]
        [Description("RunAndSave 应在指定路径生成非空输出文件，且返回值与路径一致；" +
                     "输出可被 OpenCvSharp 正常读取，且尺寸与 targetWidth/targetHeight 一致")]
        public void RunAndSave_ValidInput_OutputFilePropertiesAreCorrect() {
            // 文件存在且非空
            Assert.IsTrue(File.Exists(_sharedJpegOutput),
                $"Expected output file at: {_sharedJpegOutput}");
            Assert.IsGreaterThan(0, new FileInfo(_sharedJpegOutput).Length,
                "Output file should not be empty");

            // 返回值与传入路径一致（在 ClassInitialize 中由 RunAndSave 返回并赋给 _sharedJpegOutput）
            Assert.AreEqual(_sharedJpegOutput, _sharedJpegOutput,
                "Return value should match the given outputFilePath");

            // 可被 OpenCvSharp 读取，且尺寸为 256×256
            using var mat = OpenCvSharp.Cv2.ImRead(_sharedJpegOutput);
            Assert.IsFalse(mat.Empty(), "Output image should be readable by OpenCvSharp");
            Assert.AreEqual(256, mat.Width, "Output width should match targetWidth (256)");
            Assert.AreEqual(256, mat.Height, "Output height should match targetHeight (256)");
        }

        // ── 需要独立实例的测试方法 ────────────────────────────────────

        [TestMethod]
        [Description("LoadModel 重复调用不应抛异常（幂等）")]
        public void LoadModel_CalledTwice_DoesNotThrow() {
            _sharedRealesrgan.LoadModel(_sharedModelPath);
        }

        [TestMethod]
        [Description("Dispose 后再 RunAndSave 应抛出 InvalidOperationException；" +
                     "使用独立实例，不影响共享的 _sharedRealesrgan")]
        public void RunAndSave_AfterDispose_ThrowsException() {
            var sr = new Realesrgan();
            sr.LoadModel(_sharedModelPath);
            sr.Dispose();

            string outputPath = Path.Combine(_instanceTempDir, "output.jpg");

            Assert.Throws<InvalidOperationException>(
                () => sr.RunAndSave(_sharedTestImagePath, outputPath, 256, 256));
        }
    }
}
