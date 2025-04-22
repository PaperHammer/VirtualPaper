using System;
using System.Buffers;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using VirtualPaper.Common.Utils.Archive;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.Security.Cryptography.Certificates;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Workloads.Creation.StaticImg.Models {
    internal partial class InkRenderData : IDisposable {
        public CanvasRenderTarget RenderTarget { get; private set; }
        public bool IsRootBackground { get; }
        public Matrix3x2 Transform { get; private set; } = Matrix3x2.Identity;
        public TaskCompletionSource<bool> IsCompleted => _isCompleted;

        public InkRenderData(ArcSize arcSize, bool isRootBackground = false) {
            _arcSize = arcSize;
            IsRootBackground = isRootBackground;
            InitializeRenderTarget();
        }

        private void InitializeRenderTarget() {
            RenderTarget?.Dispose();
            RenderTarget = new CanvasRenderTarget(
                MainPage.Instance.SharedDevice,
                (float)_arcSize.Width,
                (float)_arcSize.Height,
                _arcSize.Dpi,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                CanvasAlphaMode.Premultiplied);
            IsCompleted.SetResult(true);
        }

        #region save and load
        public async Task SaveWithProgressAsync(
            string filePath,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default) {
            string folderPath = Path.GetDirectoryName(filePath);
            var folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
            var fileName = Path.GetFileName(filePath);
            var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

            // 创建临时文件
            string tempFolderPath = Path.GetTempPath();
            var tempFolder = await StorageFolder.GetFolderFromPathAsync(tempFolderPath);
            var tempFileName = "vw_" + Guid.NewGuid();
            StorageFile tempFile = await tempFolder.CreateFileAsync(tempFileName);

            using (var outputStream = await tempFile.OpenStreamForWriteAsync()) {
                // 先获取PNG数据总大小
                using var pngStream = new InMemoryRandomAccessStream();
                await RenderTarget.SaveAsync(pngStream, CanvasBitmapFileFormat.Png);
                ulong totalBytes = pngStream.Size;
                long processedBytes = 0;

                // 分块处理
                var buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024); // 1MB块
                try {
                    using var inputStream = pngStream.AsStreamForRead();
                    int bytesRead;
                    while ((bytesRead = await inputStream.ReadAsync(buffer, cancellationToken)) > 0) {
                        cancellationToken.ThrowIfCancellationRequested();

                        // 压缩数据
                        var compressed = LZ4Compressor.Compress(buffer.AsSpan(0, bytesRead));

                        // 写入块头(8字节) + 压缩数据
                        await outputStream.WriteAsync(BitConverter.GetBytes(bytesRead).AsMemory(0, 4), cancellationToken); // 原始长度
                        await outputStream.WriteAsync(BitConverter.GetBytes(compressed.Length).AsMemory(0, 4), cancellationToken); // 压缩长度
                        await outputStream.WriteAsync(compressed, cancellationToken); // 压缩数据

                        // 更新进度 (考虑块头8字节的额外开销)
                        processedBytes += bytesRead;
                        progress?.Report((double)processedBytes / totalBytes);
                    }

                    await outputStream.FlushAsync(cancellationToken); // 确保所有数据写入

                    // 原子性替换文件
                    await tempFile.CopyAndReplaceAsync(file);
                    progress?.Report(1.0);
                }
                catch (Exception ex) {
                    throw;
                }
                finally {
                    ArrayPool<byte>.Shared.Return(buffer);
                    await tempFile.DeleteAsync();
                }
            }
        }

        public async Task LoadWithProgressAsync(
            string filePath,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default) {
            var pool = ArrayPool<byte>.Shared;
            var headerBuffer = pool.Rent(8); // 用于读取块头

            string tempFolderPath = Path.GetTempPath();
            var tempFolder = await StorageFolder.GetFolderFromPathAsync(tempFolderPath);
            var tempFileName = "vw_" + Guid.NewGuid();
            StorageFile tempFile = await tempFolder.CreateFileAsync(tempFileName);

            try {
                if (!File.Exists(filePath)) {
                    InitializeBlankRenderTarget(); // 初始化空白画布
                    progress?.Report(1.0);
                    return;
                }

                var folderPath = Path.GetDirectoryName(filePath);
                var folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
                var fileName = Path.GetFileName(filePath);
                StorageFile file = await folder.GetFileAsync(fileName);

                using var inputStream = await file.OpenStreamForReadAsync();

                // 获取文件总大小用于进度计算
                long totalBytes = inputStream.Length;
                long processedBytes = 0;
                using (var outputStream = await tempFile.OpenStreamForWriteAsync()) {
                    while (await inputStream.ReadAsync(headerBuffer.AsMemory(0, 8), cancellationToken) == 8) {
                        cancellationToken.ThrowIfCancellationRequested();

                        int originalLength = BitConverter.ToInt32(headerBuffer, 0);
                        int compressedLength = BitConverter.ToInt32(headerBuffer, 4);

                        var compressedChunk = pool.Rent(compressedLength);
                        try {
                            // 读取压缩数据
                            await inputStream.ReadAsync(compressedChunk.AsMemory(0, compressedLength), cancellationToken);

                            // 解压数据
                            var decompressed = LZ4Compressor.Decompress(
                                compressedChunk.AsSpan(0, compressedLength),
                                originalLength);

                            // 写入解压数据
                            await outputStream.WriteAsync(decompressed, cancellationToken);

                            // 更新进度 (处理过的字节数 = 8字节头 + 压缩数据长度)
                            processedBytes += 8 + compressedLength;
                            progress?.Report((double)processedBytes / totalBytes);
                        }
                        finally {
                            pool.Return(compressedChunk);
                        }
                    }

                    await outputStream.FlushAsync(cancellationToken); // 确保所有数据写入
                }

                // 加载临时文件到画布
                using var stream = await tempFile.OpenReadAsync();
                var bitmap = await CanvasBitmap.LoadAsync(MainPage.Instance.SharedDevice, stream);

                using var ds = RenderTarget.CreateDrawingSession();
                ds.Clear(Colors.Transparent);
                ds.DrawImage(bitmap);

                progress?.Report(1.0);
            }
            catch (Exception ex) {
                throw;
            }
            finally {
                pool.Return(headerBuffer);
                await tempFile.DeleteAsync();
            }
        }
        #endregion

        private void InitializeBlankRenderTarget() {
            using var ds = RenderTarget.CreateDrawingSession();
            if (IsRootBackground) {
                ds.Clear(Colors.White);
                ds.DrawRectangle(new Rect(0, 0, RenderTarget.Size.Width, RenderTarget.Size.Height),
                                Colors.Transparent, 1f);
            }
            else {
                ds.Clear(Colors.Transparent);
            }
        }

        internal InkRenderData Clone() {
            var newRender = new InkRenderData(_arcSize);
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
                if (IsRootBackground) {
                    ds.Clear(Colors.White);
                }
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
        private CanvasBitmap _cachedContent;
        private object _lockResize = new();
        private readonly TaskCompletionSource<bool> _isCompleted = new();
    }
}
