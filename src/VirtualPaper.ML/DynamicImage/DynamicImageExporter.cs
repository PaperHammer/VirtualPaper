using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCvSharp;
using VirtualPaper.ML.DynamicImage.Models;

namespace VirtualPaper.ML.DynamicImage {
    /// <summary>
    /// Writes a compact, inspectable representation of a dynamic-image analysis.
    /// Large pixel arrays are emitted as PNG files rather than embedded in JSON.
    /// </summary>
    public static partial class DynamicImageExporter {
        private static readonly Scalar[] PreviewColors = [
            new(255, 120, 70),
            new(80, 180, 255),
            new(90, 220, 120),
            new(220, 100, 220),
            new(40, 210, 230),
            new(230, 190, 70),
            new(180, 100, 60),
            new(120, 160, 240)
        ];

        public static DynamicImageExportResult Export(
            string imagePath,
            DynamicImageAnalysisResult analysis,
            string outputDirectory,
            CancellationToken ct = default) {

            ArgumentNullException.ThrowIfNull(analysis);
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                throw new FileNotFoundException($"Input image not found: {imagePath}", imagePath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException("Output directory cannot be empty.", nameof(outputDirectory));
            ct.ThrowIfCancellationRequested();

            DynamicImageLayerPlan plan = analysis.LayerPlan;
            ValidatePlan(plan);
            using var source = Cv2.ImRead(imagePath, ImreadModes.Color);
            if (source.Empty())
                throw new ArgumentException($"Failed to load image: {imagePath}", nameof(imagePath));
            if (source.Width != plan.Width || source.Height != plan.Height) {
                throw new ArgumentException(
                    "Source image dimensions must match the layer plan.",
                    nameof(imagePath));
            }
            if (analysis.BackgroundPlate.Width != plan.Width ||
                analysis.BackgroundPlate.Height != plan.Height ||
                analysis.BackgroundPlate.BgrPixels.Length != checked(plan.Width * plan.Height * 3) ||
                analysis.BackgroundPlate.AppliedMask.Length != checked(plan.Width * plan.Height)) {
                throw new ArgumentException(
                    "Background plate dimensions must match the layer plan.",
                    nameof(analysis));
            }

            string root = Path.GetFullPath(outputDirectory);
            string layerRoot = Path.Combine(root, "layers");
            Directory.CreateDirectory(layerRoot);

            string depthPath = Path.Combine(root, "depth.png");
            string rawDepthPath = Path.Combine(root, "depth_raw.png");
            string backgroundPlatePath = Path.Combine(root, "background_plate.png");
            string backgroundDepthPath = Path.Combine(root, "background_depth.png");
            string inpaintingPath = Path.Combine(root, "inpainting_mask.png");
            string appliedInpaintingPath = Path.Combine(root, "inpainting_mask_applied.png");
            string previewPath = Path.Combine(root, "layer_order_preview.png");
            SaveDepth(plan.RenderDepth, plan.Width, plan.Height, depthPath);
            SaveDepth(analysis.Depth.Depth, plan.Width, plan.Height, rawDepthPath);
            using var backgroundPlate = CreateBgrMat(
                analysis.BackgroundPlate.BgrPixels,
                plan.Width,
                plan.Height);
            if (!Cv2.ImWrite(backgroundPlatePath, backgroundPlate))
                throw new IOException($"Failed to save image: {backgroundPlatePath}");
            SaveAlpha(plan.InpaintingMask, plan.Width, plan.Height, inpaintingPath);
            SaveAlpha(
                analysis.BackgroundPlate.AppliedMask,
                plan.Width,
                plan.Height,
                appliedInpaintingPath);
            SaveBackgroundDepth(
                plan.RenderDepth,
                analysis.BackgroundPlate.AppliedMask,
                plan.Width,
                plan.Height,
                backgroundDepthPath);
            SaveLayerPreview(source, plan.Layers, previewPath);

            var exportedLayers = new List<DynamicImageLayerExport>(plan.Layers.Count);
            for (int index = 0; index < plan.Layers.Count; index++) {
                ct.ThrowIfCancellationRequested();
                DynamicImageLayer layer = plan.Layers[index];
                string safeId = SanitizeFileName(layer.Id);
                string prefix = $"{index:D2}_{safeId}";
                string directory = Path.Combine(layerRoot, prefix);
                Directory.CreateDirectory(directory);

                string sourceAlphaPath = Path.Combine(directory, "source_alpha.png");
                string visibleAlphaPath = Path.Combine(directory, "visible_alpha.png");
                string sourceCutoutPath = Path.Combine(directory, "source_cutout.png");
                string visibleCutoutPath = Path.Combine(directory, "visible_cutout.png");
                SaveAlpha(layer.SourceAlpha, plan.Width, plan.Height, sourceAlphaPath);
                Mat layerSource = layer.IsObject ? source : backgroundPlate;
                SaveCutout(layerSource, layer.SourceAlpha, sourceCutoutPath);

                if (layer.SourceAlpha.AsSpan().SequenceEqual(layer.VisibleAlpha)) {
                    visibleAlphaPath = sourceAlphaPath;
                    visibleCutoutPath = sourceCutoutPath;
                }
                else {
                    SaveAlpha(layer.VisibleAlpha, plan.Width, plan.Height, visibleAlphaPath);
                    SaveCutout(layerSource, layer.VisibleAlpha, visibleCutoutPath);
                }

                exportedLayers.Add(new DynamicImageLayerExport(
                    layer.Id,
                    sourceAlphaPath,
                    visibleAlphaPath,
                    sourceCutoutPath,
                    visibleCutoutPath));
            }

            string manifestPath = Path.Combine(root, "manifest.json");
            ExportManifest manifest = CreateManifest(
                imagePath,
                analysis,
                root,
                depthPath,
                rawDepthPath,
                backgroundPlatePath,
                backgroundDepthPath,
                inpaintingPath,
                appliedInpaintingPath,
                previewPath,
                exportedLayers);
            File.WriteAllText(
                manifestPath,
                JsonSerializer.Serialize(manifest, ManifestJsonContext.Default.ExportManifest));

            DynamicImageWebExport webExport = DynamicImageWebExporter.Export(
                imagePath,
                analysis,
                root,
                backgroundPlatePath,
                backgroundDepthPath,
                previewPath,
                exportedLayers,
                ct);

            return new DynamicImageExportResult(
                root,
                manifestPath,
                depthPath,
                inpaintingPath,
                previewPath,
                exportedLayers) {
                RawDepthMapPath = rawDepthPath,
                BackgroundPlatePath = backgroundPlatePath,
                BackgroundDepthMapPath = backgroundDepthPath,
                AppliedInpaintingMaskPath = appliedInpaintingPath,
                InpaintingSafetyMarginPixels = analysis.BackgroundPlate.SafetyMarginPixels,
                MotionConfigurationPath = webExport.MotionConfigurationPath,
                WebPackagePath = webExport.PackagePath
            };
        }

        private static void ValidatePlan(DynamicImageLayerPlan plan) {
            int pixelCount = checked(plan.Width * plan.Height);
            if (plan.Width <= 0 || plan.Height <= 0 ||
                plan.InpaintingMask.Length != pixelCount ||
                plan.RenderDepth.Length != pixelCount) {
                throw new ArgumentException("Layer plan dimensions are invalid.", nameof(plan));
            }
            foreach (DynamicImageLayer layer in plan.Layers) {
                if (layer.SourceAlpha.Length != pixelCount || layer.VisibleAlpha.Length != pixelCount)
                    throw new ArgumentException($"Layer '{layer.Id}' dimensions are invalid.", nameof(plan));
            }
        }

        private static void SaveDepth(float[] depth, int width, int height, string path) {
            if (depth.Length != checked(width * height))
                throw new ArgumentException("Depth dimensions do not match the layer plan.", nameof(depth));

            var pixels = new byte[depth.Length];
            for (int index = 0; index < pixels.Length; index++) {
                float value = float.IsFinite(depth[index]) ? Math.Clamp(depth[index], 0f, 1f) : 0f;
                pixels[index] = (byte)Math.Round(value * byte.MaxValue);
            }
            SaveAlpha(pixels, width, height, path);
        }

        private static void SaveAlpha(byte[] alpha, int width, int height, string path) {
            using var image = new Mat(height, width, MatType.CV_8UC1);
            Marshal.Copy(alpha, 0, image.Data, alpha.Length);
            if (!Cv2.ImWrite(path, image))
                throw new IOException($"Failed to save image: {path}");
        }

        private static Mat CreateBgrMat(byte[] pixels, int width, int height) {
            if (pixels.Length != checked(width * height * 3))
                throw new ArgumentException("BGR pixel dimensions are invalid.", nameof(pixels));
            var image = new Mat(height, width, MatType.CV_8UC3);
            Marshal.Copy(pixels, 0, image.Data, pixels.Length);
            return image;
        }

        internal static void SaveBackgroundDepth(
            float[] depth,
            byte[] removalMask,
            int width,
            int height,
            string path) {

            const int maximumRepairSide = 1024;
            const double fullResolutionRadius = 7d;

            if (depth.Length != checked(width * height) || removalMask.Length != depth.Length)
                throw new ArgumentException("Background depth inputs have invalid dimensions.");

            var depthPixels = new byte[depth.Length];
            for (int index = 0; index < depthPixels.Length; index++) {
                float value = float.IsFinite(depth[index]) ? Math.Clamp(depth[index], 0f, 1f) : 0f;
                depthPixels[index] = (byte)Math.Round(value * byte.MaxValue);
            }

            using var depthImage = new Mat(height, width, MatType.CV_8UC1);
            using var maskImage = new Mat(height, width, MatType.CV_8UC1);
            Marshal.Copy(depthPixels, 0, depthImage.Data, depthPixels.Length);
            Marshal.Copy(removalMask, 0, maskImage.Data, removalMask.Length);

            if (!removalMask.Any(value => value != 0)) {
                if (!Cv2.ImWrite(path, depthImage))
                    throw new IOException($"Failed to save image: {path}");
                return;
            }

            double repairScale = Math.Min(
                1d,
                maximumRepairSide / (double)Math.Max(width, height));
            using var repaired = depthImage.Clone();
            if (repairScale >= 1d) {
                Cv2.Inpaint(
                    depthImage,
                    maskImage,
                    repaired,
                    fullResolutionRadius,
                    InpaintMethod.Telea);
            }
            else {
                var repairSize = new Size(
                    Math.Max(1, (int)Math.Round(width * repairScale)),
                    Math.Max(1, (int)Math.Round(height * repairScale)));
                using var reducedDepth = new Mat();
                using var reducedMask = new Mat();
                using var reducedRepair = new Mat();
                using var fullResolutionRepair = new Mat();
                Cv2.Resize(depthImage, reducedDepth, repairSize, interpolation: InterpolationFlags.Area);
                Cv2.Resize(maskImage, reducedMask, repairSize, interpolation: InterpolationFlags.Nearest);
                Cv2.Inpaint(
                    reducedDepth,
                    reducedMask,
                    reducedRepair,
                    Math.Max(2d, fullResolutionRadius * repairScale),
                    InpaintMethod.Telea);
                Cv2.Resize(
                    reducedRepair,
                    fullResolutionRepair,
                    new Size(width, height),
                    interpolation: InterpolationFlags.Linear);
                fullResolutionRepair.CopyTo(repaired, maskImage);
            }

            if (!Cv2.ImWrite(path, repaired))
                throw new IOException($"Failed to save image: {path}");
        }

        private static unsafe void SaveCutout(Mat source, byte[] alpha, string path) {
            using var output = new Mat(source.Height, source.Width, MatType.CV_8UC4);
            byte* sourcePointer = (byte*)source.Data;
            byte* outputPointer = (byte*)output.Data;
            int sourceStride = (int)source.Step();
            int outputStride = (int)output.Step();

            for (int y = 0; y < source.Height; y++) {
                byte* sourceRow = sourcePointer + y * sourceStride;
                byte* outputRow = outputPointer + y * outputStride;
                int alphaOffset = y * source.Width;
                for (int x = 0; x < source.Width; x++) {
                    int sourceOffset = x * 3;
                    int outputOffset = x * 4;
                    byte opacity = alpha[alphaOffset + x];
                    outputRow[outputOffset] = opacity == 0 ? byte.MinValue : sourceRow[sourceOffset];
                    outputRow[outputOffset + 1] = opacity == 0 ? byte.MinValue : sourceRow[sourceOffset + 1];
                    outputRow[outputOffset + 2] = opacity == 0 ? byte.MinValue : sourceRow[sourceOffset + 2];
                    outputRow[outputOffset + 3] = opacity;
                }
            }

            if (!Cv2.ImWrite(path, output))
                throw new IOException($"Failed to save image: {path}");
        }

        private static unsafe void SaveLayerPreview(
            Mat source,
            IReadOnlyList<DynamicImageLayer> layers,
            string path) {

            using var preview = source.Clone();
            byte* pointer = (byte*)preview.Data;
            int stride = (int)preview.Step();
            const double sourceWeight = 0.55;
            const double layerWeight = 1 - sourceWeight;

            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++) {
                DynamicImageLayer layer = layers[layerIndex];
                Scalar color = PreviewColors[layerIndex % PreviewColors.Length];
                for (int y = 0; y < preview.Height; y++) {
                    byte* row = pointer + y * stride;
                    int alphaOffset = y * preview.Width;
                    for (int x = 0; x < preview.Width; x++) {
                        if (layer.VisibleAlpha[alphaOffset + x] == 0)
                            continue;
                        int offset = x * 3;
                        row[offset] = Blend(row[offset], color.Val0, sourceWeight, layerWeight);
                        row[offset + 1] = Blend(row[offset + 1], color.Val1, sourceWeight, layerWeight);
                        row[offset + 2] = Blend(row[offset + 2], color.Val2, sourceWeight, layerWeight);
                    }
                }
            }

            if (!Cv2.ImWrite(path, preview))
                throw new IOException($"Failed to save image: {path}");
        }

