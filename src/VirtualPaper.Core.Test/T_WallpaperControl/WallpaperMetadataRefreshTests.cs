using System.IO;
using Moq;
using OpenCvSharp;
using VirtualPaper.Common;
using VirtualPaper.Core.Test.Infrastructure;
using VirtualPaper.Cores.WpControl;
using VirtualPaper.Factories.Interfaces;
using VirtualPaper.Models.Cores;
using VirtualPaper.Services.Interfaces;
using MockFactory = VirtualPaper.Core.Test.Infrastructure.MockFactory;

namespace VirtualPaper.Core.Test.T_WallpaperControl;

[TestClass]
[DoNotParallelize]
public class WallpaperMetadataRefreshTests {
    private string _root = null!;
    private WallpaperControl _control = null!;
    private Mock<IUserSettingsService> _settings = null!;

    [TestInitialize]
    public void Initialize() {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "vp-refresh-" + Guid.NewGuid().ToString("N"))).FullName;
        _settings = MockFactory.CreateUserSettings();
        Mock.Get(_settings.Object.Settings).Setup(settings => settings.AppVersion).Returns("0.6.0.0");
        _control = new WallpaperControl(_settings.Object, MockFactory.CreateMonitorManager(1).Object,
            new Mock<IWallpaperFactory>().Object, MockFactory.CreateDesktopService().Object, new Mock<IJobService>().Object);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, true);

    [TestMethod]
    [DataRow("18")]
    [DataRow("19")]
    public async Task Refresh_RepairsDerivedPropertiesAtOldAndCurrentVersions(string version) {
        var data = Basic(FileType.FImage);
        WriteImage(data.FilePath, 729, 512);
        File.WriteAllText(data.ThumbnailPath, "existing thumbnail");
        string effect = Path.Combine(_root, "effect.json");
        File.WriteAllText(effect, "existing effect values");
        await data.SaveMetadataAsync(version);
        byte[] media = File.ReadAllBytes(data.FilePath);

        var result = await _control.UpdateBasicDataAsync(_root, data.FolderName, data.FilePath, data.FType);
        var saved = WpBasicData.Load(Path.Combine(_root, Constants.Field.WpBasicDataFileName));

        Assert.AreEqual("729 * 512", saved.Resolution);
        Assert.AreEqual("729:512", saved.AspectRatio);
        Assert.AreEqual(".png", saved.FileExtension);
        Assert.IsFalse(string.IsNullOrWhiteSpace(saved.FileSize));
        Assert.AreNotEqual("wrong size", saved.FileSize);
        Assert.AreEqual(data.WallpaperUid, result.WallpaperUid);
        Assert.AreEqual(data.CreatedTime, result.CreatedTime);
        Assert.AreEqual(data.CreatedTime, saved.CreatedTime);
        Assert.AreEqual(data.Title, saved.Title);
        Assert.AreEqual(data.Desc, saved.Desc);
        Assert.AreEqual(data.Authors, saved.Authors);
        Assert.AreEqual(data.Tags, saved.Tags);
        Assert.AreEqual(_settings.Object.Settings.AppVersion, saved.AppInfo.AppVersion);
        Assert.AreEqual(Constants.CoreField.FileVersion, saved.AppInfo.FileVersion);
        CollectionAssert.AreEqual(media, File.ReadAllBytes(data.FilePath));
        Assert.AreEqual("existing thumbnail", File.ReadAllText(data.ThumbnailPath));
        Assert.AreEqual("existing effect values", File.ReadAllText(effect));
    }

    [TestMethod]
    public async Task Refresh_ReadsMediaAgainOnEachRequestWithoutChangingCreationTime() {
        var data = Basic(FileType.FImage);
        WriteImage(data.FilePath, 729, 512);
        await data.SaveMetadataAsync();
        await _control.UpdateBasicDataAsync(_root, data.FolderName, data.FilePath, data.FType);

        WriteImage(data.FilePath, 1920, 1080);
        var result = await _control.UpdateBasicDataAsync(_root, data.FolderName, data.FilePath, data.FType);

        Assert.AreEqual("1920 * 1080", result.Resolution);
        Assert.AreEqual("16:9", result.AspectRatio);
        Assert.AreEqual(data.CreatedTime, result.CreatedTime);
        Assert.AreEqual(data.WallpaperUid, result.WallpaperUid);
    }

    [TestMethod]
    public async Task Refresh_InstalledWebProjectDoesNotTreatItsEntryAsAnImportArchive() {
        var data = Basic(FileType.FWebZip);
        data.FilePath = Path.Combine(_root, "index.html");
        data.FileSize = "2.50 MB";
        File.WriteAllText(data.FilePath, "<html>existing project</html>");
        string manifest = Path.Combine(_root, "project.json");
        File.WriteAllText(manifest, "{\"title\":\"Original manifest\",\"file\":\"index.html\"}");
        byte[] original = File.ReadAllBytes(manifest);
        await data.SaveMetadataAsync();

        var result = await _control.UpdateBasicDataAsync(_root, data.FolderName, data.FilePath, data.FType);

        Assert.AreEqual("-", result.Resolution);
        Assert.AreEqual("-", result.AspectRatio);
        Assert.AreEqual(".zip", result.FileExtension);
        Assert.AreEqual("2.50 MB", result.FileSize);
        Assert.AreEqual(data.Title, result.Title);
        Assert.AreEqual(data.CreatedTime, result.CreatedTime);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(manifest));
        Assert.AreEqual("<html>existing project</html>", File.ReadAllText(data.FilePath));
    }

    [TestMethod]
    public async Task Refresh_UnreadableImageDoesNotReplaceMetadataWithZeroDimensions() {
        var data = Basic(FileType.FImage);
        File.WriteAllText(data.FilePath, "not an image");
        await data.SaveMetadataAsync();
        string path = Path.Combine(_root, Constants.Field.WpBasicDataFileName);
        byte[] before = File.ReadAllBytes(path);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            _control.UpdateBasicDataAsync(_root, data.FolderName, data.FilePath, data.FType));

        CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
    }

    private WpBasicData Basic(FileType type) => new() {
        WallpaperUid = "original-id", FolderName = "original-folder", FolderPath = _root,
        FilePath = Path.Combine(_root, "image.png"), ThumbnailPath = Path.Combine(_root, "thumbnail.gif"),
        FType = type, CreatedTime = new DateTime(2026, 5, 18, 2, 32, 17, DateTimeKind.Utc),
        Title = "User title", Desc = "User description", Authors = "User author", Tags = "User tag",
        Resolution = "", AspectRatio = "FUnknown", FileExtension = "", FileSize = "wrong size",
        AppInfo = new() { AppVersion = "0.4.0.1" }
    };

    private static void WriteImage(string path, int width, int height) {
        using var image = new Mat(height, width, MatType.CV_8UC3, new Scalar(40, 100, 180));
        Cv2.ImWrite(path, image);
    }
}
