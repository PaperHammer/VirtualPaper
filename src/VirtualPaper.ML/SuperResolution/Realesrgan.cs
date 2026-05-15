using System.Buffers;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
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
            _outputNames = _session.OutputMetadata.Keys.ToList();
            _isLoaded = true;
        }

        /// <summary>
        /// 对输入图像进行超分辨率放大并保存到指定路径。
        /// <list type="bullet">
        ///   <item>Tile 分块：固定 <see cref="TileSize"/>×<see cref="TileSize"/> 输入，内存恒定，ONNX 执行计划复用。</item>
        ///   <item>并行推理：最多 <see cref="MaxParallelTiles"/> 块同时调用 <c>Session.Run()</c>（线程安全）。</item>
        ///   <item>立即取消：通过 <see cref="RunOptions.Terminate"/> 在下一个算子边界中止正在推理的 Tile。</item>
        /// </list>
        /// </summary>
        public string RunAndSave(
            string inputImagePath,
            string outputFilePath,
            uint targetWidth,
            uint targetHeight,
            CancellationToken ct = default) {

            ct.ThrowIfCancellationRequested();

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

            // ── 调用级局部状态（与单例实例字段完全隔离）────────────────────────
            // outputImage：本次调用的输出 Mat，跨并行 Tile 懒初始化
            Mat? outputImage = null;

            // RunOptions：本次调用独享，不能跨调用复用。
            // CT 触发时通过 Terminate = true 通知所有正在执行的 Run() 在下一个算子边界中止。
            using var runOptions = new RunOptions();
            using var ctRegistration = ct.Register(() => runOptions.Terminate = true);

            // ─────────────────────────────────────────────────────────────────
            // 取消策略：catch 里不手动 throw OCE，完全依赖 ParallelOptions.CancellationToken。
            // 原因：ParallelOptions.CancellationToken = ct 会在 CT 取消后让 Parallel.ForEach
            // 自动抛出一次 OperationCanceledException；若 catch 里也 throw，会产生双重/多重
            // OCE，在调试器中触发多次断点并污染日志。
            // 各 Tile 静默 return 即可——CT 取消后框架不再调度新 Tile，所有 Tile 退出后
            // Parallel.ForEach 统一以 OperationCanceledException 通知外层。
            // ─────────────────────────────────────────────────────────────────
            Parallel.ForEach(
                tileInfos,
                new ParallelOptions {
                    MaxDegreeOfParallelism = MaxParallelTiles,
                    CancellationToken = ct   // CT 取消后阻止新 Tile 启动，并统一抛 OCE
                },
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
                    // 传入本次调用独享的 runOptions：CT 触发时 Terminate=true 会中止 Run()
                    var memory = new Memory<float>(localBuffer, 0, fixedTilePixels);
                    var inputTensor = new DenseTensor<float>(memory, [1, 3, TileSize, TileSize]);
                    var inputs = new List<NamedOnnxValue>(1) {
                        NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
                    };

                    try {
                        using var results = _session.Run(inputs, _outputNames, runOptions);

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
                        // 各 Tile 的 [dstX, dstX+validW) * [dstY, dstY+validH) 互不重叠，无需加锁
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
                    }
                    catch (OnnxRuntimeException) when (ct.IsCancellationRequested) {
                        // runOptions.Terminate 触发了 ONNX 中断；静默返回，
                        // 由 ParallelOptions.CancellationToken 机制统一发出一次 OperationCanceledException，
                        // 避免手动 throw 与框架内部 throw 叠加产生多次 OCE。
                    }

                    return localBuffer;
                },
                // 线程结束时归还独享 buffer 到 ArrayPool
                localBuffer => ArrayPool<float>.Shared.Return(localBuffer, clearArray: false)
            );

            ct.ThrowIfCancellationRequested();

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

        // ── 配置常量（只读，无状态）──────────────────────────────────────────
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

        // ── 辅助结构（值类型，无状态）────────────────────────────────────────
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

        // ── 单例实例字段（仅 LoadModel 写入，之后只读）──────────────────────
        // 所有 RunAndSave 调用的可变状态（outputImage、runOptions 等）均为方法内局部变量，
        // 确保并发或串行的多次调用之间完全隔离，不存在数据污染风险。
        private InferenceSession? _session;
        private string _inputName = string.Empty;
        private IReadOnlyList<string> _outputNames = [];
        private volatile bool _isLoaded;
    }
}

/*
 * 超分辨率模式下文件体积的预期
 *  QualityRestore 模式（倍率 = 1）：分辨率不变，体积可能比原始 更小（去噪后压缩率更高）
 *  SuperResolution 模式（2x / 4x）：分辨率放大，体积通常会 更大，但如果原图本身噪点很多，偶尔也可能接近原图大小
 */
