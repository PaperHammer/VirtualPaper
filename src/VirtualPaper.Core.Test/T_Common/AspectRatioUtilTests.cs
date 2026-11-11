using VirtualPaper.Common.Utils.Files;

namespace VirtualPaper.Core.Test.T_Common;

[TestClass]
public class AspectRatioUtilTests {
    [TestMethod]
    [DataRow(1920, 1080, "16:9")]
    [DataRow(1366, 768, "16:9")]
    [DataRow(1920, 1200, "16:10")]
    [DataRow(1024, 768, "4:3")]
    [DataRow(729, 512, "729:512")]
    [DataRow(1080, 1920, "9:16")]
    [DataRow(0, 512, "-")]
    [DataRow(729, 0, "-")]
    [DataRow(-1, 512, "-")]
    public void Dimensions_KeepCommonRatiosAndSupportOtherSizes(int width, int height, string expected) {
        Assert.AreEqual(expected, AspectRatioUtil.Format(width, height));
    }

    [TestMethod]
    [DataRow("FUnknown", "729 * 512", "729:512 (≈ 3:2)")]
    [DataRow("729:512", "", "729:512 (≈ 3:2)")]
    [DataRow("512:729", "", "512:729 (≈ 2:3)")]
    [DataRow("3:2", "1500 * 1000", "3:2")]
    [DataRow("9:16", "1080 * 1920", "9:16")]
    [DataRow("1:1", "1000 * 1000", "1:1")]
    [DataRow("FUnknown", "3440 * 1440", "43:18 (≈ 21:9)")]
    [DataRow("FUnknown", "1920 * 1080 (60 fps)", "16:9")]
    [DataRow("", "1080 × 1920", "9:16")]
    [DataRow("-", "1920x1080", "16:9")]
    [DataRow("16:9", "1366 * 768", "16:9")]
    [DataRow("0:0", "1920 * 1080", "16:9")]
    [DataRow("FUnknown", "", "-")]
    [DataRow("FUnknown", "0 * 512", "-")]
    [DataRow("FUnknown", "729 * 0", "-")]
    [DataRow("FUnknown", "2147483648 * 512", "-")]
    [DataRow("FUnknown", "729 * missing", "-")]
    public void StoredProperties_UseReadableRatioOrStoredDimensions(string stored, string resolution, string expected) {
        Assert.AreEqual(expected, AspectRatioUtil.Format(stored, resolution));
    }
}
