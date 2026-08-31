using System.Runtime.InteropServices;
using OpenCvSharp;
using VirtualPaper.ML.DepthEstimate.Models;
using VirtualPaper.ML.DynamicImage.Models;
using VirtualPaper.ML.ObjectDetection.Models;
using VirtualPaper.ML.Segmentation.Models;

namespace VirtualPaper.ML.DynamicImage {
    /// <summary>
    /// Combines relative depth, detected objects and object masks into a
    /// deterministic back-to-front layer plan. Larger depth values are nearer.
    /// </summary>
    public static class LayerFusionEngine {
        public static DynamicImageLayerPlan Fuse(
            DepthEstimateModelOutput depthOutput,
            IReadOnlyList<DetectedObject> detections,
            IReadOnlyList<SegmentationMask> masks,
            LayerFusionOptions? options = null,
            CancellationToken ct = default) {

            ArgumentNullException.ThrowIfNull(depthOutput);
            ArgumentNullException.ThrowIfNull(detections);
            ArgumentNullException.ThrowIfNull(masks);
            options ??= new LayerFusionOptions();
            options.Validate();
            ct.ThrowIfCancellationRequested();

            int width = depthOutput.Width;
            int height = depthOutput.Height;
            int pixelCount = checked(width * height);
            if (width <= 0 || height <= 0 || depthOutput.Depth.Length != pixelCount)
                throw new ArgumentException("Depth output dimensions are invalid.", nameof(depthOutput));
            if (detections.Count != masks.Count)
                throw new ArgumentException("Each detection must have one corresponding mask.");

            foreach (SegmentationMask mask in masks) {
                if (mask.Width != width || mask.Height != height || mask.Alpha.Length != pixelCount)
                    throw new ArgumentException("Every segmentation mask must match the depth dimensions.", nameof(masks));
            }

            float[] depth = SmoothDepth(depthOutput.Depth, width, height, options);
            float globalMedian = Quantile(
                depth,
                0.5f,
                options.MaximumQuantileSamples,
                excluded: null);

            var evaluatedCandidates = new List<ObjectCandidate>();
            for (int index = 0;
                index < detections.Count && evaluatedCandidates.Count < options.MaxObjects;
                index++) {

                ct.ThrowIfCancellationRequested();
                SegmentationMask mask = masks[index];
                if (!float.IsFinite(mask.PredictedIoU) ||
                    mask.PredictedIoU < options.MinimumSegmentationIoU) {
                    continue;
                }

                LayerDepthStatistics statistics = CalculateStatistics(
                    depth,
                    mask.Alpha,
                    options.MaximumLayerDepthSamples);
                if ((float)statistics.PixelCount / pixelCount < options.MinimumObjectAreaRatio)
                    continue;

                string id = $"object_{index:D3}_{Sanitize(detections[index].Label)}";
                float salience = CalculateSalience(
                    detections[index],
                    mask,
                    statistics,
                    globalMedian,
                    width,
                    height,
                    pixelCount);
                evaluatedCandidates.Add(new ObjectCandidate(
                    id,
                    detections[index],
                    mask,
                    statistics,
                    salience,
                    DynamicImageSubjectRole.SceneElement,
                    false));
            }

            int primaryCandidateIndex = -1;
            float highestIndependentSalience = float.NegativeInfinity;
            for (int index = 0; index < evaluatedCandidates.Count; index++) {
                ObjectCandidate candidate = evaluatedCandidates[index];
                if (candidate.Salience < options.MinimumIndependentSubjectSalience ||
                    candidate.Salience <= highestIndependentSalience) {
                    continue;
                }

                primaryCandidateIndex = index;
                highestIndependentSalience = candidate.Salience;
            }

            var candidates = new List<ObjectCandidate>(evaluatedCandidates.Count);
            var subjectAssessments = new List<DynamicImageSubjectAssessment>(evaluatedCandidates.Count);
            for (int index = 0; index < evaluatedCandidates.Count; index++) {
                ObjectCandidate candidate = evaluatedCandidates[index];
                bool isIndependent =
                    candidate.Salience >= options.MinimumIndependentSubjectSalience;
                float areaRatio = (float)candidate.Depth.PixelCount / pixelCount;
                bool isPrimary = isIndependent && index == primaryCandidateIndex &&
                    (candidate.Salience >= options.PrimarySubjectSalience ||
                     areaRatio >= options.SubjectDominantAreaRatio);
                DynamicImageSubjectRole role = !isIndependent
                    ? DynamicImageSubjectRole.SceneElement
                    : isPrimary
                        ? DynamicImageSubjectRole.PrimarySubject
                        : DynamicImageSubjectRole.SecondarySubject;
                ObjectCandidate assessed = candidate with {
                    Role = role,
                    IsIndependent = isIndependent
                };
                candidates.Add(assessed);
                subjectAssessments.Add(new DynamicImageSubjectAssessment(
                    assessed.Detection,
                    assessed.Mask.PredictedIoU,
                    assessed.Depth,
                    assessed.Salience,
                    assessed.Role,
                    assessed.IsIndependent));
            }

            ObjectCandidate[] independentCandidates = candidates
                .Where(candidate => candidate.IsIndependent)
                .ToArray();

            // Only subjects selected for independent motion are removed from
            // scene statistics. Small or low-salience detections remain part
            // of the landscape and cannot distort its depth bands.
            var sceneExclusion = new byte[pixelCount];
            foreach (ObjectCandidate candidate in independentCandidates) {
                for (int index = 0; index < pixelCount; index++) {
                    if (candidate.Mask.Alpha[index] != 0)
                        sceneExclusion[index] = byte.MaxValue;
                }
            }

            var sceneAlpha = new byte[pixelCount];
            for (int index = 0; index < pixelCount; index++) {
                if (sceneExclusion[index] == 0)
                    sceneAlpha[index] = byte.MaxValue;
            }
            LayerDepthStatistics sceneDepth = CalculateStatistics(
                depth,
                sceneAlpha,
                options.MaximumLayerDepthSamples);
            float backgroundThreshold;
            float foregroundThreshold;
            if (sceneDepth.PixelCount == 0) {
                backgroundThreshold = globalMedian;
                foregroundThreshold = globalMedian;
            }
            else if (sceneDepth.Maximum - sceneDepth.Minimum <= options.MinimumSceneDepthRange) {
                // A flat scene has no meaningful discrete depth separation.
                // Both thresholds collapse so it produces one scene layer.
                backgroundThreshold = sceneDepth.Maximum;
                foregroundThreshold = sceneDepth.Maximum;
            }
            else {
                backgroundThreshold = Quantile(
                    depth,
                    options.BackgroundQuantile,
                    options.MaximumQuantileSamples,
                    sceneExclusion);
                foregroundThreshold = Quantile(
                    depth,
                    options.ForegroundQuantile,
                    options.MaximumQuantileSamples,
                    sceneExclusion);
            }

            // Nearest objects claim overlap pixels first.
            Array.Sort(independentCandidates, static (left, right) => {
                int depthOrder = right.Depth.Median.CompareTo(left.Depth.Median);
                return depthOrder != 0
                    ? depthOrder
                    : right.Detection.Score.CompareTo(left.Detection.Score);
            });

            var occupied = new byte[pixelCount];
            var inpaintingMask = new byte[pixelCount];
            var objectLayers = new List<DynamicImageLayer>(candidates.Count);
            var occlusions = new List<LayerOcclusion>();

            foreach (ObjectCandidate candidate in independentCandidates) {
                ct.ThrowIfCancellationRequested();
                var visible = new byte[pixelCount];
                var occludedBy = new List<string>();

                foreach (DynamicImageLayer nearerLayer in objectLayers) {
                    int overlap = CountOverlap(candidate.Mask.Alpha, nearerLayer.SourceAlpha);
                    if (overlap < options.MinimumOcclusionPixels)
                        continue;

                    occludedBy.Add(nearerLayer.Id);
                    occlusions.Add(new LayerOcclusion(
                        nearerLayer.Id,
                        candidate.Id,
                        overlap,
                        (float)overlap / candidate.Depth.PixelCount));
                }

                for (int index = 0; index < pixelCount; index++) {
                    if (candidate.Mask.Alpha[index] == 0)
                        continue;

                    inpaintingMask[index] = byte.MaxValue;
                    if (occupied[index] == 0)
                        visible[index] = candidate.Mask.Alpha[index];
                    occupied[index] = byte.MaxValue;
                }

                objectLayers.Add(new DynamicImageLayer(
                    candidate.Id,
                    DynamicImageLayerKind.DetectedObject,
                    Classify(candidate.Depth.Median, backgroundThreshold, foregroundThreshold),
                    candidate.Depth,
                    candidate.Mask.Alpha.ToArray(),
                    visible,
                    candidate.Detection,
                    candidate.Mask.PredictedIoU,
                    occludedBy) {
                    SubjectSalience = candidate.Salience,
                    SubjectRole = candidate.Role
                });
            }

            var allLayers = new List<DynamicImageLayer>(objectLayers.Count + 3);
            foreach (SceneDepthBand band in Enum.GetValues<SceneDepthBand>()) {
                var alpha = new byte[pixelCount];
                for (int index = 0; index < pixelCount; index++) {
                    if (occupied[index] == 0 &&
                        Classify(depth[index], backgroundThreshold, foregroundThreshold) == band) {
                        alpha[index] = byte.MaxValue;
                    }
                }

                LayerDepthStatistics statistics = CalculateStatistics(
                    depth,
                    alpha,
                    options.MaximumLayerDepthSamples);
                if (statistics.PixelCount == 0)
                    continue;

                allLayers.Add(new DynamicImageLayer(
                    $"scene_{band.ToString().ToLowerInvariant()}",
                    DynamicImageLayerKind.SceneBand,
                    band,
                    statistics,
                    alpha,
                    alpha,
                    null,
                    null,
                    []));
            }

            allLayers.AddRange(objectLayers);
            allLayers.Sort(static (left, right) => {
                int depthOrder = left.Depth.Median.CompareTo(right.Depth.Median);
                if (depthOrder != 0)
                    return depthOrder;
                return left.Kind.CompareTo(right.Kind);
            });

            int independentPixelCount = sceneExclusion.Count(value => value != 0);
            float independentAreaRatio = (float)independentPixelCount / pixelCount;
            DynamicImageSceneKind sceneKind = subjectAssessments.Count == 0
                ? DynamicImageSceneKind.PureScene
                : independentCandidates.Length == 0
                    ? DynamicImageSceneKind.SceneDominant
                    : independentAreaRatio >= options.SubjectDominantAreaRatio ||
                      subjectAssessments.Any(subject =>
                          subject.Role == DynamicImageSubjectRole.PrimarySubject)
                        ? DynamicImageSceneKind.SubjectDominant
                        : DynamicImageSceneKind.Mixed;

            return new DynamicImageLayerPlan(
                allLayers,
                occlusions,
                inpaintingMask,
                depth,
                width,
                height,
                backgroundThreshold,
                foregroundThreshold,
                sceneKind,
                subjectAssessments);
        }

