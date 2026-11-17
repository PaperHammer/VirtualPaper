using VirtualPaper.Models.Cores;
using VirtualPaper.PlayerWeb.Core.ViewModels;

namespace VirtualPaper.UI.Test.T_PlayerWeb;

[TestClass]
public class GeneralInfoViewModelTests {
    [TestMethod]
    [DataRow("FUnknown")]
    [DataRow("729:512")]
    public void Details_ShowsNearestCommonRatioWithoutChangingMetadata(string storedRatio) {
        var data = new WpBasicData { AspectRatio = storedRatio, Resolution = "729 * 512" };
        var viewModel = new GeneralInfoViewModel(data);

        Assert.AreEqual("729:512 (≈ 3:2)", viewModel.AspectRatio);
        Assert.AreEqual(storedRatio, data.AspectRatio);
    }
}
