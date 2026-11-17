using System.Security.Cryptography;

namespace VirtualPaper.ReleaseBuildData.Test;

[TestClass]
public class LegalDocumentsTests {
    [TestMethod]
    [TestCategory("ReleaseBuild")]
    public void LegalPayload_ContainsExactAuthoredTranslationsAndOriginalNotices() {
        const string prefix = "VirtualPaper.Legal.";
        var assembly = typeof(LegalDocumentsTests).Assembly;
        string root = Path.Combine(ReleaseBuildSanityTests.ReleaseDir, "Plugins", "UI", "Legal");
        string[] resources = assembly.GetManifestResourceNames().Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        Assert.IsGreaterThanOrEqualTo(10, resources.Length, "No authored legal document inventory was embedded.");
        foreach (string resource in resources) {
            string relative = resource[prefix.Length..].Replace('\\', Path.DirectorySeparatorChar);
            string path = Path.Combine(root, relative);
            Assert.IsTrue(File.Exists(path), "Missing bundled legal document: " + relative);
            using var expected = assembly.GetManifestResourceStream(resource)!;
            using var actual = File.OpenRead(path);
            Assert.AreEqual(Convert.ToHexString(SHA256.HashData(expected)), Convert.ToHexString(SHA256.HashData(actual)), relative);
        }
        Assert.IsFalse(File.Exists(Path.Combine(root, "README.md")), "The repository maintenance index is not an application notice.");
    }
}
