using VirtualPaper.Common;
using VirtualPaper.ML.ObjectDetection;
using VirtualPaper.ML.ObjectDetection.Models;
using VirtualPaper.ML.Test.Infrastructure;

namespace VirtualPaper.ML.Test.T_ObjectDetection {
    [TestClass]
    [TestCategory("Unit")]
    public class ObjectDetectionOptionsTests {
        [TestMethod]
        public void Validate_DefaultOptions_DoesNotThrow() {
            var options = new ObjectDetectionOptions();
            options.Validate();
        }

        [TestMethod]
        public void Validate_NonStrideAlignedWidth_Throws() {
            var options = new ObjectDetectionOptions { InputWidth = 641 };
            Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
        }

        [TestMethod]
        public void Validate_InvalidScoreThreshold_Throws() {
            var options = new ObjectDetectionOptions { ScoreThreshold = 1.1f };
            Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
        }

        [TestMethod]
        public void CocoLabels_HasExpectedClassCount() {
            Assert.HasCount(80, Utils.CocoLabels);
            Assert.AreEqual("person", Utils.GetLabelName(0));
            Assert.AreEqual("toothbrush", Utils.GetLabelName(79));
        }
    }

    [TestClass]
    [TestCategory("Unit")]
    public class RTMDetExceptionTests {
        [TestMethod]
        public void Run_WithoutLoadModel_Throws() {
            using var detector = new RTMDet();
            Assert.Throws<InvalidOperationException>(() => detector.Run("missing.jpg"));
        }

        [TestMethod]
        public void LoadModel_MissingFile_Throws() {
            using var detector = new RTMDet();
            Assert.Throws<FileNotFoundException>(() => detector.LoadModel("missing.onnx"));
        }
    }

    [TestClass]
    [TestCategory("Integration")]
    public class RTMDetIntegrationTests {
        private static string? _skipReason;
        private string _tempDir = null!;
        private string _imagePath = null!;
        private RTMDet _detector = null!;

        private static readonly string ModelPath = Path.Combine(
            Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),
            Constants.WorkingDir.ML_ObjectDetection_AI_Models,
            Utils.Fields.ModelName);

        [ClassInitialize]
        public static void ClassSetup(TestContext _) {
            if (!File.Exists(ModelPath))
                _skipReason = $"RTMDet model not found, skipping integration tests: {ModelPath}";
        }

        [TestInitialize]
        public void Setup() {
            if (_skipReason is not null)
                Assert.Inconclusive(_skipReason);

            _tempDir = Path.Combine(Path.GetTempPath(), $"rtmdet_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _imagePath = TestImageHelper.CreateSolidColorJpeg(96, 64, _tempDir);
            _detector = new RTMDet();
            _detector.LoadModel(ModelPath);
        }

        [TestCleanup]
        public void Cleanup() {
            _detector?.Dispose();
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [TestMethod]
        public void Run_ValidImage_ReturnsMappedOutput() {
            var output = _detector.Run(
                _imagePath,
                new ObjectDetectionOptions {
                    InputWidth = 320,
                    InputHeight = 320,
                    ScoreThreshold = 0.25f
                });

            Assert.AreEqual(96, output.OriginalWidth);
            Assert.AreEqual(64, output.OriginalHeight);
            Assert.AreEqual(320, output.InputWidth);
            Assert.AreEqual(320, output.InputHeight);

            foreach (var detection in output.Detections) {
                Assert.IsGreaterThanOrEqualTo(0.25f, detection.Score);
                Assert.IsGreaterThanOrEqualTo(0f, detection.Left);
                Assert.IsLessThanOrEqualTo(output.OriginalWidth, detection.Right);
                Assert.IsGreaterThanOrEqualTo(0f, detection.Top);
                Assert.IsLessThanOrEqualTo(output.OriginalHeight, detection.Bottom);
                Assert.IsGreaterThan(0f, detection.Width);
                Assert.IsGreaterThan(0f, detection.Height);
            }
        }

        [TestMethod]
        public void LoadModel_CalledTwice_DoesNotThrow() {
            _detector.LoadModel(ModelPath);
        }
    }
}
