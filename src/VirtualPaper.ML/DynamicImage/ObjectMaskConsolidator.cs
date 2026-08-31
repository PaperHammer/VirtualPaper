using VirtualPaper.ML.ObjectDetection.Models;
using VirtualPaper.ML.Segmentation.Models;

namespace VirtualPaper.ML.DynamicImage {
    /// <summary>
    /// Merges highly-contained masks of the same class. This handles nested
    /// detector results that describe different extents of the same object.
    /// </summary>
    internal static class ObjectMaskConsolidator {
        internal static ConsolidatedObjects Consolidate(
            IReadOnlyList<DetectedObject> detections,
            IReadOnlyList<SegmentationMask> masks,
            float containmentThreshold) {

            ArgumentNullException.ThrowIfNull(detections);
            ArgumentNullException.ThrowIfNull(masks);
            if (detections.Count != masks.Count)
                throw new ArgumentException("Each detection must have one corresponding mask.");
            if (!float.IsFinite(containmentThreshold) ||
                containmentThreshold <= 0f || containmentThreshold > 1f) {
                throw new ArgumentOutOfRangeException(nameof(containmentThreshold));
            }
            if (detections.Count <= 1)
                return new ConsolidatedObjects(detections, masks);

            int width = masks[0].Width;
            int height = masks[0].Height;
            int pixelCount = checked(width * height);
            var areas = new int[masks.Count];
            for (int index = 0; index < masks.Count; index++) {
                SegmentationMask mask = masks[index];
                if (mask.Width != width || mask.Height != height || mask.Alpha.Length != pixelCount)
                    throw new ArgumentException("All masks must have matching dimensions.", nameof(masks));
                areas[index] = mask.Alpha.Count(value => value != 0);
            }

            var parent = Enumerable.Range(0, detections.Count).ToArray();
            for (int left = 0; left < detections.Count; left++) {
                if (areas[left] == 0)
                    continue;
                for (int right = left + 1; right < detections.Count; right++) {
                    if (areas[right] == 0 || detections[left].LabelId != detections[right].LabelId)
                        continue;

                    int overlap = CountOverlap(masks[left].Alpha, masks[right].Alpha);
                    float containment = (float)overlap / Math.Min(areas[left], areas[right]);
                    if (containment >= containmentThreshold)
                        Union(parent, left, right);
                }
            }

            var groups = Enumerable.Range(0, detections.Count)
                .GroupBy(index => Find(parent, index))
                .Select(group => group.ToArray())
                .OrderByDescending(group => group.Max(index => detections[index].Score))
                .ToArray();
            var mergedDetections = new List<DetectedObject>(groups.Length);
            var mergedMasks = new List<SegmentationMask>(groups.Length);

            foreach (int[] group in groups) {
                if (group.Length == 1) {
                    mergedDetections.Add(detections[group[0]]);
                    mergedMasks.Add(masks[group[0]]);
                    continue;
                }

                int representativeIndex = group
                    .OrderByDescending(index => detections[index].Score)
                    .First();
                DetectedObject representative = detections[representativeIndex];
                float left = group.Min(index => detections[index].Left);
                float top = group.Min(index => detections[index].Top);
                float right = group.Max(index => detections[index].Right);
                float bottom = group.Max(index => detections[index].Bottom);
                float score = group.Max(index => detections[index].Score);
                float predictedIou = group.Max(index => masks[index].PredictedIoU);
                var alpha = new byte[pixelCount];
                foreach (int sourceIndex in group) {
                    byte[] source = masks[sourceIndex].Alpha;
                    for (int pixel = 0; pixel < pixelCount; pixel++)
                        alpha[pixel] = Math.Max(alpha[pixel], source[pixel]);
                }

                var mergedDetection = new DetectedObject(
                    representative.LabelId,
                    representative.Label,
                    score,
                    left,
                    top,
                    right,
                    bottom);
                mergedDetections.Add(mergedDetection);
                mergedMasks.Add(new SegmentationMask(
                    SegmentationBox.FromDetection(mergedDetection),
                    predictedIou,
                    alpha,
                    width,
                    height));
            }

            return new ConsolidatedObjects(mergedDetections, mergedMasks);
        }

        private static int CountOverlap(byte[] left, byte[] right) {
            int result = 0;
            for (int index = 0; index < left.Length; index++) {
                if (left[index] != 0 && right[index] != 0)
                    result++;
            }
            return result;
        }

        private static int Find(int[] parent, int value) {
            while (parent[value] != value) {
                parent[value] = parent[parent[value]];
                value = parent[value];
            }
            return value;
        }

        private static void Union(int[] parent, int left, int right) {
            int leftRoot = Find(parent, left);
            int rightRoot = Find(parent, right);
            if (leftRoot != rightRoot)
                parent[rightRoot] = leftRoot;
        }

        internal sealed record ConsolidatedObjects(
            IReadOnlyList<DetectedObject> Detections,
            IReadOnlyList<SegmentationMask> Masks);
    }
}