        private static float[] SmoothDepth(
            float[] source,
            int width,
            int height,
            LayerFusionOptions options) {

            var normalized = new float[source.Length];
            for (int index = 0; index < source.Length; index++)
                normalized[index] = float.IsFinite(source[index])
                    ? Math.Clamp(source[index], 0f, 1f)
                    : 0f;

            if (options.DepthSmoothingDiameter == 0)
                return normalized;

            using var input = new Mat(height, width, MatType.CV_32FC1);
            Marshal.Copy(normalized, 0, input.Data, normalized.Length);
            double scale = Math.Min(
                1d,
                (double)options.DepthProcessingMaxDimension / Math.Max(width, height));
            int processingWidth = Math.Max(1, (int)Math.Round(width * scale));
            int processingHeight = Math.Max(1, (int)Math.Round(height * scale));
            using var processingInput = new Mat();
            if (processingWidth == width && processingHeight == height)
                input.CopyTo(processingInput);
            else
                Cv2.Resize(
                    input,
                    processingInput,
                    new Size(processingWidth, processingHeight),
                    interpolation: InterpolationFlags.Area);

            using var processingOutput = new Mat();
            Cv2.BilateralFilter(
                processingInput,
                processingOutput,
                options.DepthSmoothingDiameter,
                options.DepthSmoothingSigmaColor,
                options.DepthSmoothingSigmaSpace);
            using var output = new Mat();
            if (processingWidth == width && processingHeight == height)
                processingOutput.CopyTo(output);
            else
                Cv2.Resize(
                    processingOutput,
                    output,
                    new Size(width, height),
                    interpolation: InterpolationFlags.Linear);

            Marshal.Copy(output.Data, normalized, 0, normalized.Length);
            for (int index = 0; index < normalized.Length; index++)
                normalized[index] = float.IsFinite(normalized[index])
                    ? Math.Clamp(normalized[index], 0f, 1f)
                    : 0f;
            return normalized;
        }

