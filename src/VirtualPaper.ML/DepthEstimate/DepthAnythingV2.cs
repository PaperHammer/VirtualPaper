using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.ML.DepthEstimate.Interfaces;
using VirtualPaper.ML.DepthEstimate.Models;

namespace VirtualPaper.ML.DepthEstimate {
    /// <summary>
    /// Depth Anything V2 ViT-S relative-depth inference using a dynamic FP32
    /// ONNX model. Larger normalized values represent image regions estimated
    /// to be closer to the camera.
    /// </summary>
    public sealed class DepthAnythingV2 : IDepthEstimate {
        private const string PreferredInputName = "image";
        private const string PreferredOutputName = "depth";
        private const int ModelStride = 14;

        private static readonly float[] Mean = [0.485f, 0.456f, 0.406f];
        private static readonly float[] Std = [0.229f, 0.224f, 0.225f];

        private readonly SemaphoreSlim _runGate = new(1, 1);
        private InferenceSession? _session;
        private string _inputName = string.Empty;
        private string _outputName = string.Empty;
        private bool _isLoaded;
        private bool _isDisposed;

        public string ModelPath { get; private set; } = null!;

        public void LoadModel(string? path = null) {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _runGate.Wait();

            try {
                ObjectDisposedException.ThrowIf(_isDisposed, this);
                if (_isLoaded) {
                    ArcLog.GetLogger<DepthAnythingV2>().Info(
                        "Depth Anything V2 model already loaded, skipping.");
                    return;
                }

                ModelPath = path ?? Path.Combine(
                    Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..")),
                    Constants.WorkingDir.ML_DepthEstimate_AI_Models,
                    Utils.Fields.DepthAnythingV2ModelName);

                if (!File.Exists(ModelPath))
                    throw new FileNotFoundException(
                        $"Depth Anything V2 model file not found: {ModelPath}",
                        ModelPath);

                using var sessionOptions = new SessionOptions {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    EnableCpuMemArena = false,
                    EnableMemoryPattern = false,
                    IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount),
                    InterOpNumThreads = 1
                };

                InferenceSession? session = null;
                try {
                    session = new InferenceSession(ModelPath, sessionOptions);
                    _inputName = session.InputMetadata.ContainsKey(PreferredInputName)
                        ? PreferredInputName
                        : session.InputMetadata.Keys.Single();
                    _outputName = session.OutputMetadata.ContainsKey(PreferredOutputName)
                        ? PreferredOutputName
                        : session.OutputMetadata.Keys.Single();
                    ValidateMetadata(session, _inputName, _outputName);
                }
                catch {
                    session?.Dispose();
                    throw;
                }

                _session = session;
                _isLoaded = true;
                ArcLog.GetLogger<DepthAnythingV2>().Info(
                    $"Depth Anything V2 loaded. Input: {_inputName}; output: {_outputName}");
            }
            finally {
                _runGate.Release();
            }
        }

        public DepthEstimateModelOutput Run(string imagePath) =>
            Run(imagePath, options: null, CancellationToken.None);

