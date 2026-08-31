using VirtualPaper.ML.DynamicImage;
using VirtualPaper.ML.ObjectDetection.Models;
using VirtualPaper.ML.Segmentation.Models;

namespace VirtualPaper.ML.Test.T_DynamicImage {
    [TestClass]
    [TestCategory("Unit")]
    public class ObjectMaskConsolidatorTests {
        [TestMethod]
        public void Consolidate_NestedMasksOfSameClass_MergesIntoUnion() {
            const int width = 4;
            const int height = 2;
            byte[] small = Alpha(width, height, 0, 1, 4);
            byte[] large = Alpha(width, height, 0, 1, 2, 3, 4, 5);

            ObjectMaskConsolidator.ConsolidatedObjects result =
                ObjectMaskConsolidator.Consolidate(
                    [
                        Detection(0, "person", 0.9f, 0, 0, 2, 2),
                        Detection(0, "person", 0.7f, 0, 0, 4, 2)
                    ],
                    [Mask(small, width, height, 0.8f), Mask(large, width, height, 0.95f)],
                    0.9f);

            Assert.HasCount(1, result.Detections);
            Assert.HasCount(1, result.Masks);
            Assert.AreEqual(0.9f, result.Detections[0].Score);
            Assert.AreEqual(4f, result.Detections[0].Right);
            Assert.AreEqual(0.95f, result.Masks[0].PredictedIoU);
            CollectionAssert.AreEqual(large, result.Masks[0].Alpha);
        }

        [TestMethod]
        public void Consolidate_PartialOverlapBelowThreshold_KeepsBoth() {
            const int width = 4;
            const int height = 2;
            ObjectMaskConsolidator.ConsolidatedObjects result =
                ObjectMaskConsolidator.Consolidate(
                    [
                        Detection(0, "person", 0.9f, 0, 0, 2, 2),
                        Detection(0, "person", 0.8f, 1, 0, 3, 2)
                    ],
                    [
                        Mask(Alpha(width, height, 0, 1, 4, 5), width, height),
                        Mask(Alpha(width, height, 1, 2, 5, 6), width, height)
                    ],
                    0.9f);

            Assert.HasCount(2, result.Detections);
        }

        [TestMethod]
        public void Consolidate_NestedMasksOfDifferentClasses_KeepsBoth() {
            const int width = 2;
            const int height = 2;
            byte[] alpha = Alpha(width, height, 0, 1, 2, 3);
            ObjectMaskConsolidator.ConsolidatedObjects result =
                ObjectMaskConsolidator.Consolidate(
                    [
                        Detection(0, "person", 0.9f, 0, 0, 2, 2),
                        Detection(16, "dog", 0.8f, 0, 0, 2, 2)
                    ],
                    [Mask(alpha, width, height), Mask(alpha, width, height)],
                    0.9f);

            Assert.HasCount(2, result.Detections);
        }

        private static DetectedObject Detection(
            int labelId,
            string label,
            float score,
            float left,
            float top,
            float right,
            float bottom) =>
            new(labelId, label, score, left, top, right, bottom);

        private static SegmentationMask Mask(
            byte[] alpha,
            int width,
            int height,
            float iou = 0.9f) =>
            new(new SegmentationBox(0, 0, width, height), iou, alpha, width, height);

        private static byte[] Alpha(int width, int height, params int[] pixels) {
            var result = new byte[checked(width * height)];
            foreach (int pixel in pixels)
                result[pixel] = byte.MaxValue;
            return result;
        }
    }
}
