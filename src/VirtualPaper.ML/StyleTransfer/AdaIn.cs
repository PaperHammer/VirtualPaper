using System.Buffers;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.ML.StyleTransfer.Interfaces;

namespace VirtualPaper.ML.StyleTransfer {
    public class AdaIn : IStyleTransfer {
        public string ModelPath { get; private set; } = null!;

        public AdaIn() { }

        public void LoadModel(string? path = null) {
            if (_isLoaded) {
                ArcLog.GetLogger<AdaIn>().Info("Model already loaded, skipping.");
                return;
            }

            ModelPath = path ?? Path.Combine(
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..")),
                Constants.WorkingDir.ML_StyleTransfer_AI_Models,
                Utils.Fields.ModelName);

            if (!File.Exists(ModelPath))
                throw new FileNotFoundException($"Model file not found: {ModelPath}");

            _session?.Dispose();
            using var options = new SessionOptions();
            options.EnableCpuMemArena = false;
            options.EnableMemoryPattern = false;
            // options.AppendExecutionProvider_CUDA();
            _session = new InferenceSession(ModelPath, options);
            ArcLog.GetLogger<AdaIn>().Info($"Model version: {_session.ModelMetadata.Version}");

            _outputNames = _session.OutputMetadata.Keys.ToList();
            _isLoaded = true;
        }

        public string RunAndSave(
            string contentImagePath,
            string styleImagePath,
            string outputFilePath,
            float alpha = 1.0f,
            int contentSize = 512,
            int styleSize = 512,
            CancellationToken ct = default) {

            ct.ThrowIfCancellationRequested();

            if (_session == null) throw new InvalidOperationException("ONNX Session is not initialized.");
            if (string.IsNullOrEmpty(ModelPath)) throw new FileNotFoundException("ONNX file not provided.");
            if (!File.Exists(contentImagePath)) throw new FileNotFoundException($"Content image not found: {contentImagePath}");
            if (!File.Exists(styleImagePath)) throw new FileNotFoundException($"Style image not found: {styleImagePath}");

            ct.ThrowIfCancellationRequested();

            using var contentImage = LoadAndResizeImage(contentImagePath, contentSize);
            using var styleImage = LoadAndResizeImage(styleImagePath, styleSize);

            int originalWidth = contentImage.Width;
            int originalHeight = contentImage.Height;

            int contentPixels = contentImage.Height * contentImage.Width * 3;
            int stylePixels = styleImage.Height * styleImage.Width * 3;

            float[] contentBuffer = ArrayPool<float>.Shared.Rent(contentPixels);
            float[] styleBuffer = ArrayPool<float>.Shared.Rent(stylePixels);

            try {
                ct.ThrowIfCancellationRequested();

                var contentTensor = ImageToTensor(contentImage, contentBuffer);
                var styleTensor = ImageToTensor(styleImage, styleBuffer);
                var alphaTensor = new DenseTensor<float>(new float[] { alpha }, new int[] { 1 });

                var inputs = new List<NamedOnnxValue> {
                    NamedOnnxValue.CreateFromTensor("content", contentTensor),
                    NamedOnnxValue.CreateFromTensor("style",   styleTensor),
                    NamedOnnxValue.CreateFromTensor("alpha",   alphaTensor)
                };

                // RunOptions 与 CT 绑定：CT 取消时通过 Terminate = true 在下一个算子边界中止推理，
                // 使外部能立即感知取消而无需等待整个模型执行完毕。
                // 注：AdaIn 是单次串行 Run()，无 Parallel.ForEach 兜底，
                //     catch 里需直接 throw OperationCanceledException。
                using var runOptions = new RunOptions();
                using var ctRegistration = ct.Register(() => runOptions.Terminate = true);

                try {
                    using var results = _session.Run(inputs, _outputNames, runOptions);
                    var outputTensor = (DenseTensor<float>)results[0].AsTensor<float>();

                    int outHeight = outputTensor.Dimensions[2];
                    int outWidth = outputTensor.Dimensions[3];

                    return TensorToImageAndSave(
                        outputTensor, outWidth, outHeight,
                        originalWidth, originalHeight,
                        outputFilePath);
                }
                catch (OnnxRuntimeException) when (ct.IsCancellationRequested) {
                    // RunOptions.Terminate 触发的 ONNX 中断，转换为标准取消异常向外传播
                    ct.ThrowIfCancellationRequested();
                    throw; // unreachable，仅为满足编译器返回值静态检查
                }
            }
            finally {
                ArrayPool<float>.Shared.Return(contentBuffer, clearArray: false);
                ArrayPool<float>.Shared.Return(styleBuffer, clearArray: false);
            }
        }

