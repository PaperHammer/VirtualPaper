using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VirtualPaper.Common;
using VirtualPaper.Utils;

namespace VirtualPaper.Core.Test.T_Workflows;

[TestClass]
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
}
