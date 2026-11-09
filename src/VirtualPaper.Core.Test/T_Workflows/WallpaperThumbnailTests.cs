using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VirtualPaper.Common;
using VirtualPaper.Utils;

namespace VirtualPaper.Core.Test.T_Workflows;

[TestClass]
[DoNotParallelize]
public class WallpaperThumbnailTests {
    [STATestMethod]
    public void GifThumbnail_ReadsMultipleFramesBeforeDisposingBitmaps() {
        string folder = Path.Combine(Path.GetTempPath(), "VirtualPaper.GifThumbnail", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try {
            string source = Path.Combine(folder, "source.gif");
            string target = Path.Combine(folder, "thumbnail.gif");
            var encoder = new GifBitmapEncoder();
            foreach (byte value in new byte[] { 40, 180 }) {
                byte[] pixels = Enumerable.Repeat(value, 16 * 12 * 3).ToArray();
                var bitmap = BitmapSource.Create(16, 12, 96, 96, PixelFormats.Bgr24, null, pixels, 16 * 3);
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
            }
            using (var stream = File.Create(source)) encoder.Save(stream);

            WallpaperUtil.CreateGif(source, target, FileType.FGif, CancellationToken.None);

            using var output = File.OpenRead(target);
            var decoder = new GifBitmapDecoder(output, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            Assert.HasCount(2, decoder.Frames);
            Assert.AreEqual(16, decoder.Frames[0].PixelWidth);
            Assert.AreEqual(12, decoder.Frames[0].PixelHeight);
            byte[] first = new byte[16 * 12 * 4], second = new byte[first.Length];
            new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0).CopyPixels(first, 16 * 4, 0);
            new FormatConvertedBitmap(decoder.Frames[1], PixelFormats.Bgra32, null, 0).CopyPixels(second, 16 * 4, 0);
            Assert.AreNotEqual(first[0], second[0]);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [STATestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RepeatedGifThumbnails_DoNotAccumulateGdiHandles(bool failToSave) {
        string folder = Path.Combine(Path.GetTempPath(), "VirtualPaper.GifHandles", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try {
            string source = Path.Combine(folder, "source.gif");
            string target = Path.Combine(folder, "thumbnail.gif");
            var encoder = new GifBitmapEncoder();
            for (int i = 0; i < 12; i++) {
                byte[] pixels = Enumerable.Repeat((byte)(i * 20), 16 * 12 * 3).ToArray();
                encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(16, 12, 96, 96, PixelFormats.Bgr24, null, pixels, 16 * 3)));
            }
            using (var stream = File.Create(source)) encoder.Save(stream);
            // Warm up WPF/OpenCV before measuring this process's GDI object count.
            WallpaperUtil.CreateGif(source, target, FileType.FGif, CancellationToken.None);
            if (failToSave) target = Directory.CreateDirectory(Path.Combine(folder, "output-directory")).FullName;
            using var process = Process.GetCurrentProcess();
            uint before = GetGuiResources(process.Handle, 0);
            for (int i = 0; i < 10; i++) {
                if (failToSave) Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
                    WallpaperUtil.CreateGif(source, target, FileType.FGif, CancellationToken.None));
                else WallpaperUtil.CreateGif(source, target, FileType.FGif, CancellationToken.None);
            }
            uint after = GetGuiResources(process.Handle, 0);
            // A leaked HBITMAP per frame would add 120 handles in this loop.
            Assert.IsLessThanOrEqualTo(before + 4, after, $"GDI handles grew from {before} to {after}.");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(IntPtr process, uint flags);

    [STATestMethod]
    public void VideoThumbnail_KeepsDistinctFramesAfterTemporaryMatsAreReleased() {
        string folder = Path.Combine(Path.GetTempPath(), "VirtualPaper.VideoThumbnail", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try {
            string source = Path.Combine(folder, "source.avi");
            string target = Path.Combine(folder, "thumbnail.gif");
            using (var writer = new OpenCvSharp.VideoWriter(source, OpenCvSharp.FourCC.FromString("MJPG"),
                10, new OpenCvSharp.Size(32, 24))) {
                Assert.IsTrue(writer.IsOpened(), "The bundled OpenCV runtime must support the MJPEG test fixture.");
                foreach (int value in new[] { 40, 90, 150, 210 }) {
                    using var frame = new OpenCvSharp.Mat(24, 32, OpenCvSharp.MatType.CV_8UC3, new OpenCvSharp.Scalar(value, value, value));
                    writer.Write(frame);
                }
            }

            WallpaperUtil.CreateGif(source, target, FileType.FVideo, CancellationToken.None);

            using var output = File.OpenRead(target);
            var decoder = new GifBitmapDecoder(output, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            Assert.HasCount(4, decoder.Frames);
            byte[] first = new byte[32 * 24 * 4], last = new byte[first.Length];
            new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0).CopyPixels(first, 32 * 4, 0);
            new FormatConvertedBitmap(decoder.Frames[3], PixelFormats.Bgra32, null, 0).CopyPixels(last, 32 * 4, 0);
            Assert.IsTrue(last[0] - first[0] > 100, "Earlier frames must retain their own pixels after each source Mat is disposed.");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }
}
