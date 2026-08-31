using System;
using System.Buffers;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Archive;
using VirtualPaper.UIComponent.Utils;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.Storage.Streams;
using Workloads.Creation.StaticImg.Core.Utils;
using Workloads.Creation.StaticImg.Extensions;

namespace Workloads.Creation.StaticImg.Models {
    public partial class InkRenderData : IDisposable {
        public event EventHandler? OnceRenderCompleted;

        public CanvasRenderTarget RenderTarget { get; private set; } = null!;
        public bool IsNeedBackground { get; }
        public TaskCompletionSource<bool> IsInited => _isInited;
        public TaskCompletionSource<bool> IsReady => _isReady;

        public InkRenderData(InkProjectSession session, ArcSize arcSize, bool isNeedBackground = false) {
            _session = session;
            ResetSize(arcSize);
            IsNeedBackground = isNeedBackground;
            IsReady.Task.ContinueWith(t => {
                if (t.Status == TaskStatus.RanToCompletion && t.Result) {
                    HandleOnceRenderCompleted();
                }
            });
            Init();
        }

        public void Init() {
            RenderTarget?.Dispose();
            RenderTarget = new CanvasRenderTarget(
                InkProjectSession.SharedDevice,
                (float)_arcSize.Width,
                (float)_arcSize.Height,
                _arcSize.Dpi,
                _session.SharedFormat,
                _session.SharedAlphaMode);
            InitializeBlankRenderTarget();
            IsInited.TrySetResult(true);
        }

        public void HandleOnceRenderCompleted() {
            OnceRenderCompleted?.Invoke(this, EventArgs.Empty);
        }

        #region save and load
        /// <summary>
        /// 保存渲染数据到流
        /// </summary>
        public async Task SaveAsync(
            Stream outputStream,
            IProgress<double>? progress = null,
            CancellationToken ct = default) {
            using var pngStream = new InMemoryRandomAccessStream();
            await RenderTarget.SaveAsync(pngStream, CanvasBitmapFileFormat.Png);

            long totalBytes = (long)pngStream.Size;
            long processedBytes = 0;
            var buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024); // 1MB块

