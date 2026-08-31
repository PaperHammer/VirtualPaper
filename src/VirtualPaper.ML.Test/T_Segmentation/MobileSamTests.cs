using VirtualPaper.Common;
using OpenCvSharp;
using VirtualPaper.ML.Segmentation;
using VirtualPaper.ML.Segmentation.Models;
using VirtualPaper.ML.Test.Infrastructure;

namespace VirtualPaper.ML.Test.T_Segmentation {
    [TestClass]
    [TestCategory("Unit")]
    public class MobileSamOptionsTests {
        [TestMethod]
        public void Validate_DefaultOptions_DoesNotThrow() {
            new MobileSamOptions().Validate();
        }

        [TestMethod]
        public void Validate_NonFiniteThreshold_Throws() {
            var options = new MobileSamOptions { MaskThreshold = float.NaN };
            Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
        }

        [TestMethod]
        public void Validate_NonPositiveMaxBoxes_Throws() {
            var options = new MobileSamOptions { MaxBoxes = 0 };
            Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
        }

        [TestMethod]
        public void GetPreprocessSize_Landscape_UsesLongestSide() {
            (int width, int height) = MobileSamPreprocessor.GetPreprocessSize(1920, 1080);
            Assert.AreEqual(1024, width);
            Assert.AreEqual(576, height);
        }

        [TestMethod]
        public void GetPreprocessSize_Portrait_UsesSamRounding() {
            (int width, int height) = MobileSamPreprocessor.GetPreprocessSize(1000, 1500);
            Assert.AreEqual(683, width);
            Assert.AreEqual(1024, height);
        }

        [TestMethod]
        public void ClampAndValidate_PartiallyOutsideImage_ClipsBox() {
            var box = new SegmentationBox(-10, 5, 120, 90);
            SegmentationBox result = box.ClampAndValidate(100, 80);

            Assert.AreEqual(0f, result.Left);
            Assert.AreEqual(5f, result.Top);
            Assert.AreEqual(100f, result.Right);
            Assert.AreEqual(80f, result.Bottom);
        }

        [TestMethod]
        public void ClampAndValidate_EmptyBox_Throws() {
            var box = new SegmentationBox(10, 10, 10, 20);
            Assert.Throws<ArgumentOutOfRangeException>(() => box.ClampAndValidate(100, 80));
        }
    }

    [TestClass]
    [TestCategory("Unit")]
    public class SegmentationVisualizationTests {
        private string _tempDir = null!;
        private string _imagePath = null!;
        private SegmentationMask _mask = null!;

