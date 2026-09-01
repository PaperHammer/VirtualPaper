using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.UIComponent.Utils;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.UI;
using Workloads.Utils.DraftUtils.Models;

namespace Workloads.Creation.StaticImg.Extensions {
    public static class CanvasRenderTargetExtension {
        public static CanvasRenderTarget Clone(this CanvasRenderTarget source) {
            var clone = new CanvasRenderTarget(
                source.Device,
                source.SizeInPixels.Width,
                source.SizeInPixels.Height,
                source.Dpi,
                source.Format,
                source.AlphaMode);

            // 运行在 GPU 上
            // GetPixelBytes 运行在 CPU 上
            clone.CopyPixelsFromBitmap(source);

            return clone;
        }

        /// <summary>
        /// 将 CanvasRenderTarget 异步导出为指定的图片文件
        /// </summary>
        /// <param name="renderTarget">Win2D 渲染目标</param>
        /// <param name="data">导出参数数据包</param>
        /// <param name="cancellationToken">取消令牌</param>
        public static async Task<string?> ExportAsync(
            this CanvasRenderTarget? renderTarget,
            Size size,
            ExportDataStaticImg data,
            CancellationToken cancellationToken = default) {
            if (renderTarget == null) {
                GlobalMessageUtil.ShowError(LanguageUtil.GetI18n(nameof(Constants.I18n.Project_Export_InternalError)));
                return null;
            }

            if (string.IsNullOrWhiteSpace(data.Path)) {
                GlobalMessageUtil.ShowError(LanguageUtil.GetI18n(nameof(Constants.I18n.Project_Export_PathNotBeNone)));
                return null;
            }

            try {
                cancellationToken.ThrowIfCancellationRequested();

                string directory = Path.GetDirectoryName(Path.GetFullPath(data.Path))!;
                if (!Directory.Exists(directory)) {
                    Directory.CreateDirectory(directory);
                }

                CanvasBitmapFileFormat bitmapFormat = data.Format switch {
                    ExportImageFormat.Png => CanvasBitmapFileFormat.Png,
                    ExportImageFormat.Bmp => CanvasBitmapFileFormat.Bmp,
                    ExportImageFormat.Jpeg => CanvasBitmapFileFormat.Jpeg,
                    ExportImageFormat.JpegXR => CanvasBitmapFileFormat.JpegXR,
                    _ => CanvasBitmapFileFormat.Png
                };

                bool supportsTransparency = SupportsTransparency(data.Format);
                using var exportRenderTarget = new CanvasRenderTarget(
                    renderTarget.Device,
                    (float)size.Width,
                    (float)size.Height,
                    renderTarget.Dpi,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    CanvasAlphaMode.Premultiplied);

                using (var ds = exportRenderTarget.CreateDrawingSession()) {
                    var sourceRect = new Rect(0, 0, size.Width, size.Height);
                    var destRect = new Rect(0, 0, size.Width, size.Height);

                    if (supportsTransparency) {
                        // PNG/JPEG XR 支持 Alpha。Copy 模式直接复制合成结果的 RGBA，
                        // 避免透明像素再次经过 SourceOver 混合后丢失透明度或颜色信息。
                        ds.Clear(Color.FromArgb(0, 0, 0, 0));
                        ds.Blend = CanvasBlend.Copy;
                    }
                    else {
                        // JPEG/BMP 没有可靠的透明通道表示，显式铺白底，避免透明区域
                        // 被编码器或查看器解释成不可预测的黑色背景。
                        ds.Clear(Color.FromArgb(255, 255, 255, 255));
                        ds.Blend = CanvasBlend.SourceOver;
                    }

                    ds.DrawImage(renderTarget, destRect, sourceRect);
                }

                using var fileStream = new FileStream(
                    data.Path,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    options: FileOptions.Asynchronous);

                using var randomAccessStream = fileStream.AsRandomAccessStream();
                await exportRenderTarget.SaveAsync(randomAccessStream, bitmapFormat).AsTask(cancellationToken);
                await randomAccessStream.FlushAsync().AsTask(cancellationToken);

                GlobalMessageUtil.ShowSuccess(
                    $"{LanguageUtil.GetI18n(nameof(Constants.I18n.Project_Export_Success))} {data.Path}",
                    LanguageUtil.GetI18n(Constants.I18n.Text_ShowOnDisk),
                    () => _ = FileUtil.OpenFolderAsync(data.Path),
                    replaceExisting: true,
                    key: nameof(Constants.I18n.Project_Export_Success));

                return data.Path;
            }
            catch (Exception ex) {
                GlobalMessageUtil.ShowError($"{LanguageUtil.GetI18n(nameof(Constants.I18n.Project_Export_Failed))}");
                ArcLog.GetLogger<CanvasRenderTarget>().Error($"{LanguageUtil.GetI18n(nameof(Constants.I18n.Project_Export_Failed))}", ex);
            }

            return null;
        }

        /// <summary>
        /// Win2D 导出格式中，PNG 和 JPEG XR 可以保留 BGRA 像素的 Alpha 通道。
        /// JPEG 不支持透明；BMP 的 Alpha 兼容性依赖读取方，因此按不透明格式处理。
        /// </summary>
        internal static bool SupportsTransparency(ExportImageFormat format) =>
            format is ExportImageFormat.Png or ExportImageFormat.JpegXR;
    }
}
