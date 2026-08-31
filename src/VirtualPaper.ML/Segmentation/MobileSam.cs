using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.ML.Segmentation.Interfaces;
using VirtualPaper.ML.Segmentation.Models;

namespace VirtualPaper.ML.Segmentation {
    /// <summary>
    /// MobileSAM ViT-T image segmentation using separate FP32 ONNX image encoder
    /// and prompt/mask decoder sessions. One image embedding is reused for all
    /// box prompts in a single Run call.
    /// </summary>
    public sealed class MobileSam : IImageSegmenter {
        private const string EncoderInputName = "input_image";
        private const string EncoderOutputName = "image_embeddings";

        private const string DecoderEmbeddingInputName = "image_embeddings";
        private const string PointCoordinatesInputName = "point_coords";
        private const string PointLabelsInputName = "point_labels";
        private const string MaskInputName = "mask_input";
        private const string HasMaskInputName = "has_mask_input";
        private const string OriginalImageSizeInputName = "orig_im_size";

        private const string MasksOutputName = "masks";
        private const string IouOutputName = "iou_predictions";
        private const string LowResolutionMasksOutputName = "low_res_masks";

        private static readonly string[] EncoderOutputNames = [EncoderOutputName];
        private static readonly string[] DecoderOutputNames = [MasksOutputName, IouOutputName];
        private static readonly string[] RequiredDecoderInputs = [
            DecoderEmbeddingInputName,
            PointCoordinatesInputName,
            PointLabelsInputName,
            MaskInputName,
            HasMaskInputName,
            OriginalImageSizeInputName
        ];
        private static readonly string[] RequiredDecoderOutputs = [
            MasksOutputName,
            IouOutputName,
            LowResolutionMasksOutputName
        ];

        private readonly SemaphoreSlim _runGate = new(1, 1);
        private InferenceSession? _encoderSession;
        private InferenceSession? _decoderSession;
        private bool _isLoaded;
        private bool _isDisposed;

        public string EncoderModelPath { get; private set; } = null!;
        public string DecoderModelPath { get; private set; } = null!;

        public void LoadModels(string? encoderPath = null, string? decoderPath = null) {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _runGate.Wait();

            try {
                ObjectDisposedException.ThrowIf(_isDisposed, this);
                if (_isLoaded) {
                    ArcLog.GetLogger<MobileSam>().Info("MobileSAM models already loaded, skipping.");
                    return;
                }

                string modelDirectory = Path.Combine(
                    Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..")),
                    Constants.WorkingDir.ML,
                    "Segmentation",
                    "ai_models");

                EncoderModelPath = encoderPath ?? Path.Combine(
                    modelDirectory,
                    Utils.Fields.EncoderModelName);
                DecoderModelPath = decoderPath ?? Path.Combine(
                    modelDirectory,
                    Utils.Fields.DecoderModelName);

                if (!File.Exists(EncoderModelPath))
                    throw new FileNotFoundException(
                        $"MobileSAM encoder model file not found: {EncoderModelPath}",
                        EncoderModelPath);
                if (!File.Exists(DecoderModelPath))
                    throw new FileNotFoundException(
                        $"MobileSAM decoder model file not found: {DecoderModelPath}",
                        DecoderModelPath);

                InferenceSession? encoder = null;
                InferenceSession? decoder = null;

                try {
                    using (SessionOptions encoderOptions = CreateSessionOptions())
                        encoder = new InferenceSession(EncoderModelPath, encoderOptions);
                    using (SessionOptions decoderOptions = CreateSessionOptions())
                        decoder = new InferenceSession(DecoderModelPath, decoderOptions);

                    ValidateEncoderMetadata(encoder);
                    ValidateDecoderMetadata(decoder);
                }
                catch {
                    encoder?.Dispose();
                    decoder?.Dispose();
                    throw;
                }

                _encoderSession = encoder;
                _decoderSession = decoder;
                _isLoaded = true;

                ArcLog.GetLogger<MobileSam>().Info(
                    $"MobileSAM loaded. Encoder: {Path.GetFileName(EncoderModelPath)}; " +
                    $"decoder: {Path.GetFileName(DecoderModelPath)}");
            }
            finally {
                _runGate.Release();
            }
        }

