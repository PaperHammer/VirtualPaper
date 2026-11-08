using VirtualPaper.Common;
using VirtualPaper.Models.Cores;
using VirtualPaper.WpSettingsPanel.Utils;

namespace VirtualPaper.UI.Test.T_WpSettings;

[TestClass]
public class WallpaperOrganizationTests {
    private string _root = null!;
    private string _statePath = null!;
    private WallpaperIndexService _index = null!;

    [TestInitialize]
    public void Initialize() {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VirtualPaper.Tests", "Organization-" + Guid.NewGuid().ToString("N"))).FullName;
        _statePath = Path.Combine(_root, "library.json");
        _index = new(_statePath);
    }

    [TestCleanup]
    public void Cleanup() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [TestMethod]
    [DataRow("title")]
    [DataRow("description")]
    [DataRow("author")]
    [DataRow("tag")]
    public void Query_SearchesAllMetadataBeforePaging(string field) {
        for (int i = 0; i < 40; i++) _index.Update(Data("item-" + i, "Other"));
        var match = Data("match", field == "title" ? "needle" : "Other");
        match.Desc = field == "description" ? "NEEDLE in the description" : "";
        match.Authors = field == "author" ? "Needle" : "";
        match.Tags = field == "tag" ? "nature;needle" : "";
        _index.Update(match);

        var result = _index.Query(0, 1, new FilterContext { TitleKeyword = " needle " });

        Assert.HasCount(1, result);
        Assert.AreEqual("match", result[0].Uid);
    }

    [TestMethod]
    public async Task Query_CombinesPersonalTagsFavoritesAndTypeBeforePagination() {
        var first = Data("a", "Alpha");
        var second = Data("b", "Beta");
        var video = Data("c", "Video"); video.FType = FileType.FVideo;
        foreach (var item in new[] { first, second, video }) _index.Update(item);
        await _index.UpdateOrganizationAsync(["a", "b", "c"], favorite: true, tags: [" Mood ; mood, Calm；夜景 "]);
        await _index.UpdateOrganizationAsync(["b"], favorite: false);

        var result = _index.Query(0, 30, new FilterContext {
            TitleKeyword = "calm", Tag = "MOOD", FavoritesOnly = true, ActiveTypes = new HashSet<FileType> { FileType.FImage }
        });

        CollectionAssert.AreEqual(new[] { "a" }, result.Select(item => item.Uid).ToArray());
        Assert.HasCount(3, _index.GetOrganization("a").Tags);
        Assert.IsEmpty(first.Tags); // Personal tags never modify wallpaper metadata.
        CollectionAssert.AreEquivalent(new[] { "Calm", "Mood", "夜景" }, _index.GetTags().ToArray());
    }

    [TestMethod]
    [DataRow(" 夜 ", "source,personal")]
    [DataRow(" nAt ", "source")]
    [DataRow("no match", "")]
    [DataRow("  ", "other,personal,source")]
    public async Task Query_TagInputMatchesSourceAndPersonalSubstringsBeforePaging(string keyword, string expected) {
        var source = Data("source", "Source"); source.Tags = "森林夜景;Nature";
        var personal = Data("personal", "Personal");
        // A title match must not count as a tag match.
        var other = Data("other", "森林夜景 Nature");
        foreach (var item in new[] { other, personal, source }) _index.Update(item);
        await _index.UpdateOrganizationAsync(["personal"], tags: ["城市夜景"]);
        var filter = new FilterContext { Tag = keyword, SortOrder = WallpaperSortOrder.Title };

        var matches = _index.Query(0, 30, filter);
        CollectionAssert.AreEquivalent(expected.Split(',', StringSplitOptions.RemoveEmptyEntries), matches.Select(item => item.Uid).ToArray());
        var pages = Enumerable.Range(0, matches.Count).SelectMany(offset => _index.Query(offset, 1, filter)).ToArray();
        CollectionAssert.AreEqual(matches.Select(item => item.Uid).ToArray(), pages.Select(item => item.Uid).ToArray());
    }

    [TestMethod]
    public void Query_SortsAndPaginatesDeterministically() {
        var a = Data("a", "Zebra"); a.CreatedTime = new DateTime(2026, 1, 1); a.Authors = "Alpha";
        var b = Data("b", "Alpha"); b.CreatedTime = a.CreatedTime; b.Authors = "Zebra";
        var c = Data("c", "Beta"); c.CreatedTime = a.CreatedTime.AddDays(1); c.Authors = "Beta";
        foreach (var item in new[] { c, b, a }) _index.Update(item);
        AssertOrder(WallpaperSortOrder.Newest, "c", "a", "b");
        AssertOrder(WallpaperSortOrder.Oldest, "a", "b", "c");
        AssertOrder(WallpaperSortOrder.Title, "b", "c", "a");
        AssertOrder(WallpaperSortOrder.Author, "a", "c", "b");
        Assert.AreEqual("c", _index.Query(1, 1, new FilterContext { SortOrder = WallpaperSortOrder.Title })[0].Uid);
        Assert.HasCount(3, _index.Query(0, 10, new FilterContext { ActiveTypes = new HashSet<FileType>() }));
    }

