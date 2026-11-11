using VirtualPaper.Common;

namespace VirtualPaper.Core.Test.T_Workflows;

[TestClass]
public class PackageTestActivationTests {
    [TestMethod]
    public void OrdinaryLaunchDoesNotEnableProcessIsolation() {
        Assert.IsNull(TestProcessEnvironment.ReadActivationArguments([]));
        Assert.IsNull(TestProcessEnvironment.ReadActivationArguments(["--report", "report.json"]));
    }

    [TestMethod]
    public void PackageActivationRetainsTheSameValidatedIsolationContract() {
        string session = Guid.NewGuid().ToString("N");
        string root = Path.Combine(Path.GetTempPath(), "package test", "data");
        var result = TestProcessEnvironment.ReadActivationArguments(
            ["--virtualpaper-test-session", session, "--virtualpaper-test-data-root", root]);
        Assert.AreEqual(session, result!.Value.Session);
        Assert.AreEqual(Path.GetFullPath(root), result.Value.Root);
    }

    [TestMethod]
    public void IncompleteOrUnsafeActivationCannotFallBackToDailyProfile() {
        string session = Guid.NewGuid().ToString("N");
        foreach (string[] arguments in new[] {
            new[] { "--virtualpaper-test-session", session },
            new[] { "--virtualpaper-test-data-root", Path.GetTempPath() },
            new[] { "--virtualpaper-test-session", "invalid", "--virtualpaper-test-data-root", Path.GetTempPath() },
            new[] { "--virtualpaper-test-session", session, "--virtualpaper-test-data-root", "relative" },
            new[] { "--virtualpaper-test-session", session, "--virtualpaper-test-data-root", Path.GetPathRoot(Path.GetTempPath())! }
        }) Assert.ThrowsExactly<InvalidOperationException>(() => TestProcessEnvironment.ReadActivationArguments(arguments));
    }
}
