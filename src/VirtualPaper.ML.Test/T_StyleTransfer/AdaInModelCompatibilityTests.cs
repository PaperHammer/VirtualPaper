using VirtualPaper.ML.StyleTransfer;

namespace VirtualPaper.ML.Test.T_StyleTransfer;

[TestClass]
public class AdaInModelCompatibilityTests {
    [TestMethod]
    public void CpuCompatibilityLimit_IsRestrictedToTheVerifiedGraphAndRuntime() {
        const string graph = "C3407747A5FD2C54470DDCB6B965215FD80D669F5EE6D6E1516E80DF08452F87";
        Assert.IsTrue(AdaInModelCompatibility.RequiresCpu(graph, new(1, 20, 0, 0)));
        Assert.IsTrue(AdaInModelCompatibility.RequiresCpu(graph, new(1, 23, 2)));
        Assert.IsFalse(AdaInModelCompatibility.RequiresCpu(new string('0', 64), new(1, 20, 0, 0)));
        Assert.IsFalse(AdaInModelCompatibility.RequiresCpu(graph, new(1, 21, 0, 0)));
        Assert.IsFalse(AdaInModelCompatibility.RequiresCpu(graph, new(1, 19, 0, 0)));
        Assert.IsFalse(AdaInModelCompatibility.RequiresCpu(graph, new(1, 20, 1)));
        Assert.IsFalse(AdaInModelCompatibility.RequiresCpu(graph, new(1, 23, 3)));
        Assert.IsFalse(AdaInModelCompatibility.RequiresCpu(graph, null));
    }
}
