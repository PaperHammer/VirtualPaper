using VirtualPaper.Common;
using VirtualPaper.ML.DepthEstimate.Models;
using VirtualPaper.ML.DynamicImage;
using VirtualPaper.ML.DynamicImage.Models;
using VirtualPaper.ML.ObjectDetection.Models;
using VirtualPaper.ML.Segmentation.Models;
using VirtualPaper.ML.Test.Infrastructure;

namespace VirtualPaper.ML.Test.T_DynamicImage {
    [TestClass]
    [TestCategory("Unit")]
    public class LayerFusionEngineTests {
        [TestMethod]
        public void Fuse_OverlappingObjects_NearObjectOccludesFarObject() {
            const int width = 5;
            const int height = 4;
            float[] depth = CreateRowDepth(width, 0.1f, 0.2f, 0.8f, 0.9f);
            byte[] farAlpha = CreateAlpha(width, height,
                (0, 0), (1, 0), (2, 0),
                (0, 1), (1, 1), (2, 1),
                (0, 2));
            byte[] nearAlpha = CreateAlpha(width, height,
                (0, 2), (1, 2), (2, 2),
                (0, 3), (1, 3), (2, 3));

            DynamicImageLayerPlan plan = LayerFusionEngine.Fuse(
                new DepthEstimateModelOutput(depth, width, height, width, height),
                [
                    new DetectedObject(1, "far object", 0.8f, 0, 0, 3, 3),
                    new DetectedObject(2, "near object", 0.9f, 0, 2, 3, 4)
                ],
                [
                    new SegmentationMask(new SegmentationBox(0, 0, 3, 3), 0.9f, farAlpha, width, height),
                    new SegmentationMask(new SegmentationBox(0, 2, 3, 4), 0.95f, nearAlpha, width, height)
                ],
                TestOptions());

            DynamicImageLayer far = plan.Layers.Single(layer => layer.Id.Contains("far_object"));
            DynamicImageLayer near = plan.Layers.Single(layer => layer.Id.Contains("near_object"));
            int overlapIndex = 2 * width;

            Assert.AreEqual(byte.MaxValue, far.SourceAlpha[overlapIndex]);
            Assert.AreEqual(byte.MinValue, far.VisibleAlpha[overlapIndex]);
            Assert.AreEqual(byte.MaxValue, near.VisibleAlpha[overlapIndex]);
            Assert.Contains(near.Id, far.OccludedByLayerIds);

            Assert.HasCount(1, plan.Occlusions);
            LayerOcclusion occlusion = plan.Occlusions.Single();
            Assert.AreEqual(near.Id, occlusion.OccluderLayerId);
            Assert.AreEqual(far.Id, occlusion.OccludedLayerId);
            Assert.AreEqual(1, occlusion.OverlapPixelCount);
            Assert.AreEqual(byte.MaxValue, plan.InpaintingMask[overlapIndex]);
        }

        [TestMethod]
        public void Fuse_VisibleLayers_FormExactImagePartition() {
            const int width = 5;
            const int height = 4;
            byte[] first = CreateAlpha(width, height, (0, 0), (1, 0), (1, 1));
            byte[] second = CreateAlpha(width, height, (1, 0), (2, 0), (2, 1));
            DynamicImageLayerPlan plan = LayerFusionEngine.Fuse(
                new DepthEstimateModelOutput(
                    CreateRowDepth(width, 0.1f, 0.3f, 0.7f, 0.9f),
                    width,
                    height,
                    width,
                    height),
                [
                    new DetectedObject(0, "first", 0.8f, 0, 0, 2, 2),
                    new DetectedObject(1, "second", 0.9f, 1, 0, 3, 2)
                ],
                [
                    new SegmentationMask(new SegmentationBox(0, 0, 2, 2), 0.9f, first, width, height),
                    new SegmentationMask(new SegmentationBox(1, 0, 3, 2), 0.9f, second, width, height)
                ],
                TestOptions());

            for (int index = 0; index < width * height; index++) {
                int owners = plan.Layers.Count(layer => layer.VisibleAlpha[index] != 0);
                Assert.AreEqual(1, owners, $"Pixel {index} must have exactly one visible owner.");
            }
        }