    [TestMethod]
    public async Task Preferences_SurviveRestartBuiltinRefreshAndDirectoryMoveWithoutChangingMetadata() {
        string library = Directory.CreateDirectory(Path.Combine(_root, "library")).FullName;
        var data = Data("builtin", "Original");
        data.FolderPath = Directory.CreateDirectory(Path.Combine(library, "builtin")).FullName;
        data.FilePath = Path.Combine(data.FolderPath, "image.png");
        data.Tags = "original";
        await data.SaveMetadataAsync();
        string metadata = Path.Combine(data.FolderPath, Constants.Field.WpBasicDataFileName);
        byte[] before = File.ReadAllBytes(metadata);
        await _index.RebuildAsync([library]);
        await _index.UpdateOrganizationAsync([data.WallpaperUid], favorite: true, tags: ["personal"]);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(metadata));

        data.Title = "Updated builtin"; data.Tags = "new source tag";
        await data.SaveMetadataAsync();
        string moved = Path.Combine(_root, "moved-library");
        Directory.Move(library, moved);
        var restarted = new WallpaperIndexService(_statePath);
        await restarted.RebuildAsync([moved]);

        var result = restarted.Query(0, 30, new FilterContext { FavoritesOnly = true, Tag = "personal" });
        Assert.HasCount(1, result);
        Assert.AreEqual("Updated builtin", result[0].Title);
        Assert.StartsWith(moved, result[0].FolderPath);
        Assert.IsTrue(restarted.GetOrganization("BUILTIN").Favorite);
        CollectionAssert.AreEquivalent(new[] { "new source tag", "personal" }, restarted.GetTags().ToArray());
    }

    [TestMethod]
    public async Task BatchTags_AddRemoveReplaceAndClear_PreserveFavorites() {
        await _index.UpdateOrganizationAsync(["a", "A", "b"], favorite: true, tags: ["one;two"]);
        await _index.UpdateOrganizationAsync(["a", "b"], tags: ["TWO;three"], tagUpdate: WallpaperTagUpdate.Add);
        CollectionAssert.AreEquivalent(new[] { "one", "two", "three" }, _index.GetOrganization("a").Tags);
        await _index.UpdateOrganizationAsync(["a", "b"], tags: ["TWO"], tagUpdate: WallpaperTagUpdate.Remove);
        CollectionAssert.AreEquivalent(new[] { "one", "three" }, _index.GetOrganization("b").Tags);
        await _index.UpdateOrganizationAsync(["a"], tags: ["replacement"]);
        await _index.UpdateOrganizationAsync(["b"], tags: []);
        var restarted = new WallpaperIndexService(_statePath);
        CollectionAssert.AreEqual(new[] { "replacement" }, restarted.GetOrganization("a").Tags);
        Assert.IsEmpty(restarted.GetOrganization("b").Tags);
        Assert.IsTrue(restarted.GetOrganization("b").Favorite);
        var snapshot = restarted.GetOrganization("a"); snapshot.Tags[0] = "tampered";
        Assert.AreEqual("replacement", restarted.GetOrganization("a").Tags[0]);
    }

    [TestMethod]
    public async Task ConcurrentUpdates_DoNotLoseOtherWallpapersPreferences() {
        await Task.WhenAll(Enumerable.Range(0, 8).Select(i => _index.UpdateOrganizationAsync(["uid-" + i], favorite: true)));
        var restarted = new WallpaperIndexService(_statePath);
        foreach (int i in Enumerable.Range(0, 8)) Assert.IsTrue(restarted.GetOrganization("uid-" + i).Favorite);
    }

    [TestMethod]
    public async Task FailedSave_DoesNotPublishUnsavedChanges() {
        Directory.CreateDirectory(_statePath);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _index.UpdateOrganizationAsync(["a"], favorite: true));
        Assert.IsFalse(_index.GetOrganization("a").Favorite);
        Assert.IsEmpty(Directory.GetFiles(_root, ".*.tmp"));
    }

    [TestMethod]
    public async Task CorruptPreferences_AreNotSilentlyOverwritten() {
        File.WriteAllText(_statePath, "{ broken");
        var restarted = new WallpaperIndexService(_statePath);
        await Assert.ThrowsAsync<IOException>(() => restarted.UpdateOrganizationAsync(["a"], favorite: true));
        Assert.AreEqual("{ broken", File.ReadAllText(_statePath));
    }

    private WpBasicData Data(string uid, string title) => new() {
        WallpaperUid = uid, Title = title, FolderPath = Path.Combine(_root, uid), FType = FileType.FImage,
        AppInfo = new() { AppVersion = "0.6.1.0" }
    };

    private void AssertOrder(WallpaperSortOrder order, params string[] expected) =>
        CollectionAssert.AreEqual(expected, _index.Query(0, 30, new FilterContext { SortOrder = order }).Select(item => item.Uid).ToArray());
}
