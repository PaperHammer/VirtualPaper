using VirtualPaper.PlayerWeb.Core.Utils;

namespace WebBackdrop.Test.T_PlayerWeb;

[TestClass]
public class CoreWebView2LocalFileUtilTests {
    [TestMethod]
    public void GetStableHostName_SameCanonicalPathReturnsSameLowercaseHash() {
        var path = Path.Combine(Path.GetTempPath(), "preview", "assets");

        var first = CoreWebView2LocalFileUtil.GetStableHostName(path);
        var second = CoreWebView2LocalFileUtil.GetStableHostName(Path.GetFullPath(path));

        Assert.AreEqual(first, second);
        Assert.AreEqual(16, first.Length);
        StringAssert.Matches(first, new System.Text.RegularExpressions.Regex("^[0-9a-f]{16}$"));
    }

    [TestMethod]
    public void GetStableHostName_DifferentDirectoriesReturnDifferentHosts() {
        var root = Path.Combine(Path.GetTempPath(), "preview");

        var first = CoreWebView2LocalFileUtil.GetStableHostName(Path.Combine(root, "a"));
        var second = CoreWebView2LocalFileUtil.GetStableHostName(Path.Combine(root, "b"));

        Assert.AreNotEqual(first, second);
    }
}