        public DepthEstimateModelOutput Run(
            string imagePath,
            DepthAnythingOptions? options,
            CancellationToken ct = default) {

            ObjectDisposedException.ThrowIf(_isDisposed, this);
            ct.ThrowIfCancellationRequested();
            options ??= new DepthAnythingOptions();
            options.Validate();

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                throw new FileNotFoundException($"Input image not found: {imagePath}", imagePath);

            _runGate.Wait(ct);
            try {
                ObjectDisposedException.ThrowIf(_isDisposed, this);
                if (_session == null)
                    throw new InvalidOperationException(
                        "ONNX Session is not initialized. Call LoadModel first.");

                using var image = Cv2.ImRead(imagePath, ImreadModes.Color);
                if (image.Empty())
                    throw new ArgumentException($"Failed to load image: {imagePath}", nameof(imagePath));

                (int inputWidth, int inputHeight) = CalculateInputSize(
                    image.Width,
                    image.Height,
                    options);
                DenseTensor<float> inputTensor = CreateInputTensor(image, inputWidth, inputHeight);

                var inputs = new List<NamedOnnxValue>(1) {
                    NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
                };

                using var runOptions = new RunOptions();
                using var cancellationRegistration = ct.Register(() => runOptions.Terminate = true);

                IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results;
                try {
                    results = _session.Run(inputs, [_outputName], runOptions);
                }
                catch (OnnxRuntimeException) when (ct.IsCancellationRequested) {
                    throw new OperationCanceledException(ct);
                }

                using (results) {
                    Tensor<float> depthTensor = results.Single().AsTensor<float>();
                    ValidateOutputShape(depthTensor, inputWidth, inputHeight);

                    float[] normalized = NormalizeOutput(depthTensor.ToArray());
                    float[] originalSizeDepth = ResizeDepth(
                        normalized,
                        inputWidth,
                        inputHeight,
                        image.Width,
                        image.Height);

                    return new DepthEstimateModelOutput(
                        originalSizeDepth,
                        image.Width,
                        image.Height,
                        image.Width,
                        image.Height);
                }
            }
            finally {
                _runGate.Release();
            }
        }

        public string SaveDepthMap(DepthEstimateModelOutput modelOutput, string outputFolder) {
            ArgumentNullException.ThrowIfNull(modelOutput);
            if (modelOutput.Width <= 0 || modelOutput.Height <= 0 ||
                modelOutput.Depth.Length != checked(modelOutput.Width * modelOutput.Height)) {
                throw new ArgumentException("Depth output dimensions are invalid.", nameof(modelOutput));
            }

            if (string.IsNullOrWhiteSpace(outputFolder))
                throw new ArgumentException("Output folder cannot be empty.", nameof(outputFolder));
            Directory.CreateDirectory(outputFolder);

            string outputPath = Path.Combine(
                outputFolder,
                Utils.Fields.DepthAnythingV2OutputFileName);
            var pixels = new byte[modelOutput.Depth.Length];
            for (int index = 0; index < pixels.Length; index++) {
                float value = float.IsFinite(modelOutput.Depth[index])
                    ? Math.Clamp(modelOutput.Depth[index], 0f, 1f)
                    : 0f;
                pixels[index] = (byte)Math.Round(value * byte.MaxValue);
            }

            using var depthImage = new Mat(
                modelOutput.Height,
                modelOutput.Width,
                MatType.CV_8UC1);
            Marshal.Copy(pixels, 0, depthImage.Data, pixels.Length);

            if (!Cv2.ImWrite(outputPath, depthImage))
                throw new IOException($"Failed to save depth map: {outputPath}");
            return outputPath;
        }

        internal static (int Width, int Height) CalculateInputSize(
            int originalWidth,
            int originalHeight,
            DepthAnythingOptions options) {

            if (originalWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(originalWidth));
            if (originalHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(originalHeight));
            ArgumentNullException.ThrowIfNull(options);
            options.Validate();

            if (options.ResizeMode == DepthAnythingResizeMode.StretchSquare)
                return (options.InputSize, options.InputSize);

            float scale = options.ResizeMode == DepthAnythingResizeMode.FillShortestSide
                ? Math.Max(
                    (float)options.InputSize / originalWidth,
                    (float)options.InputSize / originalHeight)
                : Math.Min(
                    (float)options.InputSize / originalWidth,
                    (float)options.InputSize / originalHeight);

            int width = RoundToStride(originalWidth * scale);
            int height = RoundToStride(originalHeight * scale);
            return (width, height);
        }

        internal static float[] NormalizeOutput(float[] data) {
            ArgumentNullException.ThrowIfNull(data);
            if (data.Length == 0)
                return [];

            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            foreach (float value in data) {
                if (!float.IsFinite(value))
                    continue;
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }

            var normalized = new float[data.Length];
            float range = max - min;
            if (!float.IsFinite(range) || range <= float.Epsilon)
                return normalized;

            for (int index = 0; index < data.Length; index++) {
                float value = data[index];
                normalized[index] = float.IsFinite(value)
                    ? Math.Clamp((value - min) / range, 0f, 1f)
                    : 0f;
            }

            return normalized;
        }