        [TestMethod]
        public void Fuse_LowQualityAndSmallMasks_AreRejected() {
            const int width = 10;
            const int height = 10;
            byte[] alpha = CreateAlpha(width, height, (0, 0));
            DynamicImageLayerPlan plan = LayerFusionEngine.Fuse(
                new DepthEstimateModelOutput(Enumerable.Repeat(0.5f, 100).ToArray(), width, height, width, height),
                [new DetectedObject(0, "person", 0.9f, 0, 0, 1, 1)],
                [new SegmentationMask(new SegmentationBox(0, 0, 1, 1), 0.4f, alpha, width, height)],
                TestOptions() with {
                    MinimumSegmentationIoU = 0.5f,
                    MinimumObjectAreaRatio = 0.02f
                });

            Assert.IsFalse(plan.Layers.Any(layer => layer.IsObject));
            Assert.IsTrue(plan.InpaintingMask.All(value => value == 0));
            Assert.HasCount(1, plan.Layers);
        }

        [TestMethod]
        public void Fuse_ReturnsLayersInBackToFrontOrder() {
            const int width = 4;
            const int height = 4;
            DynamicImageLayerPlan plan = LayerFusionEngine.Fuse(
                new DepthEstimateModelOutput(
                    CreateRowDepth(width, 0.1f, 0.3f, 0.7f, 0.9f),
                    width,
                    height,
                    width,
                    height),
                [],
                [],
                TestOptions());

            float[] medians = plan.BackToFrontLayers.Select(layer => layer.Depth.Median).ToArray();
            CollectionAssert.AreEqual(medians.Order().ToArray(), medians);
            Assert.IsLessThanOrEqualTo(
                plan.ForegroundDepthThreshold,
                plan.BackgroundDepthThreshold);
        }

        [TestMethod]
        public void Fuse_DominantSubject_IsExcludedFromSceneDepthQuantiles() {
            const int width = 10;
            const int height = 1;
            float[] depth = [0f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 1f, 1f, 1f, 1f];
            byte[] subject = CreateAlpha(width, height, (6, 0), (7, 0), (8, 0), (9, 0));

            DynamicImageLayerPlan plan = LayerFusionEngine.Fuse(
                new DepthEstimateModelOutput(depth, width, height, width, height),
                [new DetectedObject(0, "person", 0.95f, 6, 0, 10, 1)],
                [new SegmentationMask(
                    new SegmentationBox(6, 0, 10, 1),
                    0.95f,
                    subject,
                    width,
                    height)],
                TestOptions());

            Assert.AreEqual(DynamicImageSceneKind.SubjectDominant, plan.SceneKind);
            Assert.HasCount(1, plan.Subjects);
            Assert.IsTrue(plan.Subjects[0].IsIndependent);
            Assert.AreEqual(DynamicImageSubjectRole.PrimarySubject, plan.Subjects[0].Role);
            Assert.AreEqual(0.2f, plan.BackgroundDepthThreshold, 0.0001f);
            Assert.AreEqual(0.3f, plan.ForegroundDepthThreshold, 0.0001f);
        }

