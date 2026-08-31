using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.ML.Inpainting.Models;

namespace VirtualPaper.ML.Inpainting {
    /// <summary>
    /// Official MI-GAN-512 Places2 ONNX pipeline. The pipeline accepts a
    /// uint8 RGB image and a uint8 mask where 255 means keep and 0 means fill.
    /// </summary>
    public sealed class MIGan : IDisposable {
        private readonly SemaphoreSlim _runGate = new(1, 1);
        private InferenceSession? _session;
        private string _imageInputName = string.Empty;
        private string _maskInputName = string.Empty;
        private string _outputName = string.Empty;
        private bool _isDisposed;

        public string ModelPath { get; private set; } = string.Empty;

        public void LoadModel(string? path = null) {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_session is not null)
                return;

            ModelPath = path ?? Path.Combine(
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..")),
                Constants.WorkingDir.ML,
                "Inpainting",
                "ai_models",
                Utils.Fields.ModelName);
            if (!File.Exists(ModelPath))
                throw new FileNotFoundException($"MI-GAN model file not found: {ModelPath}", ModelPath);

            using var options = new SessionOptions {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                EnableCpuMemArena = false,
                EnableMemoryPattern = false,
                IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount),
                InterOpNumThreads = 1
            };
            var session = new InferenceSession(ModelPath, options);
            try {
                (string Name, NodeMetadata Metadata)[] inputs = session.InputMetadata
                    .Select(item => (item.Key, item.Value))
                    .ToArray();
                if (inputs.Length != 2 || session.OutputMetadata.Count != 1)
                    throw new InvalidDataException("Unexpected MI-GAN input/output count.");

                (string Name, NodeMetadata Metadata) image = inputs.Single(item =>
                    item.Metadata.ElementType == typeof(byte) &&
                    item.Metadata.Dimensions.Length == 4 &&
                    item.Metadata.Dimensions[1] == 3);
                (string Name, NodeMetadata Metadata) mask = inputs.Single(item =>
                    item.Metadata.ElementType == typeof(byte) &&
                    item.Metadata.Dimensions.Length == 4 &&
                    item.Metadata.Dimensions[1] == 1);
                KeyValuePair<string, NodeMetadata> output = session.OutputMetadata.Single();
                if (output.Value.ElementType != typeof(byte) ||
                    output.Value.Dimensions.Length != 4 || output.Value.Dimensions[1] != 3) {
                    throw new InvalidDataException("Unexpected MI-GAN output metadata.");
                }

                _imageInputName = image.Name;
                _maskInputName = mask.Name;
                _outputName = output.Key;
                _session = session;
                ArcLog.GetLogger<MIGan>().Info(
                    $"MI-GAN loaded. Inputs: {_imageInputName}, {_maskInputName}; output: {_outputName}");
            }
            catch {
                session.Dispose();
                throw;
            }
        }

        public InpaintingModelOutput Run(
            string imagePath,
            byte[] inpaintingMask,
            int width,
            int height,
            MIGanOptions? options = null,
            CancellationToken ct = default) {

            ObjectDisposedException.ThrowIf(_isDisposed, this);
            options ??= new MIGanOptions();
            options.Validate();
            int pixelCount = checked(width * height);
            if (inpaintingMask.Length != pixelCount)
                throw new ArgumentException("Inpainting mask dimensions are invalid.", nameof(inpaintingMask));
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                throw new FileNotFoundException($"Input image not found: {imagePath}", imagePath);

            _runGate.Wait(ct);
            try {
                if (_session is null)
                    throw new InvalidOperationException("ONNX session is not initialized. Call LoadModel first.");
                using var image = Cv2.ImRead(imagePath, ImreadModes.Color);
                if (image.Empty() || image.Width != width || image.Height != height)
                    throw new ArgumentException("Input image dimensions do not match the mask.", nameof(imagePath));

                var interleavedBgr = new byte[checked(pixelCount * 3)];
                Marshal.Copy(image.Data, interleavedBgr, 0, interleavedBgr.Length);
                if (!inpaintingMask.Any(value => value != 0)) {
                    return new InpaintingModelOutput(interleavedBgr, width, height, false) {
                        AppliedMask = inpaintingMask.ToArray()
                    };
                }

                byte[] preparedMask = options.FillSmallEnclosedHoles
                    ? FillSmallEnclosedHoles(
                        inpaintingMask,
                        width,
                        height,
                        CalculateMaximumHoleArea(width, height, options))
                    : inpaintingMask.ToArray();
                int expansionPixels = CalculateExpansionPixels(width, height, options);
                byte[] expandedMask = ExpandMask(preparedMask, width, height, expansionPixels);
                var rgb = new byte[interleavedBgr.Length];
                var knownMask = new byte[pixelCount];
                for (int index = 0; index < pixelCount; index++) {
                    int offset = index * 3;
                    rgb[index] = interleavedBgr[offset + 2];
                    rgb[pixelCount + index] = interleavedBgr[offset + 1];
                    rgb[pixelCount * 2 + index] = interleavedBgr[offset];
                    knownMask[index] = expandedMask[index] == 0 ? byte.MaxValue : byte.MinValue;
                }

                var imageTensor = new DenseTensor<byte>(rgb, [1, 3, height, width]);
                var maskTensor = new DenseTensor<byte>(knownMask, [1, 1, height, width]);
                var inputs = new List<NamedOnnxValue> {
                    NamedOnnxValue.CreateFromTensor(_imageInputName, imageTensor),
                    NamedOnnxValue.CreateFromTensor(_maskInputName, maskTensor)
                };
                using var runOptions = new RunOptions();
                using var registration = ct.Register(() => runOptions.Terminate = true);
                try {
                    using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results =
                        _session.Run(inputs, [_outputName], runOptions);
                    Tensor<byte> output = results.Single().AsTensor<byte>();
                    if (output.Rank != 4 || output.Dimensions[0] != 1 ||
                        output.Dimensions[1] != 3 || output.Dimensions[2] != height ||
                        output.Dimensions[3] != width) {
                        throw new InvalidDataException(
                            $"Unexpected MI-GAN output shape: [{string.Join(", ", output.Dimensions.ToArray())}].");
                    }
                    var bgr = new byte[interleavedBgr.Length];
                    for (int index = 0; index < pixelCount; index++) {
                        int offset = index * 3;
                        bgr[offset] = output[0, 2, index / width, index % width];
                        bgr[offset + 1] = output[0, 1, index / width, index % width];
                        bgr[offset + 2] = output[0, 0, index / width, index % width];
                    }
                    return new InpaintingModelOutput(bgr, width, height, true) {
                        AppliedMask = expandedMask,
                        SafetyMarginPixels = expansionPixels
                    };
                }
                catch (OnnxRuntimeException) when (ct.IsCancellationRequested) {
                    throw new OperationCanceledException(ct);
                }
            }
            finally {
                _runGate.Release();
            }
        }

        internal static byte[] ExpandMask(byte[] source, int width, int height, int pixels) {
            if (pixels == 0)
                return source.ToArray();
            using var input = new Mat(height, width, MatType.CV_8UC1);
            Marshal.Copy(source, 0, input.Data, source.Length);
            using Mat kernel = Cv2.GetStructuringElement(
                MorphShapes.Ellipse,
                new Size(pixels * 2 + 1, pixels * 2 + 1));
            using var output = new Mat();
            Cv2.Dilate(input, output, kernel);
            var result = new byte[source.Length];
            Marshal.Copy(output.Data, result, 0, result.Length);
            return result;
        }

        internal static int CalculateExpansionPixels(
            int width,
            int height,
            MIGanOptions options) {

            int relative = (int)Math.Round(Math.Min(width, height) * options.MaskExpansionRatio);
            return Math.Clamp(
                Math.Max(options.MaskExpansionPixels, relative),
                options.MaskExpansionPixels,
                options.MaximumMaskExpansionPixels);
        }

        internal static int CalculateMaximumHoleArea(
            int width,
            int height,
            MIGanOptions options) {

            long pixelCount = checked((long)width * height);
            int relative = (int)Math.Min(
                int.MaxValue,
                Math.Round(pixelCount * options.MaximumHoleAreaRatio));
            return Math.Min(options.MaximumHoleAreaPixels, relative);
        }

        internal static byte[] FillSmallEnclosedHoles(
            byte[] source,
            int width,
            int height,
            int maximumAreaPixels) {

            if (maximumAreaPixels <= 0)
                return source.ToArray();
            int pixelCount = checked(width * height);
            if (source.Length != pixelCount)
                throw new ArgumentException("Mask dimensions are invalid.", nameof(source));

            using var input = new Mat(height, width, MatType.CV_8UC1);
            Marshal.Copy(source, 0, input.Data, source.Length);
            using var contourInput = input.Clone();
            Cv2.FindContours(
                contourInput,
                out Point[][] contours,
                out HierarchyIndex[] hierarchy,
                RetrievalModes.Tree,
                ContourApproximationModes.ApproxSimple);

            using var output = input.Clone();
            for (int index = 0; index < contours.Length; index++) {
                int depth = 0;
                int parent = hierarchy[index].Parent;
                while (parent >= 0) {
                    depth++;
                    parent = hierarchy[parent].Parent;
                }
                if ((depth & 1) == 0)
                    continue;

                double area = Math.Abs(Cv2.ContourArea(contours[index]));
                if (area <= maximumAreaPixels)
                    Cv2.DrawContours(output, contours, index, Scalar.White, thickness: -1);
            }

            var result = new byte[source.Length];
            Marshal.Copy(output.Data, result, 0, result.Length);
            return result;
        }

        public void Dispose() {
            if (_isDisposed)
                return;
            _runGate.Wait();
            try {
                _session?.Dispose();
                _session = null;
                _isDisposed = true;
            }
            finally {
                _runGate.Release();
                _runGate.Dispose();
            }
        }
    }
}
