using VirtualPaper.WpSettingsPanel.Utils;

namespace VirtualPaper.UI.Test.T_WpSettings;

[TestClass]
public class WallpaperInfoTokensTests {
    [TestMethod]
    public void ExistingAuthors_LoadAsSeparateItemsWithoutEmptyOrDuplicateEntries() {
        var authors = new WallpaperInfoTokens([" Alice; Bob；alice;; 张三 "], splitCommas: false);
        CollectionAssert.AreEqual(new[] { "Alice", "Bob", "张三" }, authors.Items.ToArray());
        Assert.AreEqual("Alice;Bob;张三", authors.Serialize(""));
    }

    [TestMethod]
    public void AddingTags_IgnoresWhitespaceAndCaseInsensitiveDuplicates() {
        var tags = new WallpaperInfoTokens(["Night"], splitCommas: true);
        tags.Add(" night ");
        tags.Add("  ");
        tags.Add("森林");
        CollectionAssert.AreEqual(new[] { "Night", "森林" }, tags.Items.ToArray());
    }

    [TestMethod]
    public void Authors_PreserveCommasInsideNamesWhileTagsAcceptPastedLists() {
        var authors = new WallpaperInfoTokens(["Doe, Jane;周，吴"], splitCommas: false);
        authors.Add(" Smith, John ");
        CollectionAssert.AreEqual(new[] { "Doe, Jane", "周，吴", "Smith, John" }, authors.Items.ToArray());
        Assert.AreEqual("Doe, Jane;周，吴;Smith, John", authors.Serialize(""));

        var tags = new WallpaperInfoTokens([], splitCommas: true);
        tags.Add("Night, 森林；星空\r\nnight");
        CollectionAssert.AreEqual(new[] { "Night", "森林", "星空" }, tags.Items.ToArray());
    }

    [TestMethod]
    public void Saving_IncludesPendingInputWithoutChangingItemsOrDuplicatingOnRetry() {
        var tags = new WallpaperInfoTokens(["One", "Two"], splitCommas: true);
        tags.Items.Remove("One");
        Assert.AreEqual("Two;Three", tags.Serialize(" Three, two "));
        Assert.AreEqual("Two;Three", tags.Serialize(" Three, two "));
        CollectionAssert.AreEqual(new[] { "Two" }, tags.Items.ToArray());
    }

    [TestMethod]
    public void RemovingAllItems_SavesEmptyValueAndAllowsAddingAgain() {
        var authors = new WallpaperInfoTokens(["Alice"], splitCommas: false);
        authors.Items.Remove("Alice");
        Assert.AreEqual("", authors.Serialize("  "));
        authors.Add("Alice");
        Assert.AreEqual("Alice", authors.Serialize("alice"));
    }
}
