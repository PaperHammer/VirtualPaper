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
using Workloads.Utils.DraftUtils.Models;

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
            if (IsNeedBackground) InitializeBlankRenderTarget(); // 初始化空白画布
            IsInited.SetResult(true);
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
                        while (await inputStream.ReadAsync(headerBuffer.AsMemory(0, 8), ct) == 8) {
                            ct.ThrowIfCancellationRequested();

                            int originalLength = BitConverter.ToInt32(headerBuffer, 0);
                            int compressedLength = BitConverter.ToInt32(headerBuffer, 4);

                            var compressedChunk = ArrayPool<byte>.Shared.Rent(compressedLength);
                            try {
                                await inputStream.ReadAsync(compressedChunk.AsMemory(0, compressedLength), ct);
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
                var bitmap = await CanvasBitmap.LoadAsync(
                    InkProjectSession.SharedDevice,
                    fileStream.AsRandomAccessStream());

                using (var ds = RenderTarget.CreateDrawingSession()) {
                    ds.Clear(Colors.Transparent);
                    ds.DrawImage(bitmap);
                }

                //var colors = RenderTarget.GetPixelBytes();

                IsReady.SetResult(true);
            }
            finally {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }        
        #endregion

        private void InitializeBlankRenderTarget() {
            using var ds = RenderTarget.CreateDrawingSession();
            if (IsNeedBackground) {
                ds.Clear(Colors.White);
            }
            else {
                ds.Clear(Colors.Transparent);
            }
        }

        internal InkRenderData Clone() {
            var newRender = new InkRenderData(_session, _arcSize) {
                RenderTarget = this.RenderTarget.Clone(),
            };
            
            return newRender;
        }

        public void ResizeAndSetPixels(ArcSize newSize, byte[]? pixels) {
            if (pixels == null) {
                GlobalMessageUtil.ShowError("Resize data is null");
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
            var original = GetOriginalContent();
            var newSize = targetArcSize.ToSize();
            var newTarget = CreateNewRenderTarget(newSize);
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
                //this.Transform = ds.Transform;
                ds.DrawImage(original);
            }
            UpdateRenderTarget(newTarget);
        }

        private void Flip(ArcSize targetArcSize) {
            var original = GetOriginalContent();
            var newTarget = CreateNewRenderTarget(targetArcSize.ToSize());
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

                // 设置高质量插值模式，防止极个别情况下的边缘采样溢出
                ds.DrawImage(original, 0, 0, original.Bounds, 1.0f, CanvasImageInterpolation.NearestNeighbor);
            }
            UpdateRenderTarget(newTarget);
        }

        private CanvasBitmap GetOriginalContent() {
            return CanvasBitmap.CreateFromBytes(
                InkProjectSession.SharedDevice,
                RenderTarget.GetPixelBytes(),
                (int)RenderTarget.SizeInPixels.Width,
                (int)RenderTarget.SizeInPixels.Height,
                RenderTarget.Format,
                RenderTarget.Dpi,
                RenderTarget.AlphaMode);
        }

        private CanvasRenderTarget CreateNewRenderTarget(Size size) {
            return new CanvasRenderTarget(
                InkProjectSession.SharedDevice,
                (float)size.Width,
                (float)size.Height,
                _arcSize.Dpi,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                CanvasAlphaMode.Premultiplied);
        }

        private void UpdateRenderTarget(CanvasRenderTarget newTarget) {
            var oldContent = RenderTarget;
            RenderTarget = newTarget;
            oldContent?.Dispose();
        }

        /// <summary>
        /// 将当前渲染数据导出为常规图片格式
        /// </summary>
        /// <param name="data">导出参数数据包，包含路径、名称、格式、比例和精度等</param>
        public async Task ExportAsync(ExportDataStaticImg data) {
            if (RenderTarget == null) {
                ArcLog.GetLogger<InkRenderData>().Error("RenderTarget is null, cannot export image.");
                GlobalMessageUtil.ShowError("RenderTarget is null, cannot export image.");
                return;
            }

            if (data.ScalePercentage <= 0) {
                GlobalMessageUtil.ShowError("Scale percentage must be greater than 0.");
                return;
            }

            // 根据缩放比例计算目标尺寸
            float scale = (float)(data.ScalePercentage / 100.0);
            float targetWidth = (float)(RenderTarget.Size.Width * scale);
            float targetHeight = (float)(RenderTarget.Size.Height * scale);

            // 创建临时画布用于绘制（如果需要缩放或加背景）
            using var tempTarget = new CanvasRenderTarget(
                InkProjectSession.SharedDevice,
                targetWidth,
                targetHeight,
                RenderTarget.Dpi,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                CanvasAlphaMode.Premultiplied);

            using (var ds = tempTarget.CreateDrawingSession()) {
                // 传统 Jpeg 不支持透明度，强制填充白色背景，避免透明变成黑色
                // JpegXR 支持透明通道，保持 Transparent 即可
                if (data.Format == ExportImageFormat.Jpeg) {
                    ds.Clear(Colors.White);
                }
                else {
                    ds.Clear(Colors.Transparent);
                }

                // 如果缩放比例正好是 100%，直接原尺寸绘制
                if (Math.Abs(scale - 1.0f) < 0.001f) {
                    ds.DrawImage(RenderTarget, 0, 0);
                }
                // 否则执行高质量缩放绘制
                else {
                    var destRect = new Rect(0, 0, targetWidth, targetHeight);
                    ds.DrawImage(RenderTarget, destRect, RenderTarget.Bounds, 1.0f, CanvasImageInterpolation.HighQualityCubic);
                }
            }

            string extension;
            CanvasBitmapFileFormat win2dFormat;
            switch (data.Format) {
                case ExportImageFormat.Png:
                    win2dFormat = CanvasBitmapFileFormat.Png;
                    extension = ".png";
                    break;
                case ExportImageFormat.Bmp:
                    win2dFormat = CanvasBitmapFileFormat.Bmp;
                    extension = ".bmp";
                    break;
                case ExportImageFormat.Jpeg:
                    win2dFormat = CanvasBitmapFileFormat.Jpeg;
                    extension = ".jpg";
                    break;
                case ExportImageFormat.JpegXR:
                    win2dFormat = CanvasBitmapFileFormat.JpegXR;
                    extension = ".jxr";
                    break;
                default:
                    win2dFormat = CanvasBitmapFileFormat.Png;
                    extension = ".png";
                    break;
            }

            if (!Directory.Exists(data.Path)) {
                Directory.CreateDirectory(data.Path);
            }

            // 拼接完整的绝对路径
            string fullPath = Path.Combine(data.Path, $"{data.Name}{extension}");

            //  开启文件流并使用 Win2D 写入
            using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            using var ras = fileStream.AsRandomAccessStream();

            if (win2dFormat == CanvasBitmapFileFormat.Jpeg || win2dFormat == CanvasBitmapFileFormat.JpegXR) {
                // Win2D 的 quality 参数要求是 0.0f 到 1.0f 之间的浮点数
                // 默认给 0.9f (90%质量)，以平衡清晰度和体积
                float quality = data.JpegQuality ?? 0.9f;
                await tempTarget.SaveAsync(ras, win2dFormat, quality);
            }
            else {
                // Png 和 Bmp 走无损保存，不接受 quality 参数
                await tempTarget.SaveAsync(ras, win2dFormat);
            }

            await ras.FlushAsync();

            GlobalMessageUtil.ShowSuccess($"Successfully exported static image to: {fullPath}");
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