        private static DenseTensor<float> CreateInputTensor(
            Mat bgrImage,
            int inputWidth,
            int inputHeight) {

            using var resized = new Mat();
            Cv2.Resize(
                bgrImage,
                resized,
                new Size(inputWidth, inputHeight),
                0,
                0,
                InterpolationFlags.Cubic);

            var tensor = new DenseTensor<float>([1, 3, inputHeight, inputWidth]);
            Span<float> buffer = tensor.Buffer.Span;
            int channelSize = inputWidth * inputHeight;

            unsafe {
                byte* imagePtr = (byte*)resized.Data;
                int stride = (int)resized.Step();

                for (int y = 0; y < inputHeight; y++) {
                    byte* row = imagePtr + y * stride;
                    int rowOffset = y * inputWidth;

                    for (int x = 0; x < inputWidth; x++) {
                        int pixelOffset = x * 3;
                        int tensorOffset = rowOffset + x;

                        float red = row[pixelOffset + 2] / 255f;
                        float green = row[pixelOffset + 1] / 255f;
                        float blue = row[pixelOffset] / 255f;
                        buffer[tensorOffset] = (red - Mean[0]) / Std[0];
                        buffer[channelSize + tensorOffset] = (green - Mean[1]) / Std[1];
                        buffer[2 * channelSize + tensorOffset] = (blue - Mean[2]) / Std[2];
                    }
                }
            }

            return tensor;
        }

        private static float[] ResizeDepth(
            float[] depth,
            int inputWidth,
            int inputHeight,
            int outputWidth,
            int outputHeight) {

            using var input = new Mat(inputHeight, inputWidth, MatType.CV_32FC1);
            Marshal.Copy(depth, 0, input.Data, depth.Length);
            using var output = new Mat();
            Cv2.Resize(
                input,
                output,
                new Size(outputWidth, outputHeight),
                0,
                0,
                InterpolationFlags.Cubic);

            var result = new float[checked(outputWidth * outputHeight)];
            Marshal.Copy(output.Data, result, 0, result.Length);
            for (int index = 0; index < result.Length; index++)
                result[index] = float.IsFinite(result[index])
                    ? Math.Clamp(result[index], 0f, 1f)
                    : 0f;
            return result;
        }

        private static int RoundToStride(float value) =>
            Math.Max(ModelStride, (int)Math.Round(value / ModelStride) * ModelStride);

        private static void ValidateMetadata(
            InferenceSession session,
            string inputName,
            string outputName) {

            NodeMetadata input = session.InputMetadata[inputName];
            NodeMetadata output = session.OutputMetadata[outputName];
            if (input.Dimensions.Length != 4)
                throw new InvalidDataException(
                    $"Unexpected Depth Anything input rank: {input.Dimensions.Length}.");
            if (output.Dimensions.Length != 3)
                throw new InvalidDataException(
                    $"Unexpected Depth Anything output rank: {output.Dimensions.Length}.");
        }

        private static void ValidateOutputShape(
            Tensor<float> output,
            int inputWidth,
            int inputHeight) {

            if (output.Rank != 3 || output.Dimensions[0] != 1 ||
                output.Dimensions[1] != inputHeight || output.Dimensions[2] != inputWidth) {
                throw new InvalidDataException(
                    $"Unexpected Depth Anything output shape: " +
                    $"[{string.Join(", ", output.Dimensions.ToArray())}].");
            }
        }

        private void Dispose(bool disposing) {
            if (_isDisposed)
                return;

            if (disposing) {
                _runGate.Wait();
                try {
                    if (_isDisposed)
                        return;

                    _session?.Dispose();
                    _session = null;
                    _isLoaded = false;
                    _isDisposed = true;
                }
                finally {
                    _runGate.Release();
                }
            }
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
