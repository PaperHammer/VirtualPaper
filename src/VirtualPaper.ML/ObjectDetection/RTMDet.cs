using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.ML.ObjectDetection.Interfaces;
using VirtualPaper.ML.ObjectDetection.Models;

namespace VirtualPaper.ML.ObjectDetection {
    /// <summary>
    /// RTMDet-Tiny COCO object detector exported by MMDeploy as dynamic FP32 ONNX.
    /// The model contains decode and NMS; this class owns image preprocessing and
    /// restores the returned boxes to the original image coordinate system.
    /// </summary>
    public sealed class RTMDet : IObjectDetector {
        private const string PreferredInputName = "input";
        private const string DetectionsOutputName = "dets";
        private const string LabelsOutputName = "labels";
        private static readonly string[] OutputNames = [DetectionsOutputName, LabelsOutputName];

        // Values are taken from the generated model_metadata/pipeline.json.
        private static readonly float[] Mean = [103.53f, 116.28f, 123.675f];
        private static readonly float[] Std = [57.375f, 57.12f, 58.395f];

        private InferenceSession? _session;
        private string _inputName = string.Empty;
        private bool _isLoaded;
        private bool _isDisposed;

        public string ModelPath { get; private set; } = null!;

        public void LoadModel(string? path = null) {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            if (_isLoaded) {
                ArcLog.GetLogger<RTMDet>().Info("RTMDet model already loaded, skipping.");
                return;
            }

            ModelPath = path ?? Path.Combine(
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..")),
                Constants.WorkingDir.ML_ObjectDetection_AI_Models,
                Utils.Fields.ModelName);

            if (!File.Exists(ModelPath))
                throw new FileNotFoundException($"RTMDet model file not found: {ModelPath}", ModelPath);

            _session?.Dispose();
            using var options = new SessionOptions {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                EnableCpuMemArena = false,
                EnableMemoryPattern = false,
                IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount),
                InterOpNumThreads = 1
            };

            _session = new InferenceSession(ModelPath, options);

            _inputName = _session.InputMetadata.ContainsKey(PreferredInputName)
                ? PreferredInputName
                : _session.InputMetadata.Keys.Single();

            if (!_session.OutputMetadata.ContainsKey(DetectionsOutputName) ||
                !_session.OutputMetadata.ContainsKey(LabelsOutputName)) {
                _session.Dispose();
                _session = null;
                throw new InvalidDataException(
                    $"Unexpected RTMDet outputs. Expected '{DetectionsOutputName}' and '{LabelsOutputName}'.");
            }

            _isLoaded = true;
            ArcLog.GetLogger<RTMDet>().Info(
                $"RTMDet loaded. Input: {_inputName}; model version: {_session.ModelMetadata.Version}");
        }

