using System.Diagnostics;
using OpenCvSharp;
using VirtualPaper.Common;
using VirtualPaper.ML.DepthEstimate;
using VirtualPaper.ML.DepthEstimate.Models;
using VirtualPaper.ML.Test.Infrastructure;

namespace VirtualPaper.ML.Test.T_DepthEstimate {
    [TestClass]
    [TestCategory("Unit")]
    public class DepthAnythingV2OptionsTests {
        [TestMethod]
        public void Validate_DefaultOptions_DoesNotThrow() {
            new DepthAnythingOptions().Validate();
        }

        [TestMethod]
        public void Validate_InputSizeNotMultipleOf14_Throws() {
            var options = new DepthAnythingOptions { InputSize = 512 };
            Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
        }

        [TestMethod]
        public void CalculateInputSize_FitLandscape_PreservesAspectAndLimitsWork() {
            (int width, int height) = DepthAnythingV2.CalculateInputSize(
                1920,
                1080,
                new DepthAnythingOptions {
                    ResizeMode = DepthAnythingResizeMode.FitLongestSide
                });

            Assert.AreEqual(518, width);
            Assert.AreEqual(294, height);
        }

        [TestMethod]
        public void CalculateInputSize_FillLandscape_MatchesLowerBoundStrategy() {
            (int width, int height) = DepthAnythingV2.CalculateInputSize(
                1920,
                1080,
                new DepthAnythingOptions {
                    ResizeMode = DepthAnythingResizeMode.FillShortestSide
                });

            Assert.AreEqual(924, width);
            Assert.AreEqual(518, height);
        }

        [TestMethod]
        public void CalculateInputSize_Stretch_ReturnsSquare() {
            (int width, int height) = DepthAnythingV2.CalculateInputSize(
                1920,
                1080,
                new DepthAnythingOptions {
                    ResizeMode = DepthAnythingResizeMode.StretchSquare
                });

            Assert.AreEqual(518, width);
            Assert.AreEqual(518, height);
        }

        [TestMethod]
        public void NormalizeOutput_MinMaxMapsToUnitRange() {
            float[] result = DepthAnythingV2.NormalizeOutput([2f, 4f, 6f]);
            CollectionAssert.AreEqual(new[] { 0f, 0.5f, 1f }, result);
        }

        [TestMethod]
        public void NormalizeOutput_ConstantInput_ReturnsZeros() {
            float[] result = DepthAnythingV2.NormalizeOutput([3f, 3f, 3f]);
            CollectionAssert.AreEqual(new[] { 0f, 0f, 0f }, result);
        }
    }

    [TestClass]
    [TestCategory("Unit")]
    public class DepthAnythingV2ExceptionTests {
        [TestMethod]
        public void LoadModel_MissingFile_Throws() {
            using var estimator = new DepthAnythingV2();
            Assert.Throws<FileNotFoundException>(() => estimator.LoadModel("missing.onnx"));
        }

        [TestMethod]
        public void Run_WithoutLoadModel_Throws() {
            string tempDir = Path.Combine(Path.GetTempPath(), $"depth_anything_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try {
                string imagePath = TestImageHelper.CreateSolidColorJpeg(32, 32, tempDir);
                using var estimator = new DepthAnythingV2();
                Assert.Throws<InvalidOperationException>(() => estimator.Run(imagePath));
            }
            finally {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [TestClass]
    [TestCategory("Integration")]
    public class DepthAnythingV2IntegrationTests {
        private static string? _skipReason;
        private string _tempDir = null!;
        private string _imagePath = null!;
        private DepthAnythingV2 _estimator = null!;

        public TestContext TestContext { get; set; } = null!;

        private static readonly string ModelPath = Path.Combine(
            Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),
            Constants.WorkingDir.ML_DepthEstimate_AI_Models,
            Utils.Fields.DepthAnythingV2ModelName);

        [ClassInitialize]
        public static void ClassSetup(TestContext _) {
            if (!File.Exists(ModelPath))
                _skipReason = $"Depth Anything V2 model not found: {ModelPath}";
        }

        [TestInitialize]
        public void Setup() {
            if (_skipReason is not null)
                Assert.Inconclusive(_skipReason);

            _tempDir = Path.Combine(Path.GetTempPath(), $"depth_anything_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _imagePath = TestImageHelper.CreateSolidColorJpeg(96, 64, _tempDir);
            _estimator = new DepthAnythingV2();
            _estimator.LoadModel(ModelPath);
        }

        [TestCleanup]
        public void Cleanup() {
            _estimator?.Dispose();
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [TestMethod]
        public void Run_ValidImage_ReturnsOriginalSizeDepthAndPng() {
            var stopwatch = Stopwatch.StartNew();
            DepthEstimateModelOutput output = _estimator.Run(_imagePath);
            stopwatch.Stop();

            Assert.AreEqual(96, output.Width);
            Assert.AreEqual(64, output.Height);
            Assert.AreEqual(96, output.OriginalWidth);
            Assert.AreEqual(64, output.OriginalHeight);
            Assert.HasCount(96 * 64, output.Depth);
            Assert.IsTrue(output.Depth.All(value => value >= 0f && value <= 1f));

            string depthPath = _estimator.SaveDepthMap(output, _tempDir);
            using var depthImage = Cv2.ImRead(depthPath, ImreadModes.Grayscale);
            Assert.AreEqual(96, depthImage.Width);
            Assert.AreEqual(64, depthImage.Height);

            TestContext.WriteLine($"Depth Anything V2 integration inference: {stopwatch.ElapsedMilliseconds} ms");
        }

        [TestMethod]
        public void LoadModel_CalledTwice_DoesNotThrow() {
            _estimator.LoadModel(ModelPath);
        }
    }
}
