using System.IO.Compression;
using System.Text.Json;
using OpenCvSharp;
using VirtualPaper.ML.DepthEstimate.Models;
using VirtualPaper.ML.DynamicImage;
using VirtualPaper.ML.DynamicImage.Models;
using VirtualPaper.ML.Inpainting.Models;
using VirtualPaper.ML.ObjectDetection.Models;
using VirtualPaper.ML.Segmentation.Models;
using VirtualPaper.ML.Test.Infrastructure;

namespace VirtualPaper.ML.Test.T_DynamicImage {
    [TestClass]
    [TestCategory("Unit")]
    public class DynamicImageExporterTests {
        private string _directory = null!;

        [TestInitialize]
        public void Setup() {
            _directory = Path.Combine(Path.GetTempPath(), $"dynamic_export_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
        }

        [TestCleanup]
        public void Cleanup() {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        [TestMethod]
        public void Export_WritesPngLayersAndCompactManifest() {
            const int width = 4;
            const int height = 3;
            string imagePath = TestImageHelper.CreateSolidColorJpeg(width, height, _directory);
            float[] values = [
                0.1f, 0.1f, 0.2f, 0.2f,
                0.4f, 0.5f, 0.5f, 0.6f,
                0.8f, 0.8f, 0.9f, 0.9f
            ];
            var depth = new DepthEstimateModelOutput(values, width, height, width, height);
            var detection = new DetectedObject(0, "person", 0.9f, 1, 1, 3, 3);
            byte[] alpha = [
                0, 0, 0, 0,
                0, 255, 255, 0,
                0, 255, 255, 0
            ];
            var mask = new SegmentationMask(
                new SegmentationBox(1, 1, 3, 3),
                0.95f,
                alpha,
                width,
                height);
            DynamicImageLayerPlan plan = LayerFusionEngine.Fuse(
                depth,
                [detection],
                [mask],
                new LayerFusionOptions {
                    MinimumObjectAreaRatio = 0f,
                    MinimumSegmentationIoU = 0f,
                    DepthSmoothingDiameter = 0
                });
            var analysis = new DynamicImageAnalysisResult(
                new ObjectDetectionModelOutput(
                    [detection], width, height, 640, 640, width, height, 1, 1),
                [detection],
                new SegmentationModelOutput(
                    [mask], width, height, 1024, width, height, 1, 1),
                depth,
                plan,
                new InpaintingModelOutput(new byte[width * height * 3], width, height, true) {
                    AppliedMask = plan.InpaintingMask.ToArray(),
                    SafetyMarginPixels = 8
                },
                new DynamicImageAnalysisTiming(
                    TimeSpan.FromMilliseconds(1),
                    TimeSpan.FromMilliseconds(2),
                    TimeSpan.FromMilliseconds(3),
                    TimeSpan.FromMilliseconds(4),
                    TimeSpan.FromMilliseconds(5)));

            DynamicImageExportResult exported = DynamicImageExporter.Export(
                imagePath,
                analysis,
                Path.Combine(_directory, "output"));

            Assert.IsTrue(File.Exists(exported.ManifestPath));
            Assert.IsTrue(File.Exists(exported.DepthMapPath));
            Assert.IsTrue(File.Exists(exported.RawDepthMapPath));
            Assert.IsTrue(File.Exists(exported.BackgroundPlatePath));
            Assert.IsTrue(File.Exists(exported.BackgroundDepthMapPath));
            Assert.IsTrue(File.Exists(exported.AppliedInpaintingMaskPath));
            Assert.IsTrue(File.Exists(exported.MotionConfigurationPath));
            Assert.IsTrue(File.Exists(exported.WebPackagePath));
            Assert.AreEqual(8, exported.InpaintingSafetyMarginPixels);
            Assert.IsTrue(File.Exists(exported.InpaintingMaskPath));
            Assert.IsTrue(File.Exists(exported.LayerOrderPreviewPath));
            Assert.HasCount(plan.Layers.Count, exported.Layers);
            Assert.IsTrue(exported.Layers.All(item =>
                File.Exists(item.SourceAlphaPath) &&
                File.Exists(item.VisibleAlphaPath) &&
                File.Exists(item.SourceCutoutPath) &&
                File.Exists(item.VisibleCutoutPath)));

            DynamicImageLayerExport objectExport = exported.Layers.Single(item => item.LayerId.Contains("person"));
            using var cutout = Cv2.ImRead(objectExport.SourceCutoutPath, ImreadModes.Unchanged);
            Assert.AreEqual(width, cutout.Width);
            Assert.AreEqual(height, cutout.Height);
            Assert.AreEqual(4, cutout.Channels());
            Assert.AreEqual(byte.MinValue, cutout.At<Vec4b>(0, 0).Item3);
            Assert.AreEqual(new Vec4b(0, 0, 0, 0), cutout.At<Vec4b>(0, 0));
            Assert.AreEqual(byte.MaxValue, cutout.At<Vec4b>(1, 1).Item3);
            Assert.AreEqual(objectExport.SourceAlphaPath, objectExport.VisibleAlphaPath);
            Assert.AreEqual(objectExport.SourceCutoutPath, objectExport.VisibleCutoutPath);

            string json = File.ReadAllText(exported.ManifestPath);
            using JsonDocument document = JsonDocument.Parse(json);
            Assert.AreEqual(width, document.RootElement.GetProperty("width").GetInt32());
            Assert.AreEqual(
                plan.Layers.Count,
                document.RootElement.GetProperty("layers").GetArrayLength());
            Assert.AreEqual(
                plan.SceneKind.ToString(),
                document.RootElement.GetProperty("sceneKind").GetString());
            Assert.AreEqual(
                plan.Subjects.Count,
                document.RootElement.GetProperty("subjects").GetArrayLength());
            Assert.AreEqual(
                "depth.png",
                document.RootElement.GetProperty("depthMap").GetString());
            Assert.AreEqual(
                "depth_raw.png",
                document.RootElement.GetProperty("rawDepthMap").GetString());
            Assert.AreEqual(
                "background_plate.png",
                document.RootElement.GetProperty("backgroundPlate").GetString());
            Assert.AreEqual(
                "background_depth.png",
                document.RootElement.GetProperty("backgroundDepthMap").GetString());
            Assert.AreEqual(
                "inpainting_mask_applied.png",
                document.RootElement.GetProperty("appliedInpaintingMask").GetString());
            Assert.AreEqual(
                8,
                document.RootElement.GetProperty("inpaintingSafetyMarginPixels").GetInt32());
            Assert.AreEqual(
                5d,
                document.RootElement.GetProperty("timingMilliseconds")
                    .GetProperty("backgroundInpainting").GetDouble());
            Assert.IsTrue(
                document.RootElement.GetProperty("requiresBackgroundReconstruction").GetBoolean());
            Assert.IsLessThan(20_000, json.Length);

            using (ZipArchive package = ZipFile.OpenRead(exported.WebPackagePath)) {
                string[] requiredEntries = [
                    "index.html",
                    "style.css",
                    "player.js",
                    "motion.json",
                    "project.json",
                    "assets/background.png",
                    "assets/background_depth.png",
                    "assets/preview.jpg",
                    "assets/objects/object_00.png"
                ];
                foreach (string entry in requiredEntries)
                    Assert.IsNotNull(package.GetEntry(entry), $"Missing package entry: {entry}");

                using Stream motionStream = package.GetEntry("motion.json")!.Open();
                using JsonDocument motion = JsonDocument.Parse(motionStream);
                Assert.AreEqual(4, motion.RootElement.GetProperty("version").GetInt32());
                Assert.AreEqual(
                    4,
                    motion.RootElement.GetProperty("maximumOffsetPixels").GetInt32());
                Assert.AreEqual(
                    0.3f,
                    motion.RootElement.GetProperty("motionResponseSeconds").GetSingle());
                Assert.AreEqual(
                    1.5f,
                    motion.RootElement.GetProperty("idleTransitionSeconds").GetSingle());
                Assert.AreEqual(
                    0.9f,
                    motion.RootElement.GetProperty("pointerRange").GetSingle());
                Assert.AreEqual(
                    2.4f,
                    motion.RootElement.GetProperty("maximumMotionSpeed").GetSingle());
                Assert.AreEqual(
                    1,
                    motion.RootElement.GetProperty("objects").GetArrayLength());

                using Stream playerStream = package.GetEntry("player.js")!.Open();
                using var playerReader = new StreamReader(playerStream);
                string player = playerReader.ReadToEnd();
                StringAssert.Contains(player, "function smoothDamp(");
                StringAssert.Contains(player, "Math.hypot(x, y)");
                StringAssert.Contains(player, "maximumCanvasPixels = 3840 * 2160");
            }
        }

        [TestMethod]
        public void CalculateMaximumOffset_SubjectScene_ReservesEdgeBuffer() {
            Assert.AreEqual(
                28,
                DynamicImageWebExporter.CalculateMaximumOffset(5424, 3196, 1, 32));
        }

        [TestMethod]
        public void CalculateMaximumOffset_PureScene_UsesResolutionBudget() {
            Assert.AreEqual(
                26,
                DynamicImageWebExporter.CalculateMaximumOffset(5424, 3196, 0, 0));
        }

        [TestMethod]
        public void SaveRuntimeTexture_LargeRgbaImage_DownscalesAndPreservesAlpha() {
            string sourcePath = Path.Combine(_directory, "large_rgba.png");
            string outputPath = Path.Combine(_directory, "runtime_rgba.png");
            using (var source = new Mat(10, 4100, MatType.CV_8UC4, new Scalar(10, 20, 30, 127)))
                Assert.IsTrue(Cv2.ImWrite(sourcePath, source));

            DynamicImageWebExporter.SaveRuntimeTexture(sourcePath, outputPath, 4096);

            using var output = Cv2.ImRead(outputPath, ImreadModes.Unchanged);
            Assert.AreEqual(4096, output.Width);
            Assert.AreEqual(10, output.Height);
            Assert.AreEqual(4, output.Channels());
            Assert.AreEqual(127, output.At<Vec4b>(0, 0).Item3);
        }

        [TestMethod]
        public void SaveBackgroundDepth_LargeImage_PreservesPixelsOutsideRemovalMask() {
            const int width = 1030;
            const int height = 16;
            var depth = new float[width * height];
            var mask = new byte[depth.Length];
            for (int index = 0; index < depth.Length; index++)
                depth[index] = (index % 251) / 250f;
            for (int y = 5; y < 11; y++) {
                for (int x = 500; x < 530; x++)
                    mask[(y * width) + x] = byte.MaxValue;
            }

            string outputPath = Path.Combine(_directory, "background_depth.png");
            DynamicImageExporter.SaveBackgroundDepth(depth, mask, width, height, outputPath);

            using var output = Cv2.ImRead(outputPath, ImreadModes.Grayscale);
            Assert.AreEqual(width, output.Width);
            Assert.AreEqual(height, output.Height);
            for (int index = 0; index < depth.Length; index++) {
                if (mask[index] != 0)
                    continue;
                int y = index / width;
                int x = index % width;
                byte expected = (byte)Math.Round(depth[index] * byte.MaxValue);
                Assert.AreEqual(expected, output.At<byte>(y, x), $"Pixel ({x}, {y}) changed.");
            }
        }

        [TestMethod]
        public void Export_SourceDimensionsDoNotMatch_Throws() {
            string imagePath = TestImageHelper.CreateSolidColorJpeg(2, 2, _directory);
            var depth = new DepthEstimateModelOutput([0.5f], 1, 1, 1, 1);
            DynamicImageLayerPlan plan = LayerFusionEngine.Fuse(
                depth,
                [],
                [],
                new LayerFusionOptions { DepthSmoothingDiameter = 0 });
            var analysis = new DynamicImageAnalysisResult(
                new ObjectDetectionModelOutput([], 1, 1, 640, 640, 1, 1, 1, 1),
                [],
                new SegmentationModelOutput([], 1, 1, 1024, 1, 1, 1, 1),
                depth,
                plan,
                new InpaintingModelOutput(new byte[3], 1, 1, false) {
                    AppliedMask = new byte[1]
                },
                new DynamicImageAnalysisTiming(
                    TimeSpan.Zero,
                    TimeSpan.Zero,
                    TimeSpan.Zero,
                    TimeSpan.Zero,
                    TimeSpan.Zero));

            Assert.Throws<ArgumentException>(() => DynamicImageExporter.Export(
                imagePath,
                analysis,
                Path.Combine(_directory, "output")));
        }
    }
}