        public ObjectDetectionModelOutput Run(
            string imagePath,
            ObjectDetectionOptions? options = null,
            CancellationToken ct = default) {

            ObjectDisposedException.ThrowIf(_isDisposed, this);
            ct.ThrowIfCancellationRequested();

            if (_session == null)
                throw new InvalidOperationException("ONNX Session is not initialized. Call LoadModel first.");
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                throw new FileNotFoundException($"Input image not found: {imagePath}", imagePath);

            options ??= new ObjectDetectionOptions();
            options.Validate();

            using var image = Cv2.ImRead(imagePath, ImreadModes.Color);
            if (image.Empty())
                throw new ArgumentException($"Failed to load image: {imagePath}", nameof(imagePath));

            int originalWidth = image.Width;
            int originalHeight = image.Height;

            float uniformScale = Math.Min(
                (float)options.InputWidth / originalWidth,
                (float)options.InputHeight / originalHeight);

            int resizedWidth = Math.Clamp(
                (int)Math.Round(originalWidth * uniformScale, MidpointRounding.AwayFromZero),
                1,
                options.InputWidth);
            int resizedHeight = Math.Clamp(
                (int)Math.Round(originalHeight * uniformScale, MidpointRounding.AwayFromZero),
                1,
                options.InputHeight);

            float scaleX = (float)resizedWidth / originalWidth;
            float scaleY = (float)resizedHeight / originalHeight;

            using var resized = new Mat();
            Cv2.Resize(
                image,
                resized,
                new Size(resizedWidth, resizedHeight),
                0,
                0,
                InterpolationFlags.Linear);

            // MMDetection Pad places the resized image at the top-left and pads
            // the right/bottom with BGR(114,114,114); it is not centered letterbox.
            using var padded = new Mat(
                options.InputHeight,
                options.InputWidth,
                MatType.CV_8UC3,
                new Scalar(114, 114, 114));
            using (var roi = new Mat(padded, new Rect(0, 0, resizedWidth, resizedHeight))) {
                resized.CopyTo(roi);
            }

            var inputTensor = CreateInputTensor(padded);
            var inputs = new List<NamedOnnxValue>(1) {
                NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
            };

            using var runOptions = new RunOptions();
            using var cancellationRegistration = ct.Register(() => runOptions.Terminate = true);

            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results;
            try {
                results = _session.Run(inputs, OutputNames, runOptions);
            }
            catch (OnnxRuntimeException) when (ct.IsCancellationRequested) {
                throw new OperationCanceledException(ct);
            }

            using (results) {
                Tensor<float> dets = results
                    .Single(result => result.Name == DetectionsOutputName)
                    .AsTensor<float>();
                Tensor<long> labels = results
                    .Single(result => result.Name == LabelsOutputName)
                    .AsTensor<long>();

                ValidateOutputShape(dets, labels);

                int detectionCount = dets.Dimensions[1];
                var detections = new List<DetectedObject>(detectionCount);

                for (int index = 0; index < detectionCount; index++) {
                    ct.ThrowIfCancellationRequested();

                    long rawLabel = labels[0, index];
                    float score = dets[0, index, 4];

                    if (rawLabel < 0 || rawLabel > int.MaxValue ||
                        !float.IsFinite(score) || score < options.ScoreThreshold) {
                        continue;
                    }

                    float inputLeft = dets[0, index, 0];
                    float inputTop = dets[0, index, 1];
                    float inputRight = dets[0, index, 2];
                    float inputBottom = dets[0, index, 3];

                    if (!float.IsFinite(inputLeft) || !float.IsFinite(inputTop) ||
                        !float.IsFinite(inputRight) || !float.IsFinite(inputBottom)) {
                        continue;
                    }

                    // Clip to the resized content, excluding right/bottom padding,
                    // then map the box back to the original image.
                    float left = Math.Clamp(inputLeft, 0f, resizedWidth) / scaleX;
                    float top = Math.Clamp(inputTop, 0f, resizedHeight) / scaleY;
                    float right = Math.Clamp(inputRight, 0f, resizedWidth) / scaleX;
                    float bottom = Math.Clamp(inputBottom, 0f, resizedHeight) / scaleY;

                    left = Math.Clamp(left, 0f, originalWidth);
                    top = Math.Clamp(top, 0f, originalHeight);
                    right = Math.Clamp(right, 0f, originalWidth);
                    bottom = Math.Clamp(bottom, 0f, originalHeight);

                    if (right <= left || bottom <= top)
                        continue;

                    int labelId = (int)rawLabel;
                    detections.Add(new DetectedObject(
                        labelId,
                        Utils.GetLabelName(labelId),
                        score,
                        left,
                        top,
                        right,
                        bottom));
                }

                detections.Sort(static (left, right) => right.Score.CompareTo(left.Score));

                return new ObjectDetectionModelOutput(
                    detections,
                    originalWidth,
                    originalHeight,
                    options.InputWidth,
                    options.InputHeight,
                    resizedWidth,
                    resizedHeight,
                    scaleX,
                    scaleY);
            }
        }

        private static DenseTensor<float> CreateInputTensor(Mat paddedImage) {
            int height = paddedImage.Height;
            int width = paddedImage.Width;
            int channelSize = height * width;
            var tensor = new DenseTensor<float>([1, 3, height, width]);
            Span<float> buffer = tensor.Buffer.Span;

            unsafe {
                byte* imagePtr = (byte*)paddedImage.Data;
                int stride = (int)paddedImage.Step();

                for (int y = 0; y < height; y++) {
                    byte* row = imagePtr + y * stride;
                    int rowOffset = y * width;

                    for (int x = 0; x < width; x++) {
                        int pixelOffset = x * 3;
                        int tensorOffset = rowOffset + x;

                        // pipeline.json has to_rgb=false, so channels remain BGR.
                        buffer[tensorOffset] = (row[pixelOffset] - Mean[0]) / Std[0];
                        buffer[channelSize + tensorOffset] = (row[pixelOffset + 1] - Mean[1]) / Std[1];
                        buffer[2 * channelSize + tensorOffset] = (row[pixelOffset + 2] - Mean[2]) / Std[2];
                    }
                }
            }

            return tensor;
        }

        private static void ValidateOutputShape(Tensor<float> dets, Tensor<long> labels) {
            if (dets.Rank != 3 || dets.Dimensions[0] != 1 || dets.Dimensions[2] != 5)
                throw new InvalidDataException(
                    $"Unexpected dets shape: [{string.Join(", ", dets.Dimensions.ToArray())}].");

            if (labels.Rank != 2 || labels.Dimensions[0] != 1 ||
                labels.Dimensions[1] != dets.Dimensions[1]) {
                throw new InvalidDataException(
                    $"Unexpected labels shape: [{string.Join(", ", labels.Dimensions.ToArray())}].");
            }
        }

        private void Dispose(bool disposing) {
            if (_isDisposed)
                return;

            if (disposing) {
                _session?.Dispose();
                _session = null;
                _isLoaded = false;
            }

            _isDisposed = true;
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
