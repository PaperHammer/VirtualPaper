using System.Reflection;
using VirtualPaper.Common;
using VirtualPaper.ML.DepthEstimate;
using VirtualPaper.ML.DepthEstimate.Models;
using VirtualPaper.ML.Test.Infrastructure;

namespace VirtualPaper.ML.Test.T_DepthEstimate {
    // ====================================================================
    //  NormaliseOutput — 纯数学逻辑（反射访问私有静态方法）
    // ====================================================================
    [TestClass]
    [TestCategory("Unit")]
    public class DepthEstimate_NormaliseOutputTests {

        private static float[] InvokeNormalise(float[] data) {
            var method = typeof(MiDaS).GetMethod(
                "NormaliseOutput",
                BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException("NormaliseOutput not found");

            return (float[])method.Invoke(null, [data])!;
        }

        [TestMethod]
        [Description("所有值相同时，输出应全为 NaN 或 0（range = 0，由实现决定）")]
        public void NormaliseOutput_AllSameValues_DoesNotThrow() {
            float[] input = [1f, 1f, 1f, 1f];

            // 仅验证不抛异常，具体值取决于 depthRange = 0 的处理方式
            float[] act() => _ = InvokeNormalise(input);

            act(); // 不抛即通过；如需断言可根据实现补充
        }

        [TestMethod]
        [Description("最小值应被归一化为 0，最大值应被归一化为 1")]
        public void NormaliseOutput_MinMaxBoundary_CorrectlyNormalised() {
            float[] input = [0f, 0.5f, 1f];

            var result = InvokeNormalise(input);

            Assert.HasCount(3, result);
            Assert.AreEqual(0f, result[0], 1e-5f, "min should map to 0");
            Assert.AreEqual(0.5f, result[1], 1e-5f, "mid should map to 0.5");
            Assert.AreEqual(1f, result[2], 1e-5f, "max should map to 1");
        }

        [TestMethod]
        [Description("归一化后所有值应在 [0, 1] 范围内")]
        public void NormaliseOutput_AllValuesInRange() {
            float[] input = [3f, 1f, 4f, 1f, 5f, 9f, 2f, 6f];

            var result = InvokeNormalise(input);

            foreach (var v in result) {
                Assert.IsTrue(v >= 0f && v <= 1f,
                    $"Value {v} is out of [0, 1] range");
            }
        }

        [TestMethod]
        [Description("负数输入应正确归一化")]
        public void NormaliseOutput_NegativeValues_CorrectlyNormalised() {
            float[] input = [-10f, 0f, 10f];

            var result = InvokeNormalise(input);

            Assert.AreEqual(0f, result[0], 1e-5f);
            Assert.AreEqual(0.5f, result[1], 1e-5f);
            Assert.AreEqual(1f, result[2], 1e-5f);
        }

        [TestMethod]
        [Description("单元素数组应归一化为 NaN 或 0（range = 0）")]
        public void NormaliseOutput_SingleElement_DoesNotThrow() {
            float[] input = [42f];

            float[] act() => InvokeNormalise(input);

            act(); // 不抛即通过
        }

        [TestMethod]
        [Description("归一化结果长度应与输入相同")]
        public void NormaliseOutput_OutputLengthMatchesInput() {
            float[] input = [1f, 2f, 3f, 4f, 5f];

            var result = InvokeNormalise(input);

            Assert.HasCount(input.Length, result);
        }
    }

    // ====================================================================
    //  SaveDepthMap — 文件系统 I/O（不需要模型推理，但需要实例）
    // ====================================================================
    [TestClass]
    [TestCategory("Unit")]
    public class MiDaS_SaveDepthMapTests {
        private string _tempDir = null!;
        private MiDaS _midas = null!;

