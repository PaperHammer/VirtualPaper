using System.Diagnostics;
using VirtualPaper.ML.DepthEstimate;
using VirtualPaper.ML.DepthEstimate.Models;
using VirtualPaper.ML.DynamicImage.Models;
using VirtualPaper.ML.ObjectDetection;
using VirtualPaper.ML.ObjectDetection.Models;
using VirtualPaper.ML.Segmentation;
using VirtualPaper.ML.Segmentation.Models;
using VirtualPaper.ML.Inpainting;
using VirtualPaper.ML.Inpainting.Models;

namespace VirtualPaper.ML.DynamicImage {
    /// <summary>
    /// Runs RTMDet, MobileSAM and Depth Anything V2 and turns their outputs into
    /// a render-ready layer plan.
    /// </summary>
    public sealed class DynamicImageAnalyzer : IDisposable {
        private readonly SemaphoreSlim _runGate = new(1, 1);
        private readonly RTMDet _detector = new();
        private readonly MobileSam _segmenter = new();
        private readonly DepthAnythingV2 _depthEstimator = new();
        private readonly MIGan _inpainter = new();
        private bool _isLoaded;
        private bool _isDisposed;

        public void LoadModels(
            string? detectorPath = null,
            string? segmentationEncoderPath = null,
            string? segmentationDecoderPath = null,
            string? depthPath = null,
            string? inpaintingPath = null) {

            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _runGate.Wait();
            try {
                if (_isLoaded)
                    return;
                _detector.LoadModel(detectorPath);
                _segmenter.LoadModels(segmentationEncoderPath, segmentationDecoderPath);
                _depthEstimator.LoadModel(depthPath);
                _inpainter.LoadModel(inpaintingPath);
                _isLoaded = true;
            }
            finally {
                _runGate.Release();
            }
        }

        public DynamicImageAnalysisResult Analyze(
            string imagePath,
            DynamicImageAnalysisOptions? options = null,
            CancellationToken ct = default) {

            ObjectDisposedException.ThrowIf(_isDisposed, this);
            options ??= new DynamicImageAnalysisOptions();
            options.Validate();
            ct.ThrowIfCancellationRequested();

            _runGate.Wait(ct);
            try {
                if (!_isLoaded)
                    throw new InvalidOperationException("Models are not initialized. Call LoadModels first.");

                var stopwatch = Stopwatch.StartNew();
                ObjectDetectionModelOutput detection = _detector.Run(imagePath, options.Detection, ct);
                TimeSpan detectionTime = stopwatch.Elapsed;

                long totalPixels = checked((long)detection.OriginalWidth * detection.OriginalHeight);
                IReadOnlyList<DetectedObject> selected = detection.Detections
                    .Where(item => item.Width > 0 && item.Height > 0 &&
                        item.Width * item.Height / totalPixels >= options.Fusion.MinimumObjectAreaRatio)
                    .Take(options.Fusion.MaxObjects)
                    .ToArray();

                IReadOnlyList<SegmentationBox> boxes = selected
                    .Select(SegmentationBox.FromDetection)
                    .ToArray();
                stopwatch.Restart();
                SegmentationModelOutput segmentation = _segmenter.Run(
                    imagePath,
                    boxes,
                    options.Segmentation,
                    ct);
                if (options.MergeDuplicateObjects && segmentation.Masks.Count > 1) {
                    ObjectMaskConsolidator.ConsolidatedObjects consolidated =
                        ObjectMaskConsolidator.Consolidate(
                            selected,
                            segmentation.Masks,
                            options.DuplicateMaskContainmentThreshold);
                    selected = consolidated.Detections;
                    segmentation = new SegmentationModelOutput(
                        consolidated.Masks,
                        segmentation.OriginalWidth,
                        segmentation.OriginalHeight,
                        segmentation.EncoderInputSize,
                        segmentation.ResizedWidth,
                        segmentation.ResizedHeight,
                        segmentation.ScaleX,
                        segmentation.ScaleY);
                }
                TimeSpan segmentationTime = stopwatch.Elapsed;

                stopwatch.Restart();
                DepthEstimateModelOutput depth = _depthEstimator.Run(imagePath, options.Depth, ct);
                TimeSpan depthTime = stopwatch.Elapsed;

                stopwatch.Restart();
                DynamicImageLayerPlan layerPlan = LayerFusionEngine.Fuse(
                    depth,
                    selected,
                    segmentation.Masks,
                    options.Fusion,
                    ct);
                TimeSpan fusionTime = stopwatch.Elapsed;

                stopwatch.Restart();
                InpaintingModelOutput backgroundPlate = _inpainter.Run(
                    imagePath,
                    layerPlan.InpaintingMask,
                    layerPlan.Width,
                    layerPlan.Height,
                    options.Inpainting,
                    ct);
                TimeSpan inpaintingTime = stopwatch.Elapsed;

                return new DynamicImageAnalysisResult(
                    detection,
                    selected,
                    segmentation,
                    depth,
                    layerPlan,
                    backgroundPlate,
                    new DynamicImageAnalysisTiming(
                        detectionTime,
                        segmentationTime,
                        depthTime,
                        fusionTime,
                        inpaintingTime));
            }
            finally {
                _runGate.Release();
            }
        }

        public void Dispose() {
            if (_isDisposed)
                return;

            _runGate.Wait();
            try {
                if (_isDisposed)
                    return;
                _detector.Dispose();
                _segmenter.Dispose();
                _depthEstimator.Dispose();
                _inpainter.Dispose();
                _isLoaded = false;
                _isDisposed = true;
            }
            finally {
                _runGate.Release();
                _runGate.Dispose();
            }
            GC.SuppressFinalize(this);
        }
    }
}