        public SegmentationModelOutput Run(
            string imagePath,
            IReadOnlyList<SegmentationBox> boxes,
            MobileSamOptions? options = null,
            CancellationToken ct = default) {

            ObjectDisposedException.ThrowIf(_isDisposed, this);
            ArgumentNullException.ThrowIfNull(boxes);
            ct.ThrowIfCancellationRequested();

            options ??= new MobileSamOptions();
            options.Validate();

            if (boxes.Count > options.MaxBoxes)
                throw new ArgumentOutOfRangeException(
                    nameof(boxes),
                    boxes.Count,
                    $"Box count exceeds the configured maximum of {options.MaxBoxes}.");

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                throw new FileNotFoundException($"Input image not found: {imagePath}", imagePath);

            _runGate.Wait(ct);
            try {
                ObjectDisposedException.ThrowIf(_isDisposed, this);
                if (_encoderSession == null || _decoderSession == null)
                    throw new InvalidOperationException(
                        "ONNX sessions are not initialized. Call LoadModels first.");

                using var image = Cv2.ImRead(imagePath, ImreadModes.Color);
                if (image.Empty())
                    throw new ArgumentException($"Failed to load image: {imagePath}", nameof(imagePath));

                MobileSamImageInput imageInput = MobileSamPreprocessor.CreateInput(image);

                if (boxes.Count == 0) {
                    return new SegmentationModelOutput(
                        [],
                        imageInput.OriginalWidth,
                        imageInput.OriginalHeight,
                        MobileSamPreprocessor.InputSize,
                        imageInput.ResizedWidth,
                        imageInput.ResizedHeight,
                        imageInput.ScaleX,
                        imageInput.ScaleY);
                }

                using var runOptions = new RunOptions();
                using var cancellationRegistration = ct.Register(() => runOptions.Terminate = true);

                DenseTensor<float> imageEmbedding;
                try {
                    imageEmbedding = RunEncoder(_encoderSession, imageInput.Tensor, runOptions);
                }
                catch (OnnxRuntimeException) when (ct.IsCancellationRequested) {
                    throw new OperationCanceledException(ct);
                }

                var emptyMaskInput = new DenseTensor<float>([1, 1, 256, 256]);
                var hasMaskInput = new DenseTensor<float>([1]);
                var originalImageSize = new DenseTensor<float>([2]);
                originalImageSize[0] = imageInput.OriginalHeight;
                originalImageSize[1] = imageInput.OriginalWidth;

                var masks = new List<SegmentationMask>(boxes.Count);
                foreach (SegmentationBox rawBox in boxes) {
                    ct.ThrowIfCancellationRequested();
                    if (rawBox is null)
                        throw new ArgumentException("Box collection cannot contain null values.", nameof(boxes));

                    SegmentationBox box = rawBox.ClampAndValidate(
                        imageInput.OriginalWidth,
                        imageInput.OriginalHeight);

                    var pointCoordinates = new DenseTensor<float>([1, 2, 2]);
                    pointCoordinates[0, 0, 0] = box.Left * imageInput.ScaleX;
                    pointCoordinates[0, 0, 1] = box.Top * imageInput.ScaleY;
                    pointCoordinates[0, 1, 0] = box.Right * imageInput.ScaleX;
                    pointCoordinates[0, 1, 1] = box.Bottom * imageInput.ScaleY;

                    // SAM represents a box as two prompt points with labels 2 and 3.
                    var pointLabels = new DenseTensor<float>([1, 2]);
                    pointLabels[0, 0] = 2f;
                    pointLabels[0, 1] = 3f;

                    try {
                        masks.Add(RunDecoder(
                            _decoderSession,
                            imageEmbedding,
                            pointCoordinates,
                            pointLabels,
                            emptyMaskInput,
                            hasMaskInput,
                            originalImageSize,
                            box,
                            imageInput.OriginalWidth,
                            imageInput.OriginalHeight,
                            options.MaskThreshold,
                            runOptions));
                    }
                    catch (OnnxRuntimeException) when (ct.IsCancellationRequested) {
                        throw new OperationCanceledException(ct);
                    }
                }

                return new SegmentationModelOutput(
                    masks,
                    imageInput.OriginalWidth,
                    imageInput.OriginalHeight,
                    MobileSamPreprocessor.InputSize,
                    imageInput.ResizedWidth,
                    imageInput.ResizedHeight,
                    imageInput.ScaleX,
                    imageInput.ScaleY);
            }
            finally {
                _runGate.Release();
            }
        }

        private static DenseTensor<float> RunEncoder(
            InferenceSession session,
            DenseTensor<float> input,
            RunOptions runOptions) {

            var inputs = new List<NamedOnnxValue>(1) {
                NamedOnnxValue.CreateFromTensor(EncoderInputName, input)
            };

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results =
                session.Run(inputs, EncoderOutputNames, runOptions);
            Tensor<float> output = results.Single().AsTensor<float>();

            if (output.Rank != 4 || output.Dimensions[0] != 1 ||
                output.Dimensions[1] != 256 || output.Dimensions[2] != 64 ||
                output.Dimensions[3] != 64) {
                throw new InvalidDataException(
                    $"Unexpected MobileSAM encoder output shape: " +
                    $"[{string.Join(", ", output.Dimensions.ToArray())}].");
            }

            var embedding = new DenseTensor<float>([1, 256, 64, 64]);
            output.ToArray().AsSpan().CopyTo(embedding.Buffer.Span);
            return embedding;
        }