        private static float Quantile(
            float[] values,
            float quantile,
            int maximumSamples,
            byte[]? excluded) {

            int eligibleCount = excluded is null
                ? values.Length
                : excluded.Count(value => value == 0);
            int stride = Math.Max(1, (int)Math.Ceiling((double)eligibleCount / maximumSamples));
            var samples = new List<float>(Math.Min(values.Length, maximumSamples));
            int eligibleIndex = 0;
            for (int index = 0; index < values.Length; index++) {
                if (excluded is not null && excluded[index] != 0)
                    continue;
                if (eligibleIndex++ % stride == 0 && float.IsFinite(values[index]))
                    samples.Add(values[index]);
            }

            if (samples.Count == 0)
                return 0f;
            samples.Sort();
            int sampleIndex = (int)Math.Round(
                quantile * (samples.Count - 1),
                MidpointRounding.AwayFromZero);
            return samples[Math.Clamp(sampleIndex, 0, samples.Count - 1)];
        }

        private static float CalculateSalience(
            DetectedObject detection,
            SegmentationMask mask,
            LayerDepthStatistics depth,
            float globalMedian,
            int width,
            int height,
            int pixelCount) {

            float areaRatio = (float)depth.PixelCount / pixelCount;
            float areaScore = Math.Clamp(areaRatio / 0.15f, 0f, 1f);
            float centerX = (detection.Left + detection.Right) * 0.5f;
            float centerY = (detection.Top + detection.Bottom) * 0.5f;
            float normalizedX = (centerX - width * 0.5f) / Math.Max(1f, width * 0.5f);
            float normalizedY = (centerY - height * 0.5f) / Math.Max(1f, height * 0.5f);
            float centerDistance = MathF.Sqrt(
                normalizedX * normalizedX + normalizedY * normalizedY) / MathF.Sqrt(2f);
            float centerScore = 1f - Math.Clamp(centerDistance, 0f, 1f);
            float confidenceScore = Math.Clamp(detection.Score, 0f, 1f);
            float segmentationScore = Math.Clamp(mask.PredictedIoU, 0f, 1f);
            float depthProminence = Math.Clamp(
                Math.Abs(depth.Median - globalMedian) / 0.35f,
                0f,
                1f);

            return Math.Clamp(
                0.50f * areaScore +
                0.10f * centerScore +
                0.10f * confidenceScore +
                0.10f * segmentationScore +
                0.20f * depthProminence,
                0f,
                1f);
        }