        private static byte Blend(byte source, double color, double sourceWeight, double layerWeight) =>
            (byte)Math.Clamp(Math.Round(source * sourceWeight + color * layerWeight), 0, byte.MaxValue);

        private static string SanitizeFileName(string value) {
            char[] invalid = Path.GetInvalidFileNameChars();
            string result = new(value
                .Select(character => invalid.Contains(character) ? '_' : character)
                .ToArray());
            result = result.Trim().TrimEnd('.');
            return string.IsNullOrWhiteSpace(result) ? "layer" : result;
        }

        private static ExportManifest CreateManifest(
            string imagePath,
            DynamicImageAnalysisResult analysis,
            string root,
            string depthPath,
            string rawDepthPath,
            string backgroundPlatePath,
            string backgroundDepthPath,
            string inpaintingPath,
            string appliedInpaintingPath,
            string previewPath,
            IReadOnlyList<DynamicImageLayerExport> exports) {

            DynamicImageLayerPlan plan = analysis.LayerPlan;
            var exportLookup = exports.ToDictionary(item => item.LayerId, StringComparer.Ordinal);
            return new ExportManifest(
                Path.GetFullPath(imagePath),
                plan.Width,
                plan.Height,
                plan.SceneKind.ToString(),
                plan.RequiresBackgroundReconstruction,
                plan.BackgroundDepthThreshold,
                plan.ForegroundDepthThreshold,
                new ExportTiming(
                    analysis.Timing.Detection.TotalMilliseconds,
                    analysis.Timing.Segmentation.TotalMilliseconds,
                    analysis.Timing.DepthEstimation.TotalMilliseconds,
                    analysis.Timing.LayerFusion.TotalMilliseconds,
                    analysis.Timing.BackgroundInpainting.TotalMilliseconds,
                    analysis.Timing.Total.TotalMilliseconds),
                Path.GetRelativePath(root, depthPath),
                Path.GetRelativePath(root, rawDepthPath),
                Path.GetRelativePath(root, backgroundPlatePath),
                Path.GetRelativePath(root, backgroundDepthPath),
                Path.GetRelativePath(root, inpaintingPath),
                Path.GetRelativePath(root, appliedInpaintingPath),
                analysis.BackgroundPlate.SafetyMarginPixels,
                Path.GetRelativePath(root, previewPath),
                plan.Layers.Select((layer, index) => {
                    DynamicImageLayerExport paths = exportLookup[layer.Id];
                    return new ExportLayer(
                        index,
                        layer.Id,
                        layer.Kind.ToString(),
                        layer.DepthBand.ToString(),
                        layer.Depth,
                        layer.Detection is null
                            ? null
                            : new ExportDetection(
                                layer.Detection.LabelId,
                                layer.Detection.Label,
                                layer.Detection.Score,
                                layer.Detection.Left,
                                layer.Detection.Top,
                                layer.Detection.Right,
                                layer.Detection.Bottom),
                        layer.SegmentationIoU,
                        layer.SubjectSalience,
                        layer.SubjectRole?.ToString(),
                        layer.OccludedByLayerIds,
                        Path.GetRelativePath(root, paths.SourceAlphaPath),
                        Path.GetRelativePath(root, paths.VisibleAlphaPath),
                        Path.GetRelativePath(root, paths.SourceCutoutPath),
                        Path.GetRelativePath(root, paths.VisibleCutoutPath));
                }).ToArray(),
                plan.Subjects.Select(subject => new ExportSubject(
                    new ExportDetection(
                        subject.Detection.LabelId,
                        subject.Detection.Label,
                        subject.Detection.Score,
                        subject.Detection.Left,
                        subject.Detection.Top,
                        subject.Detection.Right,
                        subject.Detection.Bottom),
                    subject.SegmentationIoU,
                    subject.Depth,
                    subject.Salience,
                    subject.Role.ToString(),
                    subject.IsIndependent)).ToArray(),
                plan.Occlusions);
        }