            try {
                using var sourceStream = pngStream.AsStreamForRead();
                int bytesRead;
                while ((bytesRead = await sourceStream.ReadAsync(buffer, ct)) > 0) {
                    ct.ThrowIfCancellationRequested();

                    // 压缩数据
                    var compressed = LZ4Compressor.Compress(buffer.AsSpan(0, bytesRead));

                    // 写入块头(8字节) + 压缩数据
                    await outputStream.WriteAsync(BitConverter.GetBytes(bytesRead).AsMemory(0, 4), ct);
                    await outputStream.WriteAsync(BitConverter.GetBytes(compressed.Length).AsMemory(0, 4), ct);
                    await outputStream.WriteAsync(compressed, ct);

                    // 更新进度
                    processedBytes += bytesRead;
                    progress?.Report((double)processedBytes / totalBytes);
                }
            }
            finally {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// 将 PNG 编码结果直接写入目标流，不创建完整 PNG 内存副本。
        /// 相对流保证编码器定位到 0 时只回到 PNG 区段起点。
        /// </summary>
        internal async Task SavePngAsync(
            Stream outputStream,
            CancellationToken ct = default) {
            ArgumentNullException.ThrowIfNull(outputStream);
            if (!outputStream.CanWrite || !outputStream.CanSeek)
                throw new ArgumentException("PNG 流必须可写且可寻址。", nameof(outputStream));

            ct.ThrowIfCancellationRequested();
            long pngStart = outputStream.Position;
            using var pngSegment = new RelativeStream(outputStream, pngStart, leaveOpen: true);
            using var randomAccessStream = pngSegment.AsRandomAccessStream();
            await RenderTarget.SaveAsync(randomAccessStream, CanvasBitmapFileFormat.Png);
            ct.ThrowIfCancellationRequested();
            outputStream.Position = checked(pngStart + pngSegment.Length);
        }

        /// <summary>
        /// 从流加载渲染数据
        /// </summary>
        public async Task LoadAsync(
            Stream inputStream,
            IProgress<double>? progress = null,
            CancellationToken ct = default) {
            long totalBytes = inputStream.Length;
            long processedBytes = 0;
            var tempFile = Path.GetTempFileName();

            try {
                using (var outputStream = File.OpenWrite(tempFile)) {
                    var headerBuffer = ArrayPool<byte>.Shared.Rent(8);

                    try {
                        while (inputStream.Position < inputStream.Length) {
                            await inputStream.ReadExactlyAsync(headerBuffer.AsMemory(0, 8), ct);
                            ct.ThrowIfCancellationRequested();

                            int originalLength = BitConverter.ToInt32(headerBuffer, 0);
                            int compressedLength = BitConverter.ToInt32(headerBuffer, 4);

                            var compressedChunk = ArrayPool<byte>.Shared.Rent(compressedLength);
                            try {
                                await inputStream.ReadExactlyAsync(
                                    compressedChunk.AsMemory(0, compressedLength),
                                    ct);
                                var decompressed = LZ4Compressor.Decompress(
                                    compressedChunk.AsSpan(0, compressedLength),
                                    originalLength);

                                await outputStream.WriteAsync(decompressed, ct);
                                processedBytes += 8 + compressedLength;
                                progress?.Report((double)processedBytes / totalBytes);
                            }
                            finally {
                                ArrayPool<byte>.Shared.Return(compressedChunk);
                            }
                        }
                    }
                    finally {
                        ArrayPool<byte>.Shared.Return(headerBuffer);
                    }
                }

                // 加载到渲染目标
                using var fileStream = File.OpenRead(tempFile);
                using var bitmap = await CanvasBitmap.LoadAsync(
                    InkProjectSession.SharedDevice,
                    fileStream.AsRandomAccessStream());

                using (var ds = RenderTarget.CreateDrawingSession()) {
                    ds.Clear(Colors.Transparent);
                    ds.DrawImage(bitmap);
                }
            }
            finally {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }        

        /// <summary>
        /// 直接从当前流的剩余区段解码 PNG，不创建完整 PNG 内存副本或临时文件。
        /// </summary>
        internal async Task LoadPngAsync(
            Stream inputStream,
            CancellationToken ct = default) {
            ArgumentNullException.ThrowIfNull(inputStream);
            if (!inputStream.CanRead || !inputStream.CanSeek)
                throw new ArgumentException("PNG 流必须可读且可寻址。", nameof(inputStream));

            ct.ThrowIfCancellationRequested();
            long pngStart = inputStream.Position;
            long pngLength = inputStream.Length - pngStart;
            if (pngLength <= 0) throw new InvalidDataException("PNG 图层数据为空。");

            using var pngSegment = new RelativeStream(
                inputStream,
                pngStart,
                pngLength,
                leaveOpen: true);
            using var randomAccessStream = pngSegment.AsRandomAccessStream();
            using var bitmap = await CanvasBitmap.LoadAsync(
                InkProjectSession.SharedDevice,
                randomAccessStream);
            ct.ThrowIfCancellationRequested();

            using var ds = RenderTarget.CreateDrawingSession();
            ds.Clear(Colors.Transparent);
            ds.DrawImage(bitmap);
            inputStream.Position = checked(pngStart + pngLength);
        }
        #endregion

        private void InitializeBlankRenderTarget() {
            using var ds = RenderTarget.CreateDrawingSession();
            // CanvasRenderTarget 的初始内容未定义。普通新图层也必须显式清空，
            // 否则复用的 GPU 内存可能呈现为刚刚操作过的图层内容。
            ds.Clear(IsNeedBackground ? Colors.White : Colors.Transparent);
        }

        internal InkRenderData Clone() {
            var newRender = new InkRenderData(_session, _arcSize) {
                RenderTarget = this.RenderTarget.Clone(),
            };
            
            return newRender;
        }

        public void ResizeAndSetPixels(ArcSize newSize, byte[]? pixels) {
            if (pixels == null) {
                GlobalMessageUtil.ShowError("Text_ResizeDataUnavailable", isNeedLocalizer: true);
                ArcLog.GetLogger<InkRenderData>().Error("Resize data is null");
                return;
            }

            ResetSize(newSize);
            RenderTarget?.Dispose();
            RenderTarget = new CanvasRenderTarget(
                InkProjectSession.SharedDevice,
                (float)_arcSize.Width,
                (float)_arcSize.Height,
                _arcSize.Dpi,
                _session.SharedFormat,
                _session.SharedAlphaMode);
            RenderTarget.SetPixelBytes(pixels);
        }

        public async Task ResizeRenderTargetAsync(ArcSize arcSize) {
            await Task.Run(() => {
                lock (_lockResize) {
                    switch (arcSize.Rebuild) {
                        case RebuildMode.ResizeExpand:
                            ResizeRenderTargetWithExpand(arcSize);
                            break;
                        case RebuildMode.ResizeScale:
                            ResizeRenderTargetWithScale(arcSize);
                            break;
                        case RebuildMode.RotateLeft or RebuildMode.RotateRight:
                            Rotate(arcSize);
                            break;
                        case RebuildMode.FlipHorizontal or RebuildMode.FlipVertical:
                            Flip(arcSize);
                            break;
                        case RebuildMode.None:
                        default:
                            break;
                    }
                }
            });
            ResetSize(arcSize);
        }

        public void ResetSize(ArcSize arcSize) {
            _arcSize = arcSize;
        }

        private void ResizeRenderTargetWithScale(ArcSize arcSize) {            
            var oldTarget = RenderTarget;
            RenderTarget = new CanvasRenderTarget(
                InkProjectSession.SharedDevice,
                (float)arcSize.Width,
                (float)arcSize.Height,
                arcSize.Dpi,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                CanvasAlphaMode.Premultiplied);

            using (var ds = RenderTarget.CreateDrawingSession()) {
                ds.Clear(Colors.Transparent);

                if (oldTarget != null) {
                    var destRect = new Rect(0, 0, arcSize.Width, arcSize.Height);

                    // 对于 Scale 操作，使用高质量插值算法，缩放后的图像更清晰
                    ds.DrawImage(oldTarget, destRect, oldTarget.Bounds, 1.0f, CanvasImageInterpolation.HighQualityCubic);
                }
            }

            oldTarget?.Dispose();
        }

        private void ResizeRenderTargetWithExpand(ArcSize arcSize) {            
            var oldTarget = RenderTarget;
            RenderTarget = new CanvasRenderTarget(
                InkProjectSession.SharedDevice,
                (float)arcSize.Width,
                (float)arcSize.Height,
                arcSize.Dpi,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                CanvasAlphaMode.Premultiplied);

            using (var ds = RenderTarget.CreateDrawingSession()) {
                ds.Clear(Colors.Transparent);

                if (oldTarget != null) {
                    // 废弃原先复杂的 DPI 计算和 contentRect
                    // Win2D 的 DrawImage(image, x, y) 默认就是 1:1 的 DIPs 映射
                    // 直接画在 (0,0) 坐标即可，超出的部分会自动被新画布裁剪，不够的部分就是透明背景
                    ds.DrawImage(oldTarget, 0, 0);
                }
            }

            oldTarget?.Dispose();
        }

        private void Rotate(ArcSize targetArcSize) {
            CanvasRenderTarget original = RenderTarget;
            var newSize = targetArcSize.ToSize();
            var newTarget = CreateNewRenderTarget(targetArcSize, original);
            try {
                using (var ds = newTarget.CreateDrawingSession()) {
                    ds.Clear(Colors.Transparent);

                    // 使用标准的 90 度边缘映射矩阵，避免 width/2f 带来的浮点数漂移
                    if (targetArcSize.Rebuild == RebuildMode.RotateLeft) {
                        // 逆时针 90 度：先绕 (0,0) 旋转 -90度，然后向下平移新画布的高度
                        ds.Transform = Matrix3x2.CreateRotation(-(float)(Math.PI / 2)) *
                                       Matrix3x2.CreateTranslation(0, (float)newSize.Height);
                    }
                    else {
                        // 顺时针 90 度：先绕 (0,0) 旋转 90度，然后向右平移新画布的宽度
                        ds.Transform = Matrix3x2.CreateRotation((float)(Math.PI / 2)) *
                                       Matrix3x2.CreateTranslation((float)newSize.Width, 0);
                    }

                    // RenderTarget 可直接作为 GPU 图像源，不需要读取并重建 CPU 像素。
                    ds.DrawImage(original);
                }
            }
            catch {
                newTarget.Dispose();
                throw;
            }

            UpdateRenderTarget(newTarget);
        }

        private void Flip(ArcSize targetArcSize) {
            CanvasRenderTarget original = RenderTarget;
            var newTarget = CreateNewRenderTarget(targetArcSize, original);
            try {
                using (var ds = newTarget.CreateDrawingSession()) {
                    ds.Clear(Colors.Transparent);

                    float width = (float)Math.Round(original.Size.Width);
                    float height = (float)Math.Round(original.Size.Height);

                    // 抛弃带小数点的 Center，使用“原点缩放 + 物理平移”算法
                    if (targetArcSize.Rebuild == RebuildMode.FlipHorizontal) {
                        // 水平翻转：X轴变为-1（画面跑到左边负数区），然后再向右平移 width 距离拉回来
                        ds.Transform = Matrix3x2.CreateScale(-1, 1) * Matrix3x2.CreateTranslation(width, 0);
                    }
                    else {
                        // 垂直翻转：Y轴变为-1（画面跑到上边负数区），然后再向下平移 height 距离拉回来
                        ds.Transform = Matrix3x2.CreateScale(1, -1) * Matrix3x2.CreateTranslation(0, height);
                    }

                    // 同尺寸翻转使用最近邻采样，保持像素边缘且不经过 CPU。
                    ds.DrawImage(original, 0, 0, original.Bounds, 1.0f, CanvasImageInterpolation.NearestNeighbor);
                }
            }
            catch {
                newTarget.Dispose();
                throw;
            }

            UpdateRenderTarget(newTarget);
        }

        private static CanvasRenderTarget CreateNewRenderTarget(
            ArcSize targetArcSize,
            CanvasRenderTarget source) {
            return new CanvasRenderTarget(
                source,
                (float)targetArcSize.Width,
                (float)targetArcSize.Height,
                targetArcSize.Dpi,
                source.Format,
                source.AlphaMode);
        }

        private void UpdateRenderTarget(CanvasRenderTarget newTarget) {
            var oldContent = RenderTarget;
            RenderTarget = newTarget;
            oldContent?.Dispose();
        }        

        public void Dispose() {
            RenderTarget?.Dispose();
            RenderTarget = null;
            GC.SuppressFinalize(this);
        }

        private readonly InkProjectSession _session;
        private ArcSize _arcSize;
        private readonly object _lockResize = new();
        private readonly TaskCompletionSource<bool> _isReady = new();
        private readonly TaskCompletionSource<bool> _isInited = new();
    }
}
