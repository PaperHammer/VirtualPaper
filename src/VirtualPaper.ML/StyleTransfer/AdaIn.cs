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

            _isLoaded = true;
        }        

        /// <summary>
        /// 如果仍然需要分步调用的接口
        /// </summary>
        public string RunAndSave(
            string contentImagePath,
            string styleImagePath,
            string outputFilePath,
            float alpha = 1.0f,
            int contentSize = 512,
            int styleSize = 512) {

            if (_session == null) throw new InvalidOperationException("ONNX Session is not initialized.");
            if (string.IsNullOrEmpty(ModelPath)) throw new FileNotFoundException("ONNX file not provided.");
            if (!File.Exists(contentImagePath)) throw new FileNotFoundException($"Content image not found: {contentImagePath}");
            if (!File.Exists(styleImagePath)) throw new FileNotFoundException($"Style image not found: {styleImagePath}");

            int originalWidth, originalHeight;

            using var contentImage = LoadAndResizeImage(contentImagePath, contentSize);
            using var styleImage = LoadAndResizeImage(styleImagePath, styleSize);

            originalWidth = contentImage.Width;
            originalHeight = contentImage.Height;

            int contentPixels = contentImage.Height * contentImage.Width * 3;
            int stylePixels = styleImage.Height * styleImage.Width * 3;

            float[] contentBuffer = System.Buffers.ArrayPool<float>.Shared.Rent(contentPixels);
            float[] styleBuffer = System.Buffers.ArrayPool<float>.Shared.Rent(stylePixels);

            try {
                var contentTensor = ImageToTensor(contentImage, contentBuffer);
                var styleTensor = ImageToTensor(styleImage, styleBuffer);
                var alphaTensor = new DenseTensor<float>(new float[] { alpha }, new int[] { 1 });

                var inputs = new List<NamedOnnxValue> {
                    NamedOnnxValue.CreateFromTensor("content", contentTensor),
                    NamedOnnxValue.CreateFromTensor("style", styleTensor),
                    NamedOnnxValue.CreateFromTensor("alpha", alphaTensor)
                };

                using var results = _session.Run(inputs);
                var outputTensor = results[0].AsTensor<float>();

                int outHeight = outputTensor.Dimensions[2];
                int outWidth = outputTensor.Dimensions[3];

                string result = TensorToImageAndSave(
                    outputTensor, outWidth, outHeight,
                    originalWidth, originalHeight,
                    outputFilePath);

                return result;
            }
            finally {
                System.Buffers.ArrayPool<float>.Shared.Return(contentBuffer, clearArray: false);
                System.Buffers.ArrayPool<float>.Shared.Return(styleBuffer, clearArray: false);
            }
        }

        #region OpenCV Image Processing

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
                        buffer[pixelIdx] = row[x * 3] / 255f;                    // R
                        buffer[channelSize + pixelIdx] = row[x * 3 + 1] / 255f;  // G
                        buffer[2 * channelSize + pixelIdx] = row[x * 3 + 2] / 255f; // B
                    }
                }
            }

            var memory = new Memory<float>(buffer, 0, channelSize * 3);
            return new DenseTensor<float>(memory, new int[] { 1, 3, height, width });
        }

        private static string TensorToImageAndSave(
            Tensor<float> outputTensor,
            int outWidth, int outHeight,
            int originalWidth, int originalHeight,
            string outputFilePath) {

            using var image = new Mat(outHeight, outWidth, MatType.CV_8UC3);
            int channelSize = outHeight * outWidth;

            unsafe {
                byte* ptr = (byte*)image.Data;
                int stride = (int)image.Step();

                for (int y = 0; y < outHeight; y++) {
                    byte* row = ptr + y * stride;
                    for (int x = 0; x < outWidth; x++) {
                        int idx = y * outWidth + x;

                        float r = Math.Clamp(outputTensor.GetValue(idx), 0f, 1f);
                        float g = Math.Clamp(outputTensor.GetValue(channelSize + idx), 0f, 1f);
                        float b = Math.Clamp(outputTensor.GetValue(2 * channelSize + idx), 0f, 1f);

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
            var ext = extension.ToLowerInvariant();
            switch (ext) {
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

        private InferenceSession? _session;
        private volatile bool _isLoaded;
    }
}
