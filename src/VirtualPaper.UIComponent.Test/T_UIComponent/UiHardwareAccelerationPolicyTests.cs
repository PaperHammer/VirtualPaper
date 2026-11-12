using VirtualPaper.Common;
using VirtualPaper.UIComponent.Utils;

namespace VirtualPaper.UIComponent.Test.T_UIComponent;

[TestClass]
public class UiHardwareAccelerationPolicyTests {
    [TestMethod]
    public void Profiles_KeepUiModesAndMarkdownSeparateFromDesktopAndScreenSaver() {
        string player = Constants.CommonPaths.TempWebView2Dir;
        string screenSaver = Constants.CommonPaths.TempScrWebView2Dir;
        var gpu = new UiHardwareAccelerationPolicy(true);
        var software = new UiHardwareAccelerationPolicy(false);

        Assert.AreEqual(Path.Combine(player, "UI", "GPU"), gpu.GetWebView2UserDataFolder());
        Assert.AreEqual(Path.Combine(player, "UI", "Software"), software.GetWebView2UserDataFolder());
        string markdown = gpu.GetWebView2UserDataFolder("MarkdownPreview");
        Assert.AreEqual(Path.Combine(gpu.WebView2UserDataFolder, "MarkdownPreview"), markdown);
        Assert.AreNotEqual(markdown, software.GetWebView2UserDataFolder("MarkdownPreview"));
        CollectionAssert.AllItemsAreUnique(new[] { player, screenSaver, gpu.WebView2UserDataFolder, software.WebView2UserDataFolder, markdown });
        Assert.AreEqual(player, Constants.CommonPaths.TempWebView2Dir);
        Assert.AreEqual(screenSaver, Constants.CommonPaths.TempScrWebView2Dir);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Arguments_DisableGpuOnlyForSoftwareMode_AndRetainPreviewBehavior(bool enabled) {
        var policy = new UiHardwareAccelerationPolicy(enabled);
        string[] arguments = policy.WebView2BrowserArguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual(!enabled, arguments.Contains("--disable-gpu"));
        Assert.Contains("--disk-cache-size=1", arguments);
        Assert.Contains("--autoplay-policy=no-user-gesture-required", arguments);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow(".")]
    [DataRow("..")]
    [DataRow("../player")]
    [DataRow("nested/profile")]
    [DataRow(@"C:\player")]
    public void ProfilePartition_RejectsPathsOutsideItsSingleDirectoryName(string partition) {
        Assert.Throws<ArgumentException>(() => new UiHardwareAccelerationPolicy(true).GetWebView2UserDataFolder(partition));
    }
}