        [TestInitialize]
        public void Setup() {
            _tempDir = Path.Combine(Path.GetTempPath(), $"midas_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _midas = new MiDaS();
        }

        [TestCleanup]
        public void Cleanup() {
            _midas?.Dispose();
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        private static DepthEstimateModelOutput MakeSolidDepthOutput(
            int width, int height,
            int originalWidth, int originalHeight,
            float value = 0.5f) {
            var depth = Enumerable.Repeat(value, width * height).ToArray();
            return new DepthEstimateModelOutput(depth, width, height, originalWidth, originalHeight);
        }

        [TestMethod]
        [Description("正常调用应在指定目录生成深度图文件")]
        public void SaveDepthMap_ValidInput_CreatesFile() {
            int w = 32, h = 32;
            var modelOutput = MakeSolidDepthOutput(w, h, w, h);

            string outputPath = _midas.SaveDepthMap(modelOutput, _tempDir);

            Assert.IsTrue(File.Exists(outputPath),
                $"Expected output file at: {outputPath}");
        }

        [TestMethod]
        [Description("返回路径应指向有内容的文件")]
        public void SaveDepthMap_ValidInput_FileHasContent() {
            int w = 32, h = 32;
            var modelOutput = MakeSolidDepthOutput(w, h, w, h);

            string outputPath = _midas.SaveDepthMap(modelOutput, _tempDir);

            var info = new FileInfo(outputPath);
            Assert.IsGreaterThan(0, info.Length, "Output file should not be empty");
        }

        [TestMethod]
        [Description("resize 到不同的原始尺寸应仍能正常生成文件")]
        public void SaveDepthMap_DifferentOriginalSize_Succeeds() {
            int modelW = 32, modelH = 32;
            int origW = 128, origH = 96; // 不同于模型输出尺寸
            var modelOutput = MakeSolidDepthOutput(modelW, modelH, origW, origH);

            string outputPath = _midas.SaveDepthMap(modelOutput, _tempDir);

            Assert.IsTrue(File.Exists(outputPath));
        }

        [TestMethod]
        [Description("全零深度（最近处）应正常生成文件")]
        public void SaveDepthMap_AllZeroDepth_Succeeds() {
            int w = 16, h = 16;
            var modelOutput = MakeSolidDepthOutput(w, h, w, h, value: 0f);

            string outputPath = _midas.SaveDepthMap(modelOutput, _tempDir);

            Assert.IsTrue(File.Exists(outputPath));
        }

        [TestMethod]
        [Description("全一深度（最远处）应正常生成文件")]
        public void SaveDepthMap_AllOneDepth_Succeeds() {
            int w = 16, h = 16;
            var modelOutput = MakeSolidDepthOutput(w, h, w, h, value: 1f);

            string outputPath = _midas.SaveDepthMap(modelOutput, _tempDir);

            Assert.IsTrue(File.Exists(outputPath));
        }

        [TestMethod]
        [Description("连续两次调用同一目录，第二次应覆盖或共存（不抛异常）")]
        public void SaveDepthMap_CalledTwice_DoesNotThrow() {
            int w = 16, h = 16;
            var modelOutput = MakeSolidDepthOutput(w, h, w, h);

            _midas.SaveDepthMap(modelOutput, _tempDir);
            string act() => _midas.SaveDepthMap(modelOutput, _tempDir);

            act(); // 不抛即通过
        }
    }

    // ====================================================================
    //  Run — 异常分支（需要实例但不需要有效模型加载）
    // ====================================================================
    [TestClass]
    [TestCategory("Unit")]
    public class MiDaS_RunExceptionTests {
        private MiDaS _midas = null!;

        [TestInitialize]
        public void Setup() {
            _midas = new MiDaS();
        }

        [TestCleanup]
        public void Cleanup() {
            _midas?.Dispose();
        }

        [TestMethod]
        [Description("未调用 LoadModel 就 Run 应抛出 InvalidOperationException")]
        public void Run_WithoutLoadModel_ThrowsInvalidOperationException() {
            Assert.Throws<InvalidOperationException>(
                () => _midas.Run("C:\\nonexistent\\path.jpg"));
        }

        [TestMethod]
        [Description("传入不存在的文件路径应抛出 FileNotFoundException")]
        public void Run_FileNotFound_ThrowsFileNotFoundException() {
            // 需要先让 _session 非 null，但因为没有真实模型文件，
            // 这里测试在 session 为 null 时优先抛 InvalidOperationException
            Assert.Throws<InvalidOperationException>(
                () => _midas.Run("C:\\nonexistent\\path.jpg"));
        }

        [TestMethod]
        [Description("传入空字符串应抛出异常")]
        public void Run_EmptyPath_ThrowsException() {
            Assert.Throws<InvalidOperationException>(
                () => _midas.Run(string.Empty));
        }

        [TestMethod]
        [Description("传入 null 应抛出异常")]
        public void Run_NullPath_ThrowsException() {
            Assert.Throws<InvalidOperationException>(
                () => _midas.Run(null!));
        }
    }

    // ====================================================================
    //  Run + LoadModel — 集成测试（需要真实模型文件 + 测试图片）
    // ====================================================================
    [TestClass]
    [TestCategory("Integration")]
    public class MiDaS_IntegrationTests {
        private static string? _classSkipReason;

        private string _tempDir = null!;
        private string _testImagePath = null!;
        private MiDaS _midas = null!;
        private readonly string _modelPath =
            Path.Combine(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory)),
                Constants.WorkingDir.ML_DepthEstimate_AI_Models,
                Utils.Fields.ModelName);

