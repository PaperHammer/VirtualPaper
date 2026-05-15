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
            // 将每个 Run() 的 intra-op 线程数按并行 Tile 数等比分配：
            // MaxParallelTiles 个 Tile 同时推理时，合计仍恰好占满全部 CPU 核心，不互相争抢。
            options.IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount / MaxParallelTiles);
            _session = new InferenceSession(ModelPath, options);
            ArcLog.GetLogger<Realesrgan>().Info($"Real-ESRGAN Model version: {_session.ModelMetadata.Version}");

            _inputName = _session.InputMetadata.Keys.First();
            _isLoaded = true;
        }

        /// <summary>
        /// 对输入图像进行超分辨率放大并保存到指定路径。
        /// <list type="bullet">
        ///   <item>Tile 分块：固定 <see cref="TileSize"/>×<see cref="TileSize"/> 输入，内存恒定，ONNX 执行计划复用。</item>
        ///   <item>并行推理：最多 <see cref="MaxParallelTiles"/> 块同时调用 <c>Session.Run()</c>（线程安全）。</item>
        ///   <item>无锁写回：各 Tile 目标区域互不重叠，可并发写入 outputImage。</item>
        /// </list>
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

            int imgWidth = image.Width;
            int imgHeight = image.Height;

            int step = TileSize - 2 * TileOverlap;
            int numTilesX = (imgWidth + step - 1) / step;
            int numTilesY = (imgHeight + step - 1) / step;

            // 预计算全部 Tile 坐标，避免在并行 lambda 中重复推导
            var tileInfos = new TileInfo[numTilesX * numTilesY];
            for (int ty = 0; ty < numTilesY; ty++) {
                for (int tx = 0; tx < numTilesX; tx++) {
                    int sx = tx * step - TileOverlap;
                    int sy = ty * step - TileOverlap;
                    tileInfos[ty * numTilesX + tx] = new TileInfo(
                        sx, sy,
                        Math.Max(0, sx),
                        Math.Max(0, sy),
                        Math.Min(imgWidth, sx + TileSize),
                        Math.Min(imgHeight, sy + TileSize));
                }
            }

            int fixedTilePixels = TileSize * TileSize * 3;
            int fixedChannelSize = TileSize * TileSize;

            // outputImage 跨线程懒初始化：所有 Tile 推理 shape 相同，任何一块完成即可确定整图尺寸，
            // 用 Interlocked.CompareExchange 保证只初始化一次，竞争失败方直接 Dispose 候选对象。
            Mat? outputImage = null;

            Parallel.ForEach(
                tileInfos,
                new ParallelOptions { MaxDegreeOfParallelism = MaxParallelTiles },
                // 每个工作线程独享一块 ArrayPool 缓冲，避免并发写入同一 buffer
                () => ArrayPool<float>.Shared.Rent(fixedTilePixels),
                (tile, _, localBuffer) => {
                    int tileW = tile.ExClamped - tile.SxClamped;
                    int tileH = tile.EyClamped - tile.SyClamped;

                    // 提取 Tile 并填充 TileSize×TileSize 的 float 张量缓冲
                    var roi = new Rect(tile.SxClamped, tile.SyClamped, tileW, tileH);
                    using var tileMat = new Mat(image, roi);

                    unsafe {
                        byte* ptr = (byte*)tileMat.Data;
                        int stride = (int)tileMat.Step();

                        if (tileW == TileSize && tileH == TileSize) {
                            // 内部 Tile：尺寸恰好 TileSize×TileSize，无需边缘填充，走快速路径
                            for (int y = 0; y < TileSize; y++) {
                                byte* row = ptr + y * stride;
                                for (int x = 0; x < TileSize; x++) {
                                    int pixIdx = y * TileSize + x;
                                    localBuffer[pixIdx] = row[x * 3] / 255f; // R
                                    localBuffer[fixedChannelSize + pixIdx] = row[x * 3 + 1] / 255f; // G
                                    localBuffer[2 * fixedChannelSize + pixIdx] = row[x * 3 + 2] / 255f; // B
                                }
                            }
                        }
                        else {
                            // 边缘 Tile：不足 TileSize 的部分用边缘像素填充（edge replication），
                            // 避免零填充在超分输出中产生黑色伪影
                            for (int y = 0; y < TileSize; y++) {
                                int srcY = Math.Min(y, tileH - 1);
                                byte* row = ptr + srcY * stride;
                                for (int x = 0; x < TileSize; x++) {
                                    int srcX = Math.Min(x, tileW - 1);
                                    int pixIdx = y * TileSize + x;
                                    localBuffer[pixIdx] = row[srcX * 3] / 255f; // R
                                    localBuffer[fixedChannelSize + pixIdx] = row[srcX * 3 + 1] / 255f; // G
                                    localBuffer[2 * fixedChannelSize + pixIdx] = row[srcX * 3 + 2] / 255f; // B
                                }
                            }
                        }
                    }

                    // 推理（InferenceSession.Run 线程安全，可并发调用）
                    // 所有 Tile 输入均为 [1, 3, TileSize, TileSize]，ONNX 复用执行计划
                    var memory = new Memory<float>(localBuffer, 0, fixedTilePixels);
                    var inputTensor = new DenseTensor<float>(memory, [1, 3, TileSize, TileSize]);
                    var inputs = new List<NamedOnnxValue>(1) {
                        NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
                    };

                    using var results = _session.Run(inputs);
                    var outTensor = (DenseTensor<float>)results[0].AsTensor<float>();
                    ReadOnlySpan<float> outSpan = outTensor.Buffer.Span;

                    int upW = outTensor.Dimensions[3]; // = TileSize * scaleX
                    int upH = outTensor.Dimensions[2]; // = TileSize * scaleY
                    int upChannelSize = upW * upH;
                    int scaleX = upW / TileSize;
                    int scaleY = upH / TileSize;

                    // outputImage 懒分配（任意一块推理完成后即可确定整图尺寸）
                    if (outputImage == null) {
                        var candidate = new Mat(imgHeight * scaleY, imgWidth * scaleX, MatType.CV_8UC3);
                        if (Interlocked.CompareExchange(ref outputImage, candidate, null) != null)
                            candidate.Dispose(); // 其他线程已抢先分配，放弃本候选
                    }

                    // 计算有效区域（剥离 overlap 边缘，以实际内容宽高为上界排除填充区）
                    int contentW = tileW * scaleX;
                    int contentH = tileH * scaleY;

                    int leftStrip = (tile.Sx < 0 ? 0 : TileOverlap) * scaleX;
                    int topStrip = (tile.Sy < 0 ? 0 : TileOverlap) * scaleY;
                    int rightStrip = (tile.ExClamped == imgWidth ? 0 : TileOverlap) * scaleX;
                    int bottomStrip = (tile.EyClamped == imgHeight ? 0 : TileOverlap) * scaleY;

                    int validX = leftStrip;
                    int validY = topStrip;
                    int validW = contentW - leftStrip - rightStrip;
                    int validH = contentH - topStrip - bottomStrip;

                    // 有效区域在完整输出图中的起始坐标
                    int dstX = (tile.SxClamped + (tile.Sx < 0 ? 0 : TileOverlap)) * scaleX;
                    int dstY = (tile.SyClamped + (tile.Sy < 0 ? 0 : TileOverlap)) * scaleY;

                    // 将有效区域写入 outputImage
                    // 各 Tile 的 [dstX, dstX+validW) × [dstY, dstY+validH) 互不重叠，无需加锁
                    unsafe {
                        byte* dstPtr = (byte*)outputImage!.Data;
                        int dstStride = (int)outputImage.Step();

                        for (int y = 0; y < validH; y++) {
                            byte* dstRow = dstPtr + (dstY + y) * dstStride;
                            int srcY = validY + y;

                            for (int x = 0; x < validW; x++) {
                                int idx = srcY * upW + (validX + x);

                                byte r = (byte)(Math.Clamp(outSpan[idx], 0f, 1f) * 255f);
                                byte g = (byte)(Math.Clamp(outSpan[upChannelSize + idx], 0f, 1f) * 255f);
                                byte b = (byte)(Math.Clamp(outSpan[2 * upChannelSize + idx], 0f, 1f) * 255f);

                                // OpenCV Mat 存储为 BGR
                                dstRow[(dstX + x) * 3] = b;
                                dstRow[(dstX + x) * 3 + 1] = g;
                                dstRow[(dstX + x) * 3 + 2] = r;
                            }
                        }
                    }

                    return localBuffer;
                },

                localBuffer => ArrayPool<float>.Shared.Return(localBuffer, clearArray: false)
            );

            if (outputImage == null)
                throw new InvalidOperationException("No tiles were processed; output image was never initialized.");

            string? dir = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string extension = Path.GetExtension(outputFilePath).ToLowerInvariant();

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

        /// <summary>每个 Tile 的输入尺寸（像素）。越小内存越低，但 Tile 数量越多。</summary>
        private const int TileSize = 512;
        /// <summary>Tile 边缘 overlap 宽度（输入像素）。防止拼接处产生明显接缝。</summary>
        private const int TileOverlap = 16;
        /// <summary>
        /// 最大并行 Tile 数。ONNX intra-op 线程按 ProcessorCount / MaxParallelTiles 分配，
        /// 使得所有并行 Tile 合计恰好占满全部 CPU 核心。
        /// 若遇到内存压力，可调低此值。
        /// </summary>
        private const int MaxParallelTiles = 2;

        private readonly struct TileInfo(
            int sx, int sy,
            int sxClamped, int syClamped,
            int exClamped, int eyClamped) {
            public readonly int Sx = sx;
            public readonly int Sy = sy;
            public readonly int SxClamped = sxClamped;
            public readonly int SyClamped = syClamped;
            public readonly int ExClamped = exClamped;
            public readonly int EyClamped = eyClamped;
        }

        private InferenceSession? _session;
        private string _inputName = string.Empty;
        private volatile bool _isLoaded;
    }
}
