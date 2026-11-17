using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Utils;

namespace VirtualPaper.Core.Test.T_Workflows;

[TestClass]
public class WallpaperImagePropertiesTests {
    private string _root = null!;

    [TestInitialize]
    public void Initialize() => _root = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "vp-image-properties-" + Guid.NewGuid().ToString("N"))).FullName;

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, recursive: true);

    [STATestMethod]
    [DataRow(".png")]
    [DataRow(".jpg")]
    [DataRow(".bmp")]
    [DataRow(".tif")]
    public void CommonFormats_ReadDimensionsAndReleaseFile(string extension) {
        string path = Path.Combine(_root, "image" + extension);
        using (var image = new Mat(24, 40, MatType.CV_8UC3, new Scalar(40, 100, 180)))
            Assert.IsTrue(Cv2.ImWrite(path, image));

        var result = WallpaperUtil.GetWpProperty(path, FileType.FImage);

        Assert.AreEqual("40 * 24", result.Resolution);
        Assert.AreEqual(AspectRatioUtil.Format(40, 24), result.AspectRatio);
        Assert.AreEqual(extension, result.FileExtension);
        // A deferred decoder must not leave the source open after returning dimensions.
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [STATestMethod]
    public void LargeImage_ReadsDimensionsWithoutDecodingPixels() {
        string path = Path.Combine(_root, "large.jpg");
        using (var image = new Mat(24, 40, MatType.CV_8UC3, Scalar.All(0)))
            Assert.IsTrue(Cv2.ImWrite(path, image));
        byte[] bytes = File.ReadAllBytes(path);
        // Only the JPEG header describes the large image; its pixel payload is still tiny.
        int frameOffset = bytes.AsSpan().IndexOf(new byte[] { 0xFF, 0xC0 });
        Assert.IsGreaterThanOrEqualTo(0, frameOffset);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(frameOffset + 5), 10000);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(frameOffset + 7), 20000);
        File.WriteAllBytes(path, bytes);

        var result = WallpaperUtil.GetWpProperty(path, FileType.FImage);

        Assert.AreEqual("20000 * 10000", result.Resolution);
        Assert.AreEqual("2:1", result.AspectRatio);
        Assert.IsLessThan(4096L, new FileInfo(path).Length);
    }

    [STATestMethod]
    public void MalformedHeader_FallsBackWithoutReturningZeroDimensions() {
        string path = Path.Combine(_root, "broken.bmp");
        // Recognized as BMP, but too short to initialize the Windows decoder.
        File.WriteAllBytes(path, [0x42, 0x4D, .. new byte[52]]);

        Assert.ThrowsExactly<InvalidDataException>(() => WallpaperUtil.GetWpProperty(path, FileType.FImage));
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [STATestMethod]
    [DataRow("jpg", (ushort)1)]
    [DataRow("jpg", (ushort)2)]
    [DataRow("jpg", (ushort)3)]
    [DataRow("jpg", (ushort)4)]
    [DataRow("jpg", (ushort)5)]
    [DataRow("jpg", (ushort)6)]
    [DataRow("jpg", (ushort)7)]
    [DataRow("jpg", (ushort)8)]
    [DataRow("tiff", (ushort)1)]
    [DataRow("tiff", (ushort)2)]
    [DataRow("tiff", (ushort)3)]
    [DataRow("tiff", (ushort)4)]
    [DataRow("tiff", (ushort)5)]
    [DataRow("tiff", (ushort)6)]
    [DataRow("tiff", (ushort)7)]
    [DataRow("tiff", (ushort)8)]
    public void ExifOrientation_PreservesExistingDimensions(string format, ushort orientation) {
        string path = Path.Combine(_root, "oriented." + format);
        BitmapEncoder encoder = format == "jpg" ? new JpegBitmapEncoder() : new TiffBitmapEncoder();
        var metadata = new BitmapMetadata(format);
        metadata.SetQuery(format == "jpg" ? "/app1/ifd/{ushort=274}" : "/ifd/{ushort=274}", orientation);
        var pixels = new byte[40 * 24 * 3];
        var source = BitmapSource.Create(40, 24, 96, 96, PixelFormats.Bgr24, null, pixels, 40 * 3);
        encoder.Frames.Add(BitmapFrame.Create(source, null, metadata, null));
        using (var output = File.Create(path)) encoder.Save(output);

        var result = WallpaperUtil.GetWpProperty(path, FileType.FImage);

        using var previous = new Mat(path);
        Assert.IsFalse(previous.Empty());
        Assert.AreEqual($"{previous.Width} * {previous.Height}", result.Resolution);
        Assert.AreEqual(AspectRatioUtil.Format(previous.Width, previous.Height), result.AspectRatio);
    }

    [STATestMethod]
    public void UnsupportedWindowsCodec_FallsBackToOpenCv() {
        string path = Path.Combine(_root, "image.ppm");
        using (var output = File.Create(path)) {
            output.Write(Encoding.ASCII.GetBytes("P6\n40 24\n255\n"));
            output.Write(new byte[40 * 24 * 3]);
        }
        using (var input = File.OpenRead(path)) {
            Assert.ThrowsExactly<NotSupportedException>(() => BitmapDecoder.Create(input,
                BitmapCreateOptions.DelayCreation, BitmapCacheOption.None));
        }

        var result = WallpaperUtil.GetWpProperty(path, FileType.FImage);

        Assert.AreEqual("40 * 24", result.Resolution);
        Assert.AreEqual(AspectRatioUtil.Format(40, 24), result.AspectRatio);
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }
}
