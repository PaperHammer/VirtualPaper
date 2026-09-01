using System.Reflection;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VirtualPaper.Common;
using VirtualPaper.UIComponent.Utils;
using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Workloads.Creation.StaticImg.Extensions;
using Workloads.Utils.DraftUtils.Models;

namespace VirtualPaper.UI.Test.T_StaticImg {
    [TestClass]
    public class ExportExperienceTests {
        [TestMethod]
        public void SupportsTransparency_OnlyForAlphaCapableFormats() {
            Assert.IsTrue(SupportsTransparency(ExportImageFormat.Png));
            Assert.IsTrue(SupportsTransparency(ExportImageFormat.JpegXR));
            Assert.IsFalse(SupportsTransparency(ExportImageFormat.Jpeg));
            Assert.IsFalse(SupportsTransparency(ExportImageFormat.Bmp));
        }

        [TestMethod]
        [DataRow(ExportImageFormat.Png)]
        [DataRow(ExportImageFormat.JpegXR)]
        public async Task Export_AlphaCapableFormatPreservesTransparentPixels(ExportImageFormat format) {
            string extension = format == ExportImageFormat.Png ? ".png" : ".jxr";
            string path = Path.Combine(Path.GetTempPath(), $"staticimg-alpha-{Guid.NewGuid():N}{extension}");

            try {
                using var target = new CanvasRenderTarget(
                    CanvasDevice.GetSharedDevice(),
                    2,
                    1,
                    96,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    CanvasAlphaMode.Premultiplied);
                using (CanvasDrawingSession ds = target.CreateDrawingSession()) {
                    ds.Clear(Windows.UI.Color.FromArgb(0, 0, 0, 0));
                    ds.FillRectangle(1, 0, 1, 1, Windows.UI.Color.FromArgb(128, 255, 0, 0));
                }

                string? exported = await target.ExportAsync(
                    target.Size,
                    new ExportDataStaticImg(Path.GetFileName(path), path, format));
                Assert.AreEqual(path, exported);

                StorageFile file = await StorageFile.GetFileFromPathAsync(path);
                using var stream = await file.OpenReadAsync();
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                PixelDataProvider pixels = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Straight,
                    new BitmapTransform(),
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage);
                byte[] bytes = pixels.DetachPixelData();

                Assert.AreEqual((byte)0, bytes[3]);
                Assert.IsLessThanOrEqualTo(3, Math.Abs(bytes[7] - 128));
            }
            finally {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [TestMethod]
        public void GlobalMessageAction_CreatesVisibleExecutableLink() {
            int invocationCount = 0;
            var message = new GlobalMsgInfo(
                key: null,
                isNeedLocalizer: false,
                msgOri18nKey: "Exported",
                extraMsg: null,
                infoBarSeverity: InfoBarSeverity.Success,
                actionText: "Open in explorer",
                action: () => invocationCount++);

            Assert.AreEqual(Visibility.Visible, message.ActionVisibility);
            Assert.IsNotNull(message.ActionCommand);

            message.ActionCommand.Execute(null);

            Assert.AreEqual(1, invocationCount);
        }

        [TestMethod]
        public void GlobalMessageAction_WithoutCallbackStaysCollapsed() {
            var message = new GlobalMsgInfo(
                key: null,
                isNeedLocalizer: false,
                msgOri18nKey: "Exported",
                extraMsg: null,
                infoBarSeverity: InfoBarSeverity.Success,
                actionText: "Open in explorer");

            Assert.AreEqual(Visibility.Collapsed, message.ActionVisibility);
            Assert.IsNull(message.ActionCommand);
        }

        private static bool SupportsTransparency(ExportImageFormat format) {
            MethodInfo? method = typeof(CanvasRenderTargetExtension).GetMethod(
                "SupportsTransparency",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return (bool)method.Invoke(null, [format])!;
        }
    }
}