        internal sealed record ExportManifest(
            string SourceImage,
            int Width,
            int Height,
            string SceneKind,
            bool RequiresBackgroundReconstruction,
            float BackgroundDepthThreshold,
            float ForegroundDepthThreshold,
            ExportTiming TimingMilliseconds,
            string DepthMap,
            string RawDepthMap,
            string BackgroundPlate,
            string BackgroundDepthMap,
            string InpaintingMask,
            string AppliedInpaintingMask,
            int InpaintingSafetyMarginPixels,
            string LayerOrderPreview,
            IReadOnlyList<ExportLayer> Layers,
            IReadOnlyList<ExportSubject> Subjects,
            IReadOnlyList<LayerOcclusion> Occlusions);

        internal sealed record ExportTiming(
            double Detection,
            double Segmentation,
            double DepthEstimation,
            double LayerFusion,
            double BackgroundInpainting,
            double Total);

        internal sealed record ExportLayer(
            int BackToFrontIndex,
            string Id,
            string Kind,
            string DepthBand,
            LayerDepthStatistics Depth,
            ExportDetection? Detection,
            float? SegmentationIoU,
            float? SubjectSalience,
            string? SubjectRole,
            IReadOnlyList<string> OccludedByLayerIds,
            string SourceAlpha,
            string VisibleAlpha,
            string SourceCutout,
            string VisibleCutout);

        internal sealed record ExportSubject(
            ExportDetection Detection,
            float SegmentationIoU,
            LayerDepthStatistics Depth,
            float Salience,
            string Role,
            bool IsIndependent);

        internal sealed record ExportDetection(
            int LabelId,
            string Label,
            float Score,
            float Left,
            float Top,
            float Right,
            float Bottom);

        [JsonSerializable(typeof(ExportManifest))]
        [JsonSourceGenerationOptions(
            PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
            WriteIndented = true)]
        internal sealed partial class ManifestJsonContext : JsonSerializerContext;
    }
}
