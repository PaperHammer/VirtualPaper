using System.Buffers;
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

        /// <summary>
        /// 对输入图像进行超分辨率放大并保存到指定路径。
        /// 使用 Tile 分块策略：将大图切成固定大小的小块逐块推理，
        /// 内存占用由 <see cref="TileSize"/> 决定，不会随原图尺寸平方级增长。
        /// 输出张量写回使用 Span 直接访问，消除逐像素 GetValue 的索引转换开销。
        /// </summary>
        public string RunAndSave(
            string inputImagePath,
            string outputFilePath,
            uint targetWidth,
            uint targetHeight) {

            if (_session == null) throw new InvalidOperationException("ONNX Session is not initialized.");
            if (!File.Exists(inputImagePath)) throw new FileNotFoundException($"Input image not found: {inputImagePath}");

            using var image = Cv2.ImRead(inputImagePath, ImreadModes.Color);
            if (image.Empty())
                throw new ArgumentException($"Failed to load image: {inputImagePath}");

            // 转 RGB 后推理，最终写回时再转 BGR
            Cv2.CvtColor(image, image, ColorConversionCodes.BGR2RGB);

            int imgWidth  = image.Width;
            int imgHeight = image.Height;

            // Tile 分块推理
            // step = 每块"有效"覆盖宽度（去掉两侧 overlap）
            int step      = TileSize - 2 * TileOverlap;
            int numTilesX = (imgWidth  + step - 1) / step;
            int numTilesY = (imgHeight + step - 1) / step;

            // 预分配最大可能 Tile 的 float 缓冲（ArrayPool 复用，避免 GC 压力）
            int maxTilePixels = TileSize * TileSize * 3;
            float[] tileBuffer = ArrayPool<float>.Shared.Rent(maxTilePixels);

            // outputImage 懒初始化：第一块推理后，从实际输出维度推算放大倍数，再分配整图
            Mat? outputImage = null;

            try {
                for (int ty = 0; ty < numTilesY; ty++) {
                    for (int tx = 0; tx < numTilesX; tx++) {
                        // 计算本块在原图中的采样区域（含 overlap，边缘自动裁剪）
                        int sx = tx * step - TileOverlap;
                        int sy = ty * step - TileOverlap;

                        int sxClamped = Math.Max(0, sx);
                        int syClamped = Math.Max(0, sy);
                        int exClamped = Math.Min(imgWidth,  sx + TileSize);
                        int eyClamped = Math.Min(imgHeight, sy + TileSize);

                        int tileW = exClamped - sxClamped;
                        int tileH = eyClamped - syClamped;
                        int tileChannelSize  = tileW * tileH;
                        int tilePixelCount   = tileChannelSize * 3;

                        // 提取 Tile 并填充 float 张量缓冲
                        var roi = new Rect(sxClamped, syClamped, tileW, tileH);
                        using var tileMat = new Mat(image, roi);

                        unsafe {
                            byte* ptr    = (byte*)tileMat.Data;
                            int   stride = (int)tileMat.Step();

                            for (int y = 0; y < tileH; y++) {
                                byte* row = ptr + y * stride;
                                for (int x = 0; x < tileW; x++) {
                                    int pixIdx = y * tileW + x;
                                    tileBuffer[pixIdx]                        = row[x * 3]     / 255f; // R
                                    tileBuffer[tileChannelSize + pixIdx]      = row[x * 3 + 1] / 255f; // G
                                    tileBuffer[2 * tileChannelSize + pixIdx]  = row[x * 3 + 2] / 255f; // B
                                }
                            }
                        }

                        // 推理
                        // ArrayPool 返回的缓冲区容量可能大于 tilePixelCount（边缘 Tile 较小时尤为如此），
                        // 多余部分是上一块的残余数据；Memory<float> 精确切片到 tilePixelCount，
                        // 模型只读取有效范围，不会受到残余数据污染，安全。
                        var memory      = new Memory<float>(tileBuffer, 0, tilePixelCount);
                        var inputTensor = new DenseTensor<float>(memory, [1, 3, tileH, tileW]);
                        var inputs      = new List<NamedOnnxValue> {
                            NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
                        };

                        using var results = _session.Run(inputs);

                        // Span 直接访问输出张量，避免 GetValue 逐像素索引转换
                        var outTensor      = (DenseTensor<float>)results[0].AsTensor<float>();
                        ReadOnlySpan<float> outSpan = outTensor.Buffer.Span;

                        int upW           = outTensor.Dimensions[3];
                        int upH           = outTensor.Dimensions[2];
                        int upChannelSize = upW * upH;

                        // 从实际推理结果推算模型放大倍数，与任何硬编码常量解耦
                        int scaleX = upW / tileW;
                        int scaleY = upH / tileH;

                        // 首块推理完毕后才知道真实放大倍数，此时懒分配整图输出缓冲
                        outputImage ??= new Mat(imgHeight * scaleY, imgWidth * scaleX, MatType.CV_8UC3);

                        // 计算有效区域（剥离 overlap 边缘）
                        // 图像左/上边缘的第一块不需要剥离左/上 overlap
                        int leftStrip   = (sx  <  0          ? 0 : TileOverlap) * scaleX;
                        int topStrip    = (sy  <  0          ? 0 : TileOverlap) * scaleY;
                        int rightStrip  = (exClamped == imgWidth  ? 0 : TileOverlap) * scaleX;
                        int bottomStrip = (eyClamped == imgHeight ? 0 : TileOverlap) * scaleY;

                        int validX = leftStrip;
                        int validY = topStrip;
                        int validW = upW - leftStrip  - rightStrip;
                        int validH = upH - topStrip   - bottomStrip;

                        // 有效区域在完整输出图中的起始坐标
                        int dstX = (sxClamped + (sx < 0 ? 0 : TileOverlap)) * scaleX;
                        int dstY = (syClamped + (sy < 0 ? 0 : TileOverlap)) * scaleY;

                        // 将有效区域写入输出图
                        unsafe {
                            byte* dstPtr    = (byte*)outputImage.Data;
                            int   dstStride = (int)outputImage.Step();

                            for (int y = 0; y < validH; y++) {
                                byte* dstRow = dstPtr + (dstY + y) * dstStride;
                                int   srcY   = validY + y;

                                for (int x = 0; x < validW; x++) {
                                    int idx = srcY * upW + (validX + x);

                                    byte r = (byte)(Math.Clamp(outSpan[idx],                     0f, 1f) * 255f);
                                    byte g = (byte)(Math.Clamp(outSpan[upChannelSize   + idx],   0f, 1f) * 255f);
                                    byte b = (byte)(Math.Clamp(outSpan[2 * upChannelSize + idx], 0f, 1f) * 255f);

                                    // OpenCV Mat 存储为 BGR
                                    dstRow[(dstX + x) * 3]     = b;
                                    dstRow[(dstX + x) * 3 + 1] = g;
                                    dstRow[(dstX + x) * 3 + 2] = r;
                                }
                            }
                        }
                    }
                }
            }
            finally {
                ArrayPool<float>.Shared.Return(tileBuffer, clearArray: false);
            }

            if (outputImage == null)
                throw new InvalidOperationException("No tiles were processed; output image was never initialized.");

            string? dir = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string extension = Path.GetExtension(outputFilePath).ToLowerInvariant();

            // 用显式 using 块包裹 outputImage，Resize 完成后立即释放大尺寸中间 Mat，
            // 避免 "using var _" 写法导致生命周期延续到方法末尾，造成内存多占用。
            using (outputImage) {
                using var finalImage = new Mat();
                Cv2.Resize(outputImage, finalImage,
                    new Size((int)targetWidth, (int)targetHeight),
                    0, 0, InterpolationFlags.Linear);

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
            }

            return outputFilePath;
        }

        #region IDisposable

        private bool _isDisposed;

        protected virtual void Dispose(bool disposing) {
            if (!_isDisposed) {
                if (disposing) {
                    _session?.Dispose();
                    _session  = null;
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

        /// <summary>每个 Tile 的输入尺寸（像素）。越小内存越低，但 Tile 数量越多。</summary>
        private const int TileSize    = 512;
        /// <summary>Tile 边缘 overlap 宽度（输入像素）。防止拼接处产生明显接缝。</summary>
        private const int TileOverlap = 16;

        private InferenceSession? _session;
        private string _inputName = string.Empty;
        private volatile bool _isLoaded;
    }
}
