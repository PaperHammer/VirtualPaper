using VirtualPaper.Models.Cores;
using VirtualPaper.WpSettingsPanel.Utils;

namespace VirtualPaper.UI.Test.T_WpSettings;

[TestClass]
public class WallpaperIndexEntryTests {
    [TestMethod]
    [DataRow("My wallpaper", "My wallpaper")]
    [DataRow("  My wallpaper  ", "  My wallpaper  ")]
    [DataRow("", "mountains.jpg")]
    [DataRow(" \t ", "mountains.jpg")]
    [DataRow(null, "mountains.jpg")]
    public void DisplayName_UsesTitleOrFileNameWithoutChangingMetadata(string? title, string expected) {
        var data = new WpBasicData {
            WallpaperUid = "wallpaper-id", Title = title!,
            FilePath = Path.Combine(Path.GetTempPath(), "wallpaper-id", "mountains.jpg"),
            ThumbnailPath = Path.Combine(Path.GetTempPath(), "wallpaper-id", "preview.jpg")
        };

        var entry = WallpaperIndexEntry.FromData(data);

        Assert.AreEqual(expected, entry.DisplayName);
        Assert.AreEqual(title ?? "", entry.Title);
        Assert.AreEqual(title, data.Title);
        Assert.AreEqual(data.ThumbnailPath, entry.ThumbnailPath);
    }

    [TestMethod]
    public void DisplayName_WithoutTitleOrFileName_UsesIdentityEvenWhenFolderHasAName() {
        var entry = WallpaperIndexEntry.FromData(new WpBasicData {
            WallpaperUid = "wallpaper-id", Title = " ",
            FolderPath = Path.Combine(Path.GetTempPath(), "forest-wallpaper") + Path.DirectorySeparatorChar
        });

        Assert.AreEqual("wallpaper-id", entry.DisplayName);
        Assert.AreEqual(" ", entry.Title);
    }

    [TestMethod]
    public void DisplayName_WithoutAnyName_UsesWallpaperIdentity() {
        var entry = WallpaperIndexEntry.FromData(new WpBasicData { WallpaperUid = "wallpaper-id" });

        Assert.AreEqual("wallpaper-id", entry.DisplayName);
        Assert.AreEqual("", entry.Title);
    }
}
