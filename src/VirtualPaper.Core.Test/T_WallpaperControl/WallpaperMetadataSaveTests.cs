using System.Text.Json;
using VirtualPaper.Common;
using VirtualPaper.Models.Cores;
using static VirtualPaper.Common.Errors;

namespace VirtualPaper.Core.Test.T_WallpaperControl;

[TestClass]
public sealed class WallpaperMetadataSaveTests {
    [TestInitialize]
    public void Initialize() => _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "vp-metadata-save-" + Guid.NewGuid().ToString("N"))).FullName;

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, recursive: true);

    [TestMethod]
    public async Task SaveWebInfo_UpdatesBothFiles_AndKeepsMediaPathsAndExtensionFields() {
        var data = Create();
        WriteManifest();
        await data.SaveAsync();
        var restored = WpBasicData.Load(MetadataPath);
        AssertInfo(restored);
        using var project = JsonDocument.Parse(File.ReadAllText(ManifestPath));
        AssertManifestInfo(project.RootElement);
        Assert.AreEqual("pages/首页.html", project.RootElement.GetProperty("file").GetString());
        Assert.AreEqual("assets/preview.png", project.RootElement.GetProperty("preview").GetString());
        Assert.AreEqual(42, project.RootElement.GetProperty("custom").GetProperty("seed").GetInt32());
        CollectionAssert.AreEqual(new[] { "one", "two" }, project.RootElement.GetProperty("assets").EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.AreEqual("html bytes", File.ReadAllText(data.FilePath));
        Assert.AreEqual("preview bytes", File.ReadAllText(data.ThumbnailPath));
    }

    [TestMethod]
    [DataRow(FileType.FImage)]
    [DataRow(FileType.FGif)]
    [DataRow(FileType.FVideo)]
    public async Task NonWebInfoSave_DoesNotRewriteAnUnrelatedManifest(FileType type) {
        var data = Create(type);
        WriteManifest();
        byte[] before = File.ReadAllBytes(ManifestPath);
        await data.SaveAsync();
        AssertInfo(WpBasicData.Load(MetadataPath));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(ManifestPath));
    }

    [TestMethod]
    public async Task MissingWebManifest_DoesNotPreventLibraryMetadataSaveOrInventAManifest() {
        var data = Create();
        await data.SaveAsync();
        AssertInfo(WpBasicData.Load(MetadataPath));
        Assert.IsFalse(File.Exists(ManifestPath));
    }

    [TestMethod]
    public async Task MetadataOnlySave_DoesNotReadOrWriteTheWebManifest() {
        var data = Create();
        WriteManifest();
        byte[] before = File.ReadAllBytes(ManifestPath);
        using (var locked = File.Open(ManifestPath, FileMode.Open, FileAccess.Read, FileShare.None)) {
            await data.SaveMetadataAsync("18");
        }
        AssertInfo(WpBasicData.Load(MetadataPath));
        Assert.AreEqual("18", WpBasicData.Load(MetadataPath).AppInfo.FileVersion);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(ManifestPath));
    }

    [TestMethod]
    [DataRow("{invalid")]
    [DataRow("null")]
    public async Task InvalidWebManifest_IsPreserved_AndCanBeFixedAndRetried(string content) {
        var data = Create();
        File.WriteAllText(ManifestPath, content);
        if (content == "null") await Assert.ThrowsAsync<InvalidDataException>(() => data.SaveAsync());
        else await Assert.ThrowsAsync<FileAccessException>(() => data.SaveAsync());
        Assert.AreEqual(content, File.ReadAllText(ManifestPath));
        // The basic file commits first; a manifest failure must remain visible and retryable.
        AssertInfo(WpBasicData.Load(MetadataPath));
        WriteManifest();
        await data.SaveAsync();
        using var project = JsonDocument.Parse(File.ReadAllText(ManifestPath));
        AssertManifestInfo(project.RootElement);
    }

    [TestMethod]
    public async Task LockedWebManifest_FailsWithoutChangingIt_AndRetrySynchronizesInfo() {
        var data = Create();
        WriteManifest();
        byte[] before = File.ReadAllBytes(ManifestPath);
        using (var locked = File.Open(ManifestPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            await Assert.ThrowsAsync<FileAccessException>(() => data.SaveAsync());
        }
        CollectionAssert.AreEqual(before, File.ReadAllBytes(ManifestPath));
        AssertInfo(WpBasicData.Load(MetadataPath));
        await data.SaveAsync();
        using var project = JsonDocument.Parse(File.ReadAllText(ManifestPath));
        AssertManifestInfo(project.RootElement);
    }

    [TestMethod]
    public async Task MergeAndRead_PreserveUserInfoWithoutReplacingRebuiltIdentityOrMedia() {
        var rebuilt = Create(FileType.FImage);
        var old = new WpBasicData {
            WallpaperUid = "old identity", FolderPath = "old location", FilePath = "old media",
            Title = rebuilt.Title, Desc = rebuilt.Desc, Authors = rebuilt.Authors, Tags = rebuilt.Tags,
            PublishDate = "2025-01-02", Rating = 4.5, Partition = "old partition", IsSubscribed = true,
        };
        rebuilt.Merge(old);
        rebuilt.Merge(null!);
        await rebuilt.SaveMetadataAsync();
        var read = new WpBasicData();
        read.Read(MetadataPath);
        AssertInfo(read);
        Assert.AreEqual("new identity", read.WallpaperUid);
        Assert.AreEqual(_root, read.FolderPath);
        Assert.AreEqual(rebuilt.FilePath, read.FilePath);
        Assert.AreEqual(rebuilt.ThumbnailPath, read.ThumbnailPath);
        Assert.AreEqual("2025-01-02", read.PublishDate);
        Assert.AreEqual(4.5, read.Rating);
        Assert.AreEqual("old partition", read.Partition);
        Assert.IsTrue(read.IsSubscribed);
        Assert.AreEqual("html bytes", File.ReadAllText(read.FilePath));
    }

    private WpBasicData Create(FileType type = FileType.FWebZip) {
        Directory.CreateDirectory(Path.Combine(_root, "pages"));
        Directory.CreateDirectory(Path.Combine(_root, "assets"));
        var result = new WpBasicData {
            WallpaperUid = "new identity", FType = type, FolderPath = _root, FolderName = Path.GetFileName(_root),
            FilePath = Path.Combine(_root, "pages", "首页.html"), ThumbnailPath = Path.Combine(_root, "assets", "preview.png"),
            Title = "New title 新标题", Desc = "New description", Authors = "VirtualPaper;Guest", Tags = "Built-in;Nature",
            AppInfo = new() { AppVersion = "1.2.3" },
        };
        File.WriteAllText(result.FilePath, "html bytes");
        File.WriteAllText(result.ThumbnailPath, "preview bytes");
        return result;
    }

    private void WriteManifest() => File.WriteAllText(ManifestPath,
        """{"title":"old","description":"old","authors":"Old author","tags":"old","file":"pages/首页.html","preview":"assets/preview.png","custom":{"seed":42},"assets":["one","two"]}""");

    private static void AssertInfo(WpBasicData data) {
        Assert.AreEqual("New title 新标题", data.Title);
        Assert.AreEqual("New description", data.Desc);
        Assert.AreEqual("VirtualPaper;Guest", data.Authors);
        Assert.AreEqual("Built-in;Nature", data.Tags);
    }

    private static void AssertManifestInfo(JsonElement data) {
        Assert.AreEqual("New title 新标题", data.GetProperty("title").GetString());
        Assert.AreEqual("New description", data.GetProperty("description").GetString());
        Assert.AreEqual("VirtualPaper;Guest", data.GetProperty("authors").GetString());
        Assert.AreEqual("Built-in;Nature", data.GetProperty("tags").GetString());
    }

    private string MetadataPath => Path.Combine(_root, Constants.Field.WpBasicDataFileName);
    private string ManifestPath => Path.Combine(_root, "project.json");
    private string _root = null!;
}