        [TestMethod]
        public void Fuse_TinyDetectedObject_RemainsPartOfLandscape() {
            const int width = 20;
            const int height = 10;
            float[] depth = Enumerable.Repeat(0.2f, width * height).ToArray();
            depth[5 * width + 10] = 1f;
            byte[] subject = CreateAlpha(width, height, (10, 5));

            DynamicImageLayerPlan plan = LayerFusionEngine.Fuse(
                new DepthEstimateModelOutput(depth, width, height, width, height),
                [new DetectedObject(0, "person", 0.9f, 10, 5, 11, 6)],
                [new SegmentationMask(
                    new SegmentationBox(10, 5, 11, 6),
                    0.95f,
                    subject,
                    width,
                    height)],
                TestOptions());

            Assert.AreEqual(DynamicImageSceneKind.SceneDominant, plan.SceneKind);
            Assert.IsFalse(plan.Layers.Any(layer => layer.IsObject));
            Assert.IsTrue(plan.InpaintingMask.All(value => value == 0));
            Assert.HasCount(1, plan.Subjects);
            Assert.AreEqual(DynamicImageSubjectRole.SceneElement, plan.Subjects[0].Role);
            Assert.IsFalse(plan.Subjects[0].IsIndependent);
        }

        [TestMethod]
        public void Fuse_NoDetectedObjects_ProducesPureScenePlan() {
            const int width = 4;
            const int height = 2;
            DynamicImageLayerPlan plan = LayerFusionEngine.Fuse(
                new DepthEstimateModelOutput(
                    Enumerable.Repeat(0.4f, width * height).ToArray(),
                    width,
                    height,
                    width,
                    height),
                [],
                [],
                TestOptions());

            Assert.AreEqual(DynamicImageSceneKind.PureScene, plan.SceneKind);
            Assert.HasCount(0, plan.Subjects);
            Assert.HasCount(1, plan.Layers);
            Assert.HasCount(width * height, plan.RenderDepth);
            Assert.IsFalse(plan.RequiresBackgroundReconstruction);
        }

        [TestMethod]
        public void Fuse_LargeDepthMap_ProducesFullSizeFiniteRenderDepth() {
            const int width = 12;
            const int height = 8;
            float[] depth = Enumerable.Range(0, width * height)
                .Select(index => (float)index / (width * height - 1))
                .ToArray();

            DynamicImageLayerPlan plan = LayerFusionEngine.Fuse(
                new DepthEstimateModelOutput(depth, width, height, width, height),
                [],
                [],
                TestOptions() with {
                    DepthProcessingMaxDimension = 4,
                    DepthSmoothingDiameter = 3
                });

            Assert.HasCount(width * height, plan.RenderDepth);
            Assert.IsTrue(plan.RenderDepth.All(float.IsFinite));
            Assert.IsTrue(plan.RenderDepth.All(value => value is >= 0f and <= 1f));
        }

        [TestMethod]
        public void Fuse_MismatchedDetectionAndMaskCounts_Throws() {
            var output = new DepthEstimateModelOutput([0.5f], 1, 1, 1, 1);
            Assert.Throws<ArgumentException>(() => LayerFusionEngine.Fuse(
                output,
                [new DetectedObject(0, "person", 0.9f, 0, 0, 1, 1)],
                [],
                TestOptions()));
        }

        private static LayerFusionOptions TestOptions() => new() {
            MinimumObjectAreaRatio = 0f,
            MinimumSegmentationIoU = 0f,
            DepthSmoothingDiameter = 0
        };

        private static float[] CreateRowDepth(int width, params float[] rows) =>
            rows.SelectMany(value => Enumerable.Repeat(value, width)).ToArray();

        private static byte[] CreateAlpha(
            int width,
            int height,
            params (int X, int Y)[] pixels) {

            var alpha = new byte[checked(width * height)];
            foreach ((int x, int y) in pixels)
                alpha[y * width + x] = byte.MaxValue;
            return alpha;
        }
    }

    [TestClass]
    [TestCategory("Unit")]
    public class DynamicImageAnalysisOptionsTests {
        [TestMethod]
        public void Validate_DefaultOptions_DoesNotThrow() {
            new DynamicImageAnalysisOptions().Validate();
        }

