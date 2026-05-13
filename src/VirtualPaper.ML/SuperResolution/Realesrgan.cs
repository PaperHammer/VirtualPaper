using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.ML.Realesrgan;
using VirtualPaper.ML.SuperResolution.Interfaces;

namespace VirtualPaper.ML.SuperResolution {
    public class Realesrgan : ISuperResolution {
        public string ModelPath { get; private set; } = null!;

        public Realesrgan() { }

        public void LoadModel(string? path = null) {
            if (_isLoaded) {
                ArcLog.GetLogger<Realesrgan>().Info("Model already loaded, skipping.");
                return;
            }

            ModelPath = path ?? Path.Combine(
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..")),
                Constants.WorkingDir.ML_SuperResolution_AI_Models,
                Utils.Fields.ModelName);

            if (!File.Exists(ModelPath))
                throw new FileNotFoundException($"Model file not found: {ModelPath}");

            _session?.Dispose();
            using var options = new SessionOptions();
            options.EnableCpuMemArena = false;
            options.EnableMemoryPattern = false;
            _session = new InferenceSession(ModelPath, options);
            ArcLog.GetLogger<Realesrgan>().Info($"Real-ESRGAN Model version: {_session.ModelMetadata.Version}");

            _inputName = _session.InputMetadata.Keys.First();
            _isLoaded = true;
        }

        public string RunAndSave(
            string inputImagePath,
            string outputFilePath,
            uint targetWidth,
            uint targetHeight) {

            if (_session == null) throw new InvalidOperationException("ONNX Session is not initialized.");
            if (!File.Exists(inputImagePath)) throw new FileNotFoundException($"Input image not found: {inputImagePath}");

            int imgWidth, imgHeight;

            using (var image = Cv2.ImRead(inputImagePath, ImreadModes.Color)) {
                if (image.Empty())
                    throw new ArgumentException($"Failed to load image: {inputImagePath}");

                imgWidth = image.Width;
                imgHeight = image.Height;

                int pixelCount = imgHeight * imgWidth * 3;
                float[] inputBuffer = System.Buffers.ArrayPool<float>.Shared.Rent(pixelCount);

                try {
                    Cv2.CvtColor(image, image, ColorConversionCodes.BGR2RGB);

                    unsafe {
                        byte* ptr = (byte*)image.Data;
                        int stride = (int)image.Step();
                        int channelSize = imgHeight * imgWidth;

                        for (int y = 0; y < imgHeight; y++) {
                            byte* row = ptr + y * stride;
                            for (int x = 0; x < imgWidth; x++) {
                                int pixelIdx = y * imgWidth + x;
                                inputBuffer[pixelIdx] = row[x * 3] / 255f;
                                inputBuffer[channelSize + pixelIdx] = row[x * 3 + 1] / 255f;
                                inputBuffer[2 * channelSize + pixelIdx] = row[x * 3 + 2] / 255f;
                            }
                        }
                    }

                    var memory = new Memory<float>(inputBuffer, 0, pixelCount);
                    var inputTensor = new DenseTensor<float>(memory, new int[] { 1, 3, imgHeight, imgWidth });

                    var inputs = new List<NamedOnnxValue> {
                        NamedOnnxValue.CreateFromTensor("input", inputTensor)
                    };

                    using var results = _session.Run(inputs);
                    var outputTensor = results[0].AsTensor<float>();

                    int outHeight = outputTensor.Dimensions[2];
                    int outWidth = outputTensor.Dimensions[3];

                    // 直接从 Tensor 写入 Mat 并保存
                    using var outputImage = new Mat(outHeight, outWidth, MatType.CV_8UC3);
                    int outChannelSize = outHeight * outWidth;

                    unsafe {
                        byte* ptr = (byte*)outputImage.Data;
                        int stride = (int)outputImage.Step();

                        for (int y = 0; y < outHeight; y++) {
                            byte* row = ptr + y * stride;
                            for (int x = 0; x < outWidth; x++) {
                                int idx = y * outWidth + x;

                                float r = Math.Clamp(outputTensor.GetValue(idx), 0f, 1f);
                                float g = Math.Clamp(outputTensor.GetValue(outChannelSize + idx), 0f, 1f);
                                float b = Math.Clamp(outputTensor.GetValue(2 * outChannelSize + idx), 0f, 1f);

                                row[x * 3] = (byte)(b * 255f);
                                row[x * 3 + 1] = (byte)(g * 255f);
                                row[x * 3 + 2] = (byte)(r * 255f);
                            }
                        }
                    }

                    // 缩放到目标尺寸
                    using var finalImage = new Mat();
                    Cv2.Resize(outputImage, finalImage,
                        new Size((int)targetWidth, (int)targetHeight),
                        0, 0, InterpolationFlags.Linear);

                    string? dir = Path.GetDirectoryName(outputFilePath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                    string extension = Path.GetExtension(outputFilePath).ToLowerInvariant();
                    switch (extension) {
                        case ".jpg":
                        case ".jpeg":
                            finalImage.ImWrite(outputFilePath, new ImageEncodingParam(ImwriteFlags.JpegQuality, 95));
                            break;
                        case ".webp":
                            finalImage.ImWrite(outputFilePath, new ImageEncodingParam(ImwriteFlags.WebPQuality, 95));
                            break;
                        default:
                            finalImage.ImWrite(outputFilePath);
                            break;
                    }

                    return outputFilePath;
                }
                finally {
                    System.Buffers.ArrayPool<float>.Shared.Return(inputBuffer, clearArray: false);
                }
            }
        }

        #region Private Helpers

        private static unsafe void FillTensorBuffer(Mat img, float[] buffer) {
            int h = img.Height;
            int w = img.Width;
            int channelSize = h * w;
            byte* ptr = (byte*)img.Data;
            int stride = (int)img.Step();

            for (int y = 0; y < h; y++) {
                byte* row = ptr + y * stride;
                for (int x = 0; x < w; x++) {
                    int pixelIdx = y * w + x;
                    buffer[pixelIdx] = row[x * 3] / 255f;                    // R
                    buffer[channelSize + pixelIdx] = row[x * 3 + 1] / 255f;  // G
                    buffer[2 * channelSize + pixelIdx] = row[x * 3 + 2] / 255f; // B
                }
            }
        }

        private static void WriteImage(Mat image, string path, string extension) {
            var encodeParams = GetEncodeParams(extension);
            if (encodeParams != null) {
                image.ImWrite(path, encodeParams);
            }
            else {
                image.ImWrite(path);
            }
        }

        private static ImageEncodingParam[]? GetEncodeParams(string extension) {
            return extension.ToLowerInvariant() switch {
                ".jpg" or ".jpeg" => new[] { new ImageEncodingParam(ImwriteFlags.JpegQuality, 95) },
                ".png" => new[] { new ImageEncodingParam(ImwriteFlags.PngCompression, 3) },
                ".webp" => new[] { new ImageEncodingParam(ImwriteFlags.WebPQuality, 95) },
                _ => null
            };
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
        private string _inputName = string.Empty;
        private volatile bool _isLoaded;
    }
}
