using System;
using System.Buffers;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using VirtualPaper.Common.Utils.Archive;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.Storage.Streams;
using Workloads.Creation.StaticImg.Extensions;

namespace Workloads.Creation.StaticImg.Models {
    public partial class InkRenderData : IDisposable {
        public CanvasRenderTarget RenderTarget { get; private set; }
        public bool IsNeedBackground { get; }
        public Matrix3x2 Transform { get; private set; } = Matrix3x2.Identity;
        public TaskCompletionSource<bool> IsReady => _isReady;

        public InkRenderData(ArcSize arcSize, bool isNeedBackground = false) {
            _arcSize = arcSize;
            IsNeedBackground = isNeedBackground;
            InitializeRenderTarget();
        }

        private void InitializeRenderTarget() {
            RenderTarget?.Dispose();
            RenderTarget = new CanvasRenderTarget(
                MainPage.Instance.SharedDevice,
                (float)_arcSize.Width,
                (float)_arcSize.Height,
                _arcSize.Dpi,
                MainPage.Instance.SharedFormat,
                MainPage.Instance.SharedAlphaMode);
            if (IsNeedBackground) InitializeBlankRenderTarget(); // 初始化空白画布
            IsReady.SetResult(true);
        }

        #region save and load
        /// <summary>
        /// 保存渲染数据到流
        /// </summary>
        public async Task SaveAsync(
            Stream outputStream,
            IProgress<double> progress = null,
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
                    MainPage.Instance.SharedDevice,
                    fileStream.AsRandomAccessStream());