        private static SegmentationMask RunDecoder(
            InferenceSession session,
            DenseTensor<float> imageEmbedding,
            DenseTensor<float> pointCoordinates,
            DenseTensor<float> pointLabels,
            DenseTensor<float> maskInput,
            DenseTensor<float> hasMaskInput,
            DenseTensor<float> originalImageSize,
            SegmentationBox sourceBox,
            int imageWidth,
            int imageHeight,
            float maskThreshold,
            RunOptions runOptions) {

            var inputs = new List<NamedOnnxValue>(6) {
                NamedOnnxValue.CreateFromTensor(DecoderEmbeddingInputName, imageEmbedding),
                NamedOnnxValue.CreateFromTensor(PointCoordinatesInputName, pointCoordinates),
                NamedOnnxValue.CreateFromTensor(PointLabelsInputName, pointLabels),
                NamedOnnxValue.CreateFromTensor(MaskInputName, maskInput),
                NamedOnnxValue.CreateFromTensor(HasMaskInputName, hasMaskInput),
                NamedOnnxValue.CreateFromTensor(OriginalImageSizeInputName, originalImageSize)
            };

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results =
                session.Run(inputs, DecoderOutputNames, runOptions);

            Tensor<float> maskTensor = results
                .Single(result => result.Name == MasksOutputName)
                .AsTensor<float>();
            Tensor<float> iouTensor = results
                .Single(result => result.Name == IouOutputName)
                .AsTensor<float>();

            if (maskTensor.Rank != 4 || maskTensor.Dimensions[0] != 1 ||
                maskTensor.Dimensions[1] != 1 || maskTensor.Dimensions[2] != imageHeight ||
                maskTensor.Dimensions[3] != imageWidth) {
                throw new InvalidDataException(
                    $"Unexpected MobileSAM masks shape: " +
                    $"[{string.Join(", ", maskTensor.Dimensions.ToArray())}].");
            }

            if (iouTensor.Rank != 2 || iouTensor.Dimensions[0] != 1 ||
                iouTensor.Dimensions[1] != 1) {
                throw new InvalidDataException(
                    $"Unexpected MobileSAM IoU shape: " +
                    $"[{string.Join(", ", iouTensor.Dimensions.ToArray())}].");
            }

            float[] logits = maskTensor.ToArray();
            var alpha = new byte[logits.Length];
            for (int index = 0; index < logits.Length; index++)
                alpha[index] = logits[index] > maskThreshold ? byte.MaxValue : byte.MinValue;

            return new SegmentationMask(
                sourceBox,
                iouTensor[0, 0],
                alpha,
                imageWidth,
                imageHeight);
        }

        private static SessionOptions CreateSessionOptions() => new() {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            EnableCpuMemArena = false,
            EnableMemoryPattern = false,
            IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount),
            InterOpNumThreads = 1
        };

        private static void ValidateEncoderMetadata(InferenceSession encoder) {
            if (!encoder.InputMetadata.TryGetValue(EncoderInputName, out NodeMetadata? input) ||
                input.Dimensions.Length != 4 || input.Dimensions[0] != 1 ||
                input.Dimensions[1] != 3 || input.Dimensions[2] != MobileSamPreprocessor.InputSize ||
                input.Dimensions[3] != MobileSamPreprocessor.InputSize) {
                throw new InvalidDataException(
                    $"Unexpected MobileSAM encoder input. Expected '{EncoderInputName}' " +
                    "with shape [1,3,1024,1024].");
            }

            if (!encoder.OutputMetadata.ContainsKey(EncoderOutputName))
                throw new InvalidDataException(
                    $"Unexpected MobileSAM encoder output. Expected '{EncoderOutputName}'.");
        }

        private static void ValidateDecoderMetadata(InferenceSession decoder) {
            string[] missingInputs = RequiredDecoderInputs
                .Where(name => !decoder.InputMetadata.ContainsKey(name))
                .ToArray();
            string[] missingOutputs = RequiredDecoderOutputs
                .Where(name => !decoder.OutputMetadata.ContainsKey(name))
                .ToArray();

            if (missingInputs.Length != 0 || missingOutputs.Length != 0) {
                throw new InvalidDataException(
                    "Unexpected MobileSAM decoder metadata. " +
                    $"Missing inputs: [{string.Join(", ", missingInputs)}]; " +
                    $"missing outputs: [{string.Join(", ", missingOutputs)}].");
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

                    _encoderSession?.Dispose();
                    _decoderSession?.Dispose();
                    _encoderSession = null;
                    _decoderSession = null;
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