        private static LayerDepthStatistics CalculateStatistics(
            float[] depth,
            byte[] alpha,
            int maximumMedianSamples) {

            int count = 0;
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            double sum = 0;
            for (int index = 0; index < alpha.Length; index++) {
                if (alpha[index] == 0)
                    continue;
                float value = depth[index];
                count++;
                minimum = Math.Min(minimum, value);
                maximum = Math.Max(maximum, value);
                sum += value;
            }

            if (count == 0)
                return new LayerDepthStatistics(0f, 0f, 0f, 0f, 0);

            int stride = Math.Max(1, (int)Math.Ceiling((double)count / maximumMedianSamples));
            var samples = new List<float>(Math.Min(count, maximumMedianSamples));
            int selectedIndex = 0;
            for (int index = 0; index < alpha.Length; index++) {
                if (alpha[index] == 0)
                    continue;
                if (selectedIndex++ % stride == 0)
                    samples.Add(depth[index]);
            }
            samples.Sort();
            float median = samples[samples.Count / 2];
            return new LayerDepthStatistics(
                minimum,
                maximum,
                (float)(sum / count),
                median,
                count);
        }

        private static int CountOverlap(byte[] left, byte[] right) {
            int count = 0;
            for (int index = 0; index < left.Length; index++) {
                if (left[index] != 0 && right[index] != 0)
                    count++;
            }
            return count;
        }

        private static SceneDepthBand Classify(
            float depth,
            float backgroundThreshold,
            float foregroundThreshold) {

            if (depth <= backgroundThreshold)
                return SceneDepthBand.Background;
            return depth >= foregroundThreshold
                ? SceneDepthBand.Foreground
                : SceneDepthBand.Midground;
        }

        private static string Sanitize(string value) {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            var result = new char[value.Length];
            int length = 0;
            bool previousSeparator = false;
            foreach (char rawCharacter in value) {
                char character = char.ToLowerInvariant(rawCharacter);
                if (char.IsLetterOrDigit(character)) {
                    result[length++] = character;
                    previousSeparator = false;
                }
                else if (!previousSeparator && length != 0) {
                    result[length++] = '_';
                    previousSeparator = true;
                }
            }

            while (length > 0 && result[length - 1] == '_')
                length--;
            return length == 0 ? "unknown" : new string(result, 0, length);
        }

        private sealed record ObjectCandidate(
            string Id,
            DetectedObject Detection,
            SegmentationMask Mask,
            LayerDepthStatistics Depth,
            float Salience,
            DynamicImageSubjectRole Role,
            bool IsIndependent);
    }
}
