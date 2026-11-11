using VirtualPaper.Common;
using VirtualPaper.Models.Cores;
using VirtualPaper.WpSettingsPanel.Utils;

namespace VirtualPaper.UI.Test.T_WpSettings;

[TestClass]
public class WallpaperSourceQueryTests {
    [TestMethod]
    [DataRow(" CALM ", "named")]
    [DataRow(" PHOTO.PNG ", "file")]
    [DataRow(" UID-ONLY ", "uid-only")]
    [DataRow(" ARTIST ", "authored")]
    [DataRow("hidden", "")]
    [DataRow("needle", "")]
    [DataRow("missing", "")]
    public void Search_MatchesOnlyDisplayedNameAndAuthorIgnoringCaseAndSurroundingWhitespace(string keyword, string expected) {
        WallpaperIndexEntry[] entries = [
            new() { Uid = "named", Title = "Calm lake", FileName = "hidden-file.jpg" },
            new() { Uid = "file", Title = " \t ", FileName = "Photo.png" },
            new() { Uid = "uid-only" },
            new() { Uid = "authored", Title = "Mountains", Author = "Digital Artist" },
            new() { Uid = "other", Title = "Other", Description = "needle", Tags = ["needle"] }
        ];

        var matches = new WallpaperSourceQuery(entries, _ => false).Search(keyword, false);

        CollectionAssert.AreEqual(expected.Split(',', StringSplitOptions.RemoveEmptyEntries), matches.Select(item => item.Uid).ToArray());
    }

    [TestMethod]
    public void EmptySearch_SortsByDisplayedNameAndIdentityWithoutChangingTheSourceOrder() {
        WallpaperIndexEntry[] entries = [
            new() { Uid = "b", Title = "Lake" },
            new() { Uid = "a", Title = "Lake" },
            new() { Uid = "c", FileName = "Alpha.png" }
        ];

        var matches = new WallpaperSourceQuery(entries, _ => false).Search(" \t ", false);

        CollectionAssert.AreEqual(new[] { "c", "a", "b" }, matches.Select(item => item.Uid).ToArray());
        CollectionAssert.AreEqual(new[] { "b", "a", "c" }, entries.Select(item => item.Uid).ToArray());
    }

    [TestMethod]
    public async Task Favorites_CombineWithAuthorSearchAndUsePersistedLibraryOrganization() {
        string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VirtualPaper.SourceQuery", Guid.NewGuid().ToString("N"))).FullName;
        string statePath = Path.Combine(root, "library.json");
        try {
            WpBasicData[] wallpapers = [
                Data("favorite", "Clouds", "Artist"),
                Data("ordinary", "Mountains", "Artist"),
                Data("unmatched", "Forest", "Someone else")
            ];
            var original = new WallpaperIndexService(statePath);
            foreach (var wallpaper in wallpapers) original.Update(wallpaper);
            await original.UpdateOrganizationAsync(["favorite", "unmatched"], favorite: true);
            string saved = await File.ReadAllTextAsync(statePath);

            var restarted = new WallpaperIndexService(statePath);
            foreach (var wallpaper in wallpapers) restarted.Update(wallpaper);
            var entries = restarted.Query(0, int.MaxValue);

            var query = new WallpaperSourceQuery(entries, uid => restarted.GetOrganization(uid).Favorite);
            var favorites = query.Search(" ARTIST ", true);
            var all = query.Search("artist", false);

            CollectionAssert.AreEqual(new[] { "favorite" }, favorites.Select(item => item.Uid).ToArray());
            CollectionAssert.AreEqual(new[] { "favorite", "ordinary" }, all.Select(item => item.Uid).ToArray());
            Assert.AreEqual(saved, await File.ReadAllTextAsync(statePath));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void RepeatedSearches_ReuseInventoryAndFavoritesWithoutReadingLibraryStateAgain() {
        var entries = Enumerable.Range(0, 10000).Select(index => new WallpaperIndexEntry {
            Uid = index.ToString(), Title = "Wallpaper " + index.ToString("D5"), Author = "Artist"
        }).ToArray();
        int reads = 0;
        var query = new WallpaperSourceQuery(entries, uid => { reads++; return uid == "9999"; });
        var inventory = query.Search("", false);

        for (int i = 0; i < 20; i++) {
            var matches = query.Search("artist", true);
            Assert.AreEqual(1, matches.Count);
            Assert.AreSame(entries[9999], matches[0]);
        }

        Assert.AreEqual(entries.Length, reads);
        Assert.AreSame(inventory, query.Search(" \t ", false));
    }

    [TestMethod]
    public void NewSnapshot_ObservesChangedFavoritesWithoutMutatingAnOpenPicker() {
        WallpaperIndexEntry[] entries = [new() { Uid = "wallpaper", Title = "Lake" }];
        bool favorite = false;
        var opened = new WallpaperSourceQuery(entries, _ => favorite);
        favorite = true;
        var reopened = new WallpaperSourceQuery(entries, _ => favorite);

        Assert.AreEqual(0, opened.Search("", true).Count);
        Assert.AreSame(entries[0], reopened.Search("", true).Single());
    }

    private static WpBasicData Data(string uid, string title, string author) => new() {
        WallpaperUid = uid, Title = title, Authors = author, FType = FileType.FImage,
        FolderPath = Path.Combine(Path.GetTempPath(), uid), AppInfo = new() { AppVersion = "0.6.1.0" }
    };
}