        [TestMethod]
        public void Validate_TooFewSegmentationBoxes_Throws() {
            var options = new DynamicImageAnalysisOptions {
                Segmentation = new MobileSamOptions { MaxBoxes = 2 },
                Fusion = new LayerFusionOptions { MaxObjects = 3 }
            };
            Assert.Throws<ArgumentException>(() => options.Validate());
        }

        [TestMethod]
        public void Validate_InvalidDuplicateContainmentThreshold_Throws() {
            var options = new DynamicImageAnalysisOptions {
                DuplicateMaskContainmentThreshold = 0f
            };
            Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
        }

        [TestMethod]
        public void Validate_InvalidDepthProcessingSize_Throws() {
            var options = new DynamicImageAnalysisOptions {
                Fusion = new LayerFusionOptions { DepthProcessingMaxDimension = 0 }
            };
            Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
        }

        [TestMethod]
        public void Analyze_WithoutLoadingModels_Throws() {
            string directory = Path.Combine(Path.GetTempPath(), $"dynamic_image_{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try {
                string imagePath = TestImageHelper.CreateSolidColorJpeg(16, 16, directory);
                using var analyzer = new DynamicImageAnalyzer();
                Assert.Throws<InvalidOperationException>(() => analyzer.Analyze(imagePath));
            }
            finally {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestClass]
    [TestCategory("Integration")]
    public class DynamicImageAnalyzerIntegrationTests {
        private static readonly string ModelRoot = Path.Combine(
            Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),
            Constants.WorkingDir.ML);

        [TestMethod]
        public void Analyze_LocalModels_ReturnsCompleteLayerPlan() {
            string detectorPath = Path.Combine(
                ModelRoot,
                "ObjectDetection",
                "ai_models",
                VirtualPaper.ML.ObjectDetection.Utils.Fields.ModelName);
            string encoderPath = Path.Combine(
                ModelRoot,
                "Segmentation",
                "ai_models",
                VirtualPaper.ML.Segmentation.Utils.Fields.EncoderModelName);
            string decoderPath = Path.Combine(
                ModelRoot,
                "Segmentation",
                "ai_models",
                VirtualPaper.ML.Segmentation.Utils.Fields.DecoderModelName);
            string depthPath = Path.Combine(
                ModelRoot,
                "DepthEstimate",
                "ai_models",
                VirtualPaper.ML.DepthEstimate.Utils.Fields.DepthAnythingV2ModelName);
            string inpaintingPath = Path.Combine(
                ModelRoot,
                "Inpainting",
                "ai_models",
                VirtualPaper.ML.Inpainting.Utils.Fields.ModelName);

            string[] paths = [detectorPath, encoderPath, decoderPath, depthPath, inpaintingPath];
            if (paths.Any(path => !File.Exists(path)))
                Assert.Inconclusive($"Required local model is missing: {string.Join("; ", paths)}");

            string directory = Path.Combine(Path.GetTempPath(), $"dynamic_image_{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try {
                string imagePath = TestImageHelper.CreateSolidColorJpeg(96, 64, directory);
                using var analyzer = new DynamicImageAnalyzer();
                analyzer.LoadModels(
                    detectorPath,
                    encoderPath,
                    decoderPath,
                    depthPath,
                    inpaintingPath);

                DynamicImageAnalysisResult result = analyzer.Analyze(imagePath);

                Assert.AreEqual(96, result.LayerPlan.Width);
                Assert.AreEqual(64, result.LayerPlan.Height);
                Assert.HasCount(96 * 64, result.LayerPlan.InpaintingMask);
                Assert.HasCount(96 * 64 * 3, result.BackgroundPlate.BgrPixels);
                Assert.IsGreaterThan(TimeSpan.Zero, result.Timing.Total);
                for (int index = 0; index < 96 * 64; index++) {
                    Assert.AreEqual(
                        1,
                        result.LayerPlan.Layers.Count(layer => layer.VisibleAlpha[index] != 0),
                        $"Pixel {index} must have exactly one visible owner.");
                }
            }
            finally {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