        #region OpenCV Image Processing

        /// <summary>
        /// 加载图像并将<b>短边</b>缩放到 <paramref name="targetSize"/>，长边按比例缩放，保持宽高比。
        /// 若 <paramref name="targetSize"/> &lt;= 0，则直接返回原图，不做缩放。
        /// </summary>
        private static Mat LoadAndResizeImage(string imagePath, int targetSize) {
            var image = Cv2.ImRead(imagePath, ImreadModes.Color);
            if (image.Empty()) {
                image.Dispose();
                throw new ArgumentException($"Failed to load image: {imagePath}");
            }

            Cv2.CvtColor(image, image, ColorConversionCodes.BGR2RGB);

            if (targetSize <= 0)
                return image;

            int w = image.Width;
            int h = image.Height;
            int newW, newH;

            if (w < h) {
                newW = targetSize;
                newH = (int)(h * ((float)targetSize / w));
            }
            else {
                newH = targetSize;
                newW = (int)(w * ((float)targetSize / h));
            }

            using var original = image;
            var resized = new Mat();
            Cv2.Resize(original, resized, new Size(newW, newH), 0, 0, InterpolationFlags.Linear);
            return resized;
        }

        private static DenseTensor<float> ImageToTensor(Mat image, float[] buffer) {
            int height = image.Height;
            int width = image.Width;
            int channelSize = height * width;

            unsafe {
                byte* ptr = (byte*)image.Data;
                int stride = (int)image.Step();

                for (int y = 0; y < height; y++) {
                    byte* row = ptr + y * stride;
                    for (int x = 0; x < width; x++) {
                        int pixelIdx = y * width + x;
                        buffer[pixelIdx] = row[x * 3] / 255f; // R
                        buffer[channelSize + pixelIdx] = row[x * 3 + 1] / 255f; // G
                        buffer[2 * channelSize + pixelIdx] = row[x * 3 + 2] / 255f; // B
                    }
                }
            }

            var memory = new Memory<float>(buffer, 0, channelSize * 3);
            return new DenseTensor<float>(memory, new int[] { 1, 3, height, width });
        }

        private static string TensorToImageAndSave(
            DenseTensor<float> outputTensor,
            int outWidth, int outHeight,
            int originalWidth, int originalHeight,
            string outputFilePath) {

            using var image = new Mat(outHeight, outWidth, MatType.CV_8UC3);
            int channelSize = outHeight * outWidth;

            // Span 直接访问张量底层缓冲，消除 GetValue() 逐像素多维索引转换的开销
            ReadOnlySpan<float> outSpan = outputTensor.Buffer.Span;

            unsafe {
                byte* ptr = (byte*)image.Data;
                int stride = (int)image.Step();

                for (int y = 0; y < outHeight; y++) {
                    byte* row = ptr + y * stride;
                    for (int x = 0; x < outWidth; x++) {
                        int idx = y * outWidth + x;

                        float r = Math.Clamp(outSpan[idx], 0f, 1f);
                        float g = Math.Clamp(outSpan[channelSize + idx], 0f, 1f);
                        float b = Math.Clamp(outSpan[2 * channelSize + idx], 0f, 1f);

                        // BGR order for OpenCV
                        row[x * 3] = (byte)(b * 255f);
                        row[x * 3 + 1] = (byte)(g * 255f);
                        row[x * 3 + 2] = (byte)(r * 255f);
                    }
                }
            }

            // 还原到原始尺寸
            using var resized = new Mat();
            Cv2.Resize(image, resized,
                new Size(originalWidth, originalHeight),
                0, 0, InterpolationFlags.Linear);

            string? dir = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string extension = Path.GetExtension(outputFilePath);
            WriteImage(resized, outputFilePath, extension);

            return outputFilePath;
        }

        private static void WriteImage(Mat image, string path, string extension) {
            switch (extension.ToLowerInvariant()) {
                case ".jpg":
                case ".jpeg":
                    image.ImWrite(path, new ImageEncodingParam(ImwriteFlags.JpegQuality, 95));
                    break;
                case ".webp":
                    image.ImWrite(path, new ImageEncodingParam(ImwriteFlags.WebPQuality, 95));
                    break;
                default:
                    image.ImWrite(path);
                    break;
            }
        }

        #endregion

        #region IDisposable

        private bool _isDisposed;

        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    _session?.Dispose();
                    _session = null;
                    _isLoaded = false;
                }
                _isDisposed = true;
            }
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion

        // 单例实例字段，仅 LoadModel 写入，之后只读
        private InferenceSession? _session;
        private IReadOnlyList<string> _outputNames = [];
        private volatile bool _isLoaded;
    }
}