        [TestInitialize]
        public void Setup() {
            _tempDir = Path.Combine(Path.GetTempPath(), $"segmentation_visual_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _imagePath = TestImageHelper.CreateSolidColorJpeg(8, 6, _tempDir);

            var alpha = new byte[8 * 6];
            for (int y = 1; y < 5; y++) {
                for (int x = 2; x < 7; x++)
                    alpha[y * 8 + x] = byte.MaxValue;
            }

            _mask = new SegmentationMask(
                new SegmentationBox(2, 1, 7, 5),
                0.9f,
                alpha,
                8,
                6);
        }

        [TestCleanup]
        public void Cleanup() {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [TestMethod]
        public void SaveAlphaMask_WritesSingleChannelPng() {
            string outputPath = Path.Combine(_tempDir, "mask.png");
            SegmentationVisualization.SaveAlphaMask(_mask, outputPath);

            using var saved = Cv2.ImRead(outputPath, ImreadModes.Grayscale);
            Assert.AreEqual(8, saved.Width);
            Assert.AreEqual(6, saved.Height);
            Assert.AreEqual(byte.MaxValue, saved.At<byte>(2, 3));
            Assert.AreEqual(byte.MinValue, saved.At<byte>(0, 0));
        }

        [TestMethod]
        public void SaveTransparentCutout_PreservesAlphaChannel() {
            string outputPath = Path.Combine(_tempDir, "cutout.png");
            SegmentationVisualization.SaveTransparentCutout(_imagePath, _mask, outputPath);

            using var saved = Cv2.ImRead(outputPath, ImreadModes.Unchanged);
            Assert.AreEqual(4, saved.Channels());
            Assert.AreEqual(byte.MaxValue, saved.At<Vec4b>(2, 3).Item3);
            Assert.AreEqual(byte.MinValue, saved.At<Vec4b>(0, 0).Item3);
        }

        [TestMethod]
        public void SaveContourPreview_WritesOriginalSizeImage() {
            string outputPath = Path.Combine(_tempDir, "preview.png");
            SegmentationVisualization.SaveContourPreview(_imagePath, _mask, outputPath);

            using var saved = Cv2.ImRead(outputPath, ImreadModes.Color);
            Assert.AreEqual(8, saved.Width);
            Assert.AreEqual(6, saved.Height);
            Assert.AreEqual(3, saved.Channels());
        }
    }

    [TestClass]
    [TestCategory("Unit")]
    public class MobileSamExceptionTests {
        [TestMethod]
        public void Run_WithoutLoadModels_Throws() {
            string tempDir = Path.Combine(Path.GetTempPath(), $"mobile_sam_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try {
                string imagePath = TestImageHelper.CreateSolidColorJpeg(32, 32, tempDir);
                using var segmenter = new MobileSam();
                Assert.Throws<InvalidOperationException>(() => segmenter.Run(
                    imagePath,
                    [new SegmentationBox(0, 0, 32, 32)]));
            }
            finally {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [TestMethod]
        public void LoadModels_MissingEncoder_Throws() {
            using var segmenter = new MobileSam();
            Assert.Throws<FileNotFoundException>(() => segmenter.LoadModels(
                "missing_encoder.onnx",
                "missing_decoder.onnx"));
        }
    }

    [TestClass]
    [TestCategory("Integration")]
    public class MobileSamIntegrationTests {
        private static string? _skipReason;
        private string _tempDir = null!;
        private string _imagePath = null!;
        private MobileSam _segmenter = null!;

        private static readonly string EncoderPath = Path.Combine(
            Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),
            Constants.WorkingDir.ML,
            "Segmentation",
            "ai_models",
            Utils.Fields.EncoderModelName);

        private static readonly string DecoderPath = Path.Combine(
            Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),
            Constants.WorkingDir.ML,
            "Segmentation",
            "ai_models",
            Utils.Fields.DecoderModelName);

        [ClassInitialize]
        public static void ClassSetup(TestContext _) {
            if (!File.Exists(EncoderPath) || !File.Exists(DecoderPath)) {
                _skipReason =
                    $"MobileSAM models not found, skipping integration tests: " +
                    $"{EncoderPath}; {DecoderPath}";
            }
        }

        [TestInitialize]
        public void Setup() {
            if (_skipReason is not null)
                Assert.Inconclusive(_skipReason);

            _tempDir = Path.Combine(Path.GetTempPath(), $"mobile_sam_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _imagePath = TestImageHelper.CreateSolidColorJpeg(96, 64, _tempDir);
            _segmenter = new MobileSam();
            _segmenter.LoadModels(EncoderPath, DecoderPath);
        }

        [TestCleanup]
        public void Cleanup() {
            _segmenter?.Dispose();
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [TestMethod]
        public void Run_BoxPrompt_ReturnsOriginalSizeAlphaMask() {
            SegmentationModelOutput output = _segmenter.Run(
                _imagePath,
                [new SegmentationBox(8, 8, 88, 56)]);

            Assert.AreEqual(96, output.OriginalWidth);
            Assert.AreEqual(64, output.OriginalHeight);
            Assert.AreEqual(1024, output.EncoderInputSize);
            Assert.HasCount(1, output.Masks);

            SegmentationMask mask = output.Masks[0];
            Assert.AreEqual(96, mask.Width);
            Assert.AreEqual(64, mask.Height);
            Assert.HasCount(96 * 64, mask.Alpha);
            Assert.IsTrue(float.IsFinite(mask.PredictedIoU));
            Assert.IsTrue(mask.Alpha.All(value => value is byte.MinValue or byte.MaxValue));
        }

        [TestMethod]
        public void LoadModels_CalledTwice_DoesNotThrow() {
            _segmenter.LoadModels(EncoderPath, DecoderPath);
        }
    }
}