                using (var ds = RenderTarget.CreateDrawingSession()) {
                    ds.Clear(Colors.Transparent);
                    ds.DrawImage(bitmap);
                }

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
                ds.DrawRectangle(new Rect(0, 0, RenderTarget.Size.Width, RenderTarget.Size.Height),
                                Colors.Transparent, 1f);
            }
            else {
                ds.Clear(Colors.Transparent);
            }
        }

        internal InkRenderData Clone() {
            var newRender = new InkRenderData(_arcSize) {
                RenderTarget = this.RenderTarget.Clone()
            };
            newRender.IsReady.SetResult(true);
            return newRender;
        }

        public async Task ResizeRenderTargetAsync(ArcSize arcSize) {
            _arcSize = arcSize;
            await Task.Run(() => {
                lock (_lockResize) {
                    switch (arcSize.Rebuild) {
                        case RebuildMode.ResizeExpand:
                            ResizeRenderTargetWithExpand();
                            break;
                        case RebuildMode.ResizeScale:
                            ResizeRenderTargetWithScale();
                            break;
                        case RebuildMode.RotateLeft or RebuildMode.RotateRight:
                            Rotate(arcSize.Rebuild == RebuildMode.RotateLeft);
                            break;
                        case RebuildMode.FlipHorizontal or RebuildMode.FlipVertical:
                            Flip(arcSize.Rebuild == RebuildMode.FlipHorizontal);
                            break;
                        case RebuildMode.None:
                        default:
                            break;
                    }
                }
            });
        }

        private void ResizeRenderTargetWithScale() {
            // 保留旧内容
            if (RenderTarget != null) {
                _cachedContent = CanvasBitmap.CreateFromBytes(
                    MainPage.Instance.SharedDevice,
                    RenderTarget.GetPixelBytes(),
                    (int)RenderTarget.SizeInPixels.Width,
                    (int)RenderTarget.SizeInPixels.Height,
                    RenderTarget.Format,
                    RenderTarget.Dpi,
                    RenderTarget.AlphaMode);
            }

            // 创建新目标
            RenderTarget?.Dispose();
            RenderTarget = new CanvasRenderTarget(
                MainPage.Instance.SharedDevice,
                (float)_arcSize.Width,
                (float)_arcSize.Height,
                _arcSize.Dpi,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                CanvasAlphaMode.Premultiplied);

            // 恢复内容
            if (_cachedContent != null) {
                using (var ds = RenderTarget.CreateDrawingSession()) {
                    var destRect = new Rect(0, 0, _arcSize.Width, _arcSize.Height);
                    ds.DrawImage(_cachedContent, destRect);
                }
                _cachedContent.Dispose();
            }
        }

        private void ResizeRenderTargetWithExpand() {
            // 保留原始内容
            Size originalSize = default;
            if (RenderTarget != null) {
                _cachedContent = CanvasBitmap.CreateFromBytes(
                   MainPage.Instance.SharedDevice,
                   RenderTarget.GetPixelBytes(),
                   (int)RenderTarget.SizeInPixels.Width,
                   (int)RenderTarget.SizeInPixels.Height,
                   RenderTarget.Format,
                   RenderTarget.Dpi,
                   RenderTarget.AlphaMode);

                originalSize = new Size(
                    _cachedContent.SizeInPixels.Width / _cachedContent.Dpi * 96,
                    _cachedContent.SizeInPixels.Height / _cachedContent.Dpi * 96);
            }

            // 创建新目标（扩展尺寸）
            RenderTarget?.Dispose();
            RenderTarget = new CanvasRenderTarget(
                MainPage.Instance.SharedDevice,
                (float)_arcSize.Width,
                (float)_arcSize.Height,
                _arcSize.Dpi,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                CanvasAlphaMode.Premultiplied);

            // 绘制到新目标
            using (var ds = RenderTarget.CreateDrawingSession()) {
                //if (IsNeedBackground) {
                //    ds.Clear(Colors.White);
                //}
                // 在左上角绘制原始内容（1:1不缩放）
                if (_cachedContent != null) {
                    var contentRect = new Rect(
                        0, 0,
                        originalSize.Width,
                        originalSize.Height);

                    ds.DrawImage(_cachedContent, contentRect);
                }
            }

            _cachedContent?.Dispose();
            _cachedContent = null;
        }

        private void Rotate(bool isLeft) {
            var original = GetOriginalContent();
            var newSize = _arcSize.GetSize();
            var newTarget = CreateNewRenderTarget(newSize);
            using (var ds = newTarget.CreateDrawingSession()) {
                var newCenter = new Vector2((float)(newSize.Width / 2f), (float)(newSize.Height / 2f));
                var translateX = (newSize.Width - original.Size.Width) / 2f; // 平移图像到新画布中心
                var translateY = (newSize.Height - original.Size.Height) / 2f;

                // 平移 -> 旋转
                ds.Transform = Matrix3x2.CreateTranslation((float)translateX, (float)translateY) *
                    (isLeft ? Matrix3x2.CreateRotation(-(float)(Math.PI / 2), newCenter) :
                    Matrix3x2.CreateRotation((float)(Math.PI / 2), newCenter));
                this.Transform = ds.Transform;
                ds.DrawImage(original);
            }
            UpdateRenderTarget(newTarget);
        }

        private void Flip(bool isHorizontal) {
            var original = GetOriginalContent();
            var newTarget = CreateNewRenderTarget(_arcSize.GetSize());
            using (var ds = newTarget.CreateDrawingSession()) {
                var center = new Vector2((float)(original.Size.Width / 2f), (float)(original.Size.Height / 2f));
                ds.Transform = isHorizontal ? Matrix3x2.CreateScale(-1, 1, center) :
                    Matrix3x2.CreateScale(1, -1, center);
                ds.DrawImage(original);
            }
            UpdateRenderTarget(newTarget);
        }

        private CanvasBitmap GetOriginalContent() {
            return CanvasBitmap.CreateFromBytes(
                MainPage.Instance.SharedDevice,
                RenderTarget.GetPixelBytes(),
                (int)RenderTarget.SizeInPixels.Width,
                (int)RenderTarget.SizeInPixels.Height,
                RenderTarget.Format,
                RenderTarget.Dpi,
                RenderTarget.AlphaMode);
        }

        private CanvasRenderTarget CreateNewRenderTarget(Size size) {
            return new CanvasRenderTarget(
                MainPage.Instance.SharedDevice,
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

        public void Dispose() {
            RenderTarget?.Dispose();
            RenderTarget = null;
            GC.SuppressFinalize(this);
        }

        private ArcSize _arcSize;
        private CanvasBitmap? _cachedContent;
        private readonly object _lockResize = new();
        private readonly TaskCompletionSource<bool> _isReady = new();
    }
}
