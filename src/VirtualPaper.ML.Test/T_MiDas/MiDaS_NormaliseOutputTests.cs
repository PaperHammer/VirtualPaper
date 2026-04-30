using System.Reflection;
using VirtualPaper.ML.DepthEstimate;

namespace VirtualPaper.ML.Test.T_MiDas {
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
    //  NormaliseOutput — 纯数学逻辑（反射访问私有静态方法）
    // ====================================================================
    [TestClass]
    [TestCategory("Unit")]
    public class MiDaS_NormaliseOutputTests {

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
            float[] result = null!;
            var act = () => result = InvokeNormalise(input);

            act(); // 不抛即通过；如需断言可根据实现补充
        }

        [TestMethod]
        [Description("最小值应被归一化为 0，最大值应被归一化为 1")]
        public void NormaliseOutput_MinMaxBoundary_CorrectlyNormalised() {
            float[] input = [0f, 0.5f, 1f];

            var result = InvokeNormalise(input);

            Assert.AreEqual(3, result.Length);
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

            var act = () => InvokeNormalise(input);

            act(); // 不抛即通过
        }

        [TestMethod]
        [Description("归一化结果长度应与输入相同")]
        public void NormaliseOutput_OutputLengthMatchesInput() {
            float[] input = [1f, 2f, 3f, 4f, 5f];

            var result = InvokeNormalise(input);

            Assert.AreEqual(input.Length, result.Length);
        }
    }

    // ====================================================================
    //  SaveDepthMap — 文件系统 I/O（不需要模型）
    // ====================================================================
    [TestClass]
    [TestCategory("Unit")]
    public class MiDaS_SaveDepthMapTests {
        private string _tempDir = null!;

        [TestInitialize]
        public void Setup() {
            _tempDir = Path.Combine(Path.GetTempPath(), $"midas_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        [TestCleanup]
        public void Cleanup() {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        private static float[] MakeSolidDepth(int width, int height, float value = 0.5f)
            => Enumerable.Repeat(value, width * height).ToArray();

        [TestMethod]
        [Description("正常调用应在指定目录生成深度图文件")]
        public void SaveDepthMap_ValidInput_CreatesFile() {
            int w = 32, h = 32;
            var depth = MakeSolidDepth(w, h);

            string outputPath = MiDaS.SaveDepthMap(depth, w, h, w, h, _tempDir);

            Assert.IsTrue(File.Exists(outputPath),
                $"Expected output file at: {outputPath}");
        }

        [TestMethod]
        [Description("返回路径应指向有内容的文件")]
        public void SaveDepthMap_ValidInput_FileHasContent() {
            int w = 32, h = 32;
            var depth = MakeSolidDepth(w, h);

            string outputPath = MiDaS.SaveDepthMap(depth, w, h, w, h, _tempDir);

            var info = new FileInfo(outputPath);
            Assert.IsGreaterThan(0, info.Length, "Output file should not be empty");
        }

        [TestMethod]
        [Description("resize 到不同的原始尺寸应仍能正常生成文件")]
        public void SaveDepthMap_DifferentOriginalSize_Succeeds() {
            int modelW = 32, modelH = 32;
            int origW = 128, origH = 96; // 不同于模型输出尺寸
            var depth = MakeSolidDepth(modelW, modelH);

            string outputPath = MiDaS.SaveDepthMap(depth, modelW, modelH, origW, origH, _tempDir);

            Assert.IsTrue(File.Exists(outputPath));
        }

        [TestMethod]
        [Description("全零深度（最近处）应正常生成文件")]
        public void SaveDepthMap_AllZeroDepth_Succeeds() {
            int w = 16, h = 16;
            var depth = MakeSolidDepth(w, h, value: 0f);

            string outputPath = MiDaS.SaveDepthMap(depth, w, h, w, h, _tempDir);

            Assert.IsTrue(File.Exists(outputPath));
        }

        [TestMethod]
        [Description("全一深度（最远处）应正常生成文件")]
        public void SaveDepthMap_AllOneDepth_Succeeds() {
            int w = 16, h = 16;
            var depth = MakeSolidDepth(w, h, value: 1f);

            string outputPath = MiDaS.SaveDepthMap(depth, w, h, w, h, _tempDir);

            Assert.IsTrue(File.Exists(outputPath));
        }

        [TestMethod]
        [Description("连续两次调用同一目录，第二次应覆盖或共存（不抛异常）")]
        public void SaveDepthMap_CalledTwice_DoesNotThrow() {
            int w = 16, h = 16;
            var depth = MakeSolidDepth(w, h);

            MiDaS.SaveDepthMap(depth, w, h, w, h, _tempDir);
            var act = () => MiDaS.SaveDepthMap(depth, w, h, w, h, _tempDir);

            act(); // 不抛即通过
        }
    }

    // ====================================================================
    //  Run — 异常分支（不触发模型推理）
    // ====================================================================
    [TestClass]
    [TestCategory("Unit")]
    public class MiDaS_RunExceptionTests {

        [TestMethod]
        [Description("传入不存在的文件路径应抛出 FileNotFoundException")]
        public void Run_FileNotFound_ThrowsFileNotFoundException() {
            Assert.Throws<FileNotFoundException>(
                () => MiDaS.Run("C:\\nonexistent\\path.jpg"));
        }

        [TestMethod]
        [Description("传入空字符串应抛出 FileNotFoundException（modelPath guard）")]
        public void Run_EmptyPath_ThrowsFileNotFoundException() {
            Assert.Throws<FileNotFoundException>(
                () => MiDaS.Run(string.Empty));
        }

        [TestMethod]
        [Description("传入 null 应抛出 ArgumentNullException 或 FileNotFoundException")]
        public void Run_NullPath_ThrowsArgumentException() {
            Assert.Throws<Exception>(() => MiDaS.Run(null!));
        }
    }

    // ====================================================================
    //  Run + LoadModel — 集成测试（需要真实模型文件 + 测试图片）
    // ====================================================================
    [TestClass]
    [TestCategory("Integration")]
    public class MiDaS_IntegrationTests {
        private string _tempDir = null!;
        private string _testImagePath = null!;

        [TestInitialize]
        public void Setup() {
            _tempDir = Path.Combine(Path.GetTempPath(), $"midas_int_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _testImagePath = TestImageHelper.CreateSolidColorJpeg(dir: _tempDir);
        }

        [TestCleanup]
        public void Cleanup() {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [TestMethod]
        [Description("Run 应返回非空的 ModelOutput")]
        public void Run_ValidImage_ReturnsModelOutput() {
            var result = MiDaS.Run(_testImagePath);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        [Description("ModelOutput.Depth 数组长度应等于模型输出的 width × height")]
        public void Run_ValidImage_DepthArrayLengthIsCorrect() {
            var result = MiDaS.Run(_testImagePath);

            Assert.AreEqual(
                result.Width * result.Height,
                result.Depth.Length,
                "Depth array length should equal Width * Height");
        }

        [TestMethod]
        [Description("ModelOutput 的 OriginalWidth/Height 应与输入图片一致")]
        public void Run_ValidImage_OriginalDimensionsMatchInput() {
            using var mat = OpenCvSharp.Cv2.ImRead(_testImagePath);
            int expectedW = mat.Width;
            int expectedH = mat.Height;

            var result = MiDaS.Run(_testImagePath);

            Assert.AreEqual(expectedW, result.OriginalWidth,
                "OriginalWidth should match source image width");
            Assert.AreEqual(expectedH, result.OriginalHeight,
                "OriginalHeight should match source image height");
        }

        [TestMethod]
        [Description("归一化后的 Depth 值应全部在 [0, 1] 范围内")]
        public void Run_ValidImage_AllDepthValuesInRange() {
            var result = MiDaS.Run(_testImagePath);

            foreach (var v in result.Depth) {
                Assert.IsTrue(v >= 0f && v <= 1f,
                    $"Depth value {v} is outside [0, 1]");
            }
        }

        [TestMethod]
        [Description("Run → SaveDepthMap 完整流程应生成可读文件")]
        public void Run_ThenSaveDepthMap_ProducesValidFile() {
            var result = MiDaS.Run(_testImagePath);
            string outputPath = MiDaS.SaveDepthMap(
                result.Depth,
                result.Width,
                result.Height,
                result.OriginalWidth,
                result.OriginalHeight,
                _tempDir);

            Assert.IsTrue(File.Exists(outputPath));
            Assert.IsGreaterThan(0, new FileInfo(outputPath).Length);

        }
    }
}
