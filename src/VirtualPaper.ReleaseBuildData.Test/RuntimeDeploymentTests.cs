using System.Text.Json;

namespace VirtualPaper.ReleaseBuildData.Test;

[TestClass]
public class RuntimeDeploymentTests {
    [TestMethod]
    [TestCategory("ReleaseBuild")]
    [DataRow("", "VirtualPaper", true)]
    [DataRow("Plugins/UI", "VirtualPaper.UI", false)]
    [DataRow("Plugins/PlayerWeb", "VirtualPaper.PlayerWeb", true)]
    [DataRow("Plugins/ScrSaver", "VirtualPaper.ScreenSaver", true)]
    public void ExeProcesses_RequireNet10WithoutBundlingRuntime(string folder, string name, bool desktop) {
        string directory = Path.Combine(ReleaseBuildSanityTests.ReleaseDir, folder);
        using var configuration = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, name + ".runtimeconfig.json")));
        var options = configuration.RootElement.GetProperty("runtimeOptions");
        Assert.AreEqual("net10.0", options.GetProperty("tfm").GetString(), name);
        Assert.IsFalse(options.TryGetProperty("includedFrameworks", out _), $"{name} unexpectedly bundles .NET.");
        var frameworks = options.TryGetProperty("frameworks", out var multiple)
            ? multiple.EnumerateArray().ToArray() : [options.GetProperty("framework")];
        string[] provided = ["Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App", "Microsoft.AspNetCore.App"];
        foreach (var framework in frameworks) {
            Assert.Contains(framework.GetProperty("name").GetString()!, provided, $"The installer does not provision this framework for {name}.");
        }
        Assert.IsTrue(frameworks.Any(item => item.GetProperty("name").GetString() == "Microsoft.NETCore.App"), name);
        if (desktop) Assert.IsTrue(frameworks.Any(item => item.GetProperty("name").GetString() == "Microsoft.WindowsDesktop.App"), name);
        foreach (var framework in frameworks)
            Assert.AreEqual(10, Version.Parse(framework.GetProperty("version").GetString()!).Major, name);
        Assert.IsFalse(File.Exists(Path.Combine(directory, "coreclr.dll")), $"{name} contains a stale MSIX runtime.");
    }
}
