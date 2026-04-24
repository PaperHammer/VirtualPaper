using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using VirtualPaper.Common;
using VirtualPaper.Common.Logging;
using VirtualPaper.UIComponent.Utils;
using Windows.Foundation;
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
                GlobalMessageUtil.ShowError($"{LanguageUtil.GetI18n(Constants.I18n.Project_Export_InternalError)}");                
                return null;
            }            

            if (string.IsNullOrWhiteSpace(data.Path)) {
                GlobalMessageUtil.ShowError($"{LanguageUtil.GetI18n(Constants.I18n.Project_Export_PathNotBeNone)}");
                return null;
            }

            try {
                cancellationToken.ThrowIfCancellationRequested();

                string? directory = Path.GetDirectoryName(data.Path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
                    Directory.CreateDirectory(directory);
                }

                CanvasBitmapFileFormat bitmapFormat = data.Format switch {
                    ExportImageFormat.Png => CanvasBitmapFileFormat.Png,
                    ExportImageFormat.Bmp => CanvasBitmapFileFormat.Bmp,
                    ExportImageFormat.Jpeg => CanvasBitmapFileFormat.Jpeg,
                    ExportImageFormat.JpegXR => CanvasBitmapFileFormat.JpegXR,
                    _ => CanvasBitmapFileFormat.Png
                };

                using var exportRenderTarget = new CanvasRenderTarget(
                    renderTarget.Device,
                    (float)size.Width,
                    (float)size.Height,
                    renderTarget.Dpi);

                using (var ds = exportRenderTarget.CreateDrawingSession()) {
                    ds.Clear(Colors.Transparent);
                    var sourceRect = new Rect(0, 0, size.Width, size.Height);
                    var destRect = new Rect(0, 0, size.Width, size.Height);
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

                GlobalMessageUtil.ShowSuccess($"{LanguageUtil.GetI18n(nameof(Constants.I18n.Project_Export_Success))} {data.Path}");

                return data.Path;
            }
            catch (Exception ex) {
                GlobalMessageUtil.ShowError($"{LanguageUtil.GetI18n(nameof(Constants.I18n.Project_Export_Failed))}");
                ArcLog.GetLogger<CanvasRenderTarget>().Error($"{LanguageUtil.GetI18n(nameof(Constants.I18n.Project_Export_Failed))}", ex);
            }

            return null;
        }
    }
}