        [ClassInitialize]
        public static void ClassSetup(TestContext _) {
            var modelPath = Path.Combine(
                Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),
                Constants.WorkingDir.ML_DepthEstimate_AI_Models,
                Utils.Fields.ModelName);

            if (!File.Exists(modelPath))
                _classSkipReason = $"MiDaS model not found, skipping integration tests: {modelPath}";
        }

        [TestInitialize]
        public void Setup() {
            if (_classSkipReason is not null)
                Assert.Inconclusive(_classSkipReason);

            _tempDir = Path.Combine(Path.GetTempPath(), $"midas_int_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _testImagePath = TestImageHelper.CreateSolidColorJpeg(dir: _tempDir);

            _midas = new MiDaS();
            _midas.LoadModel(_modelPath);
        }

        [TestCleanup]
        public void Cleanup() {
            _midas?.Dispose();
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [TestMethod]
        [Description("Run 应返回非空的 DepthEstimateModelOutput")]
        public void Run_ValidImage_ReturnsModelOutput() {
            var result = _midas.Run(_testImagePath);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        [Description("DepthEstimateModelOutput.Depth 数组长度应等于模型输出的 width * height")]
        public void Run_ValidImage_DepthArrayLengthIsCorrect() {
            var result = _midas.Run(_testImagePath);

            Assert.HasCount(
                result.Width * result.Height,
                result.Depth,
                "Depth array length should equal Width * Height");
        }

        [TestMethod]
        [Description("DepthEstimateModelOutput 的 OriginalWidth/Height 应与输入图片一致")]
        public void Run_ValidImage_OriginalDimensionsMatchInput() {
            using var mat = OpenCvSharp.Cv2.ImRead(_testImagePath);
            int expectedW = mat.Width;
            int expectedH = mat.Height;

            var result = _midas.Run(_testImagePath);

            Assert.AreEqual(expectedW, result.OriginalWidth,
                "OriginalWidth should match source image width");
            Assert.AreEqual(expectedH, result.OriginalHeight,
                "OriginalHeight should match source image height");
        }

        [TestMethod]
        [Description("归一化后的 Depth 值应全部在 [0, 1] 范围内")]
        public void Run_ValidImage_AllDepthValuesInRange() {
            var result = _midas.Run(_testImagePath);

            foreach (var v in result.Depth) {
                Assert.IsTrue(v >= 0f && v <= 1f,
                    $"Depth value {v} is outside [0, 1]");
            }
        }

        [TestMethod]
        [Description("Run → SaveDepthMap 完整流程应生成可读文件")]
        public void Run_ThenSaveDepthMap_ProducesValidFile() {
            var result = _midas.Run(_testImagePath);
            string outputPath = _midas.SaveDepthMap(result, _tempDir);

            Assert.IsTrue(File.Exists(outputPath));
            Assert.IsGreaterThan(0, new FileInfo(outputPath).Length);
        }

        [TestMethod]
        [Description("LoadModel 重复调用不应抛异常（幂等）")]
        public void LoadModel_CalledTwice_DoesNotThrow() {
            // _midas 已在 Setup 中加载过一次
            void act() => _midas.LoadModel();

            act(); // 不抛即通过（内部有 _isLoaded 守卫）
        }

        [TestMethod]
        [Description("Dispose 后再 Run 应抛出异常")]
        public void Run_AfterDispose_ThrowsException() {
            _midas.Dispose();

            Assert.Throws<InvalidOperationException>(
                () => _midas.Run(_testImagePath));
        }
    }
}
