using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Core.Test.Infrastructure;
using VirtualPaper.Models.Cores;

namespace VirtualPaper.Core.Test.T_Workflows;

[TestClass]
[DoNotParallelize]
[TestCategory("Workflow")]
public class BuiltInWallpaperWorkflowTests {
    [STATestMethod]
    public async Task PreparedCollection_IsDecodableAndScannedAfterRelocation_WithoutChangingUserData() {
        using var environment = new WallpaperWorkflowEnvironment();
        var user = await environment.ImportAsync();
        byte[] userMetadata = File.ReadAllBytes(Path.Combine(user.FolderPath, Constants.Field.WpBasicDataFileName));
        string source = Path.Combine(AppContext.BaseDirectory, "BuiltInWallpapers");
        Assert.IsTrue(Directory.Exists(source), "Build the complete Release solution before running collection tests.");
        string destination = environment.SettingsClient.Settings.BuiltInWallpapersDir;
        FileUtil.CopyDirectory(source, destination, true);
        var metadata = Directory.GetFiles(destination, Constants.Field.WpBasicDataFileName, SearchOption.AllDirectories);
        Assert.IsGreaterThan(0, metadata.Length);
        var originalHashes = Directory.GetFiles(source, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, path => SHA256.HashData(File.ReadAllBytes(path)));
        var library = await environment.OpenLibraryAsync();
        Assert.AreEqual(metadata.Length + 1, library.Index.Query(0, int.MaxValue).Count);
        foreach (string path in metadata) {
            var data = WpBasicData.Load(path);
            Assert.IsTrue(data.IsAvailable(), data.Title);
            Assert.IsTrue(data.FilePath.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(Constants.CoreField.FileVersion, data.AppInfo.FileVersion);
            using (var preview = File.OpenRead(data.ThumbnailPath)) {
                var decoder = new GifBitmapDecoder(preview, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                Assert.IsGreaterThan(0, decoder.Frames.Count);
            }
            switch (data.FType) {
                case FileType.FImage:
                    using (var image = Cv2.ImRead(data.FilePath)) Assert.IsFalse(image.Empty(), data.Title);
                    string depth = Directory.GetFiles(data.FolderPath, "depth-*.png").Single();
                    using (var image = Cv2.ImRead(depth, ImreadModes.Grayscale)) {
                        Assert.IsFalse(image.Empty());
                        Cv2.MinMaxLoc(image, out double min, out double max);
                        Assert.IsGreaterThan(32d, max - min, "Prepared depth map has no useful range.");
                    }
                    break;
                case FileType.FGif:
                    using (var input = File.OpenRead(data.FilePath)) {
                        var decoder = new GifBitmapDecoder(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        Assert.IsGreaterThan(2, decoder.Frames.Count);
                        byte[] Pixels(BitmapSource frame) {
                            var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                            byte[] pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
                            converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
                            return pixels;
                        }
                        byte[] first = Pixels(decoder.Frames[0]);
                        Assert.IsFalse(first.SequenceEqual(Pixels(decoder.Frames[decoder.Frames.Count / 2])), "Bundled GIF is static.");
                        CollectionAssert.AreEqual(first, Pixels(decoder.Frames[^1]), "Bundled GIF loop endpoints differ.");
                    }
                    break;
                case FileType.FVideo:
                    using (var video = new VideoCapture(data.FilePath))
                    using (var first = new Mat())
                    using (var later = new Mat())
                    using (var difference = new Mat()) {
                        Assert.IsTrue(video.IsOpened() && video.Read(first) && !first.Empty(), "Bundled video cannot decode.");
                        video.Set(VideoCaptureProperties.PosMsec, 1000);
                        Assert.IsTrue(video.Read(later) && !later.Empty());
                        Cv2.Absdiff(first, later, difference);
                        Assert.IsGreaterThan(.01, Cv2.Mean(difference).Val0, "Bundled video is static.");
                    }
                    break;
                case FileType.FWebZip:
                    using (var project = JsonDocument.Parse(File.ReadAllText(Path.Combine(data.FolderPath, "project.json")))) {
                        string entry = Path.GetFullPath(project.RootElement.GetProperty("file").GetString()!, data.FolderPath);
                        Assert.AreEqual(data.FilePath, entry);
                        Assert.IsTrue(File.ReadAllText(entry).Contains("playbackChanged"));
                    }
                    break;
                default: Assert.Fail("Add decode coverage for the new built-in type: " + data.FType); break;
            }
        }
        CollectionAssert.AreEqual(userMetadata, File.ReadAllBytes(Path.Combine(user.FolderPath, Constants.Field.WpBasicDataFileName)));
        CollectionAssert.AreEqual(environment.SourceHash, SHA256.HashData(File.ReadAllBytes(user.FilePath)));
        foreach (var (path, hash) in originalHashes) CollectionAssert.AreEqual(hash, SHA256.HashData(File.ReadAllBytes(path)));
    }
}
