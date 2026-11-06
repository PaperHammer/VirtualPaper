using System.Text.Json;

namespace VirtualPaper.ReleaseBuildData.Test;

[TestClass]
public class BuiltInWallpaperTests {
    [TestMethod]
    [TestCategory("ReleaseBuild")]
    public void BuiltIns_IncludeEveryAuthoredItemWithPortableMetadataAndAssets() {
        string root = Path.Combine(ReleaseBuildSanityTests.ReleaseDir, "BuiltInWallpapers");
        Assert.IsTrue(Directory.Exists(root), "The built-in wallpaper payload is missing.");
        var assembly = typeof(BuiltInWallpaperTests).Assembly;
        var resources = assembly.GetManifestResourceNames().Where(name => name.StartsWith("VirtualPaper.BuiltIn.")).ToArray();
        Assert.IsGreaterThan(0, resources.Length, "The authored collection must not silently disappear.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var types = new HashSet<string>();
        foreach (string resource in resources) {
            using var expected = JsonDocument.Parse(assembly.GetManifestResourceStream(resource)!);
            string name = expected.RootElement.GetProperty("FolderName").GetString()!;
            Assert.AreEqual(name, Path.GetFileName(name), "A built-in folder name must be a single path segment.");
            Assert.IsTrue(folders.Add(name), "Duplicate built-in folder: " + name);
            string folder = Path.Combine(root, name);
            using var actual = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "wp_metadata_basic.json")));
            var data = actual.RootElement;
            foreach (string field in new[] { "WallpaperUid", "FolderName", "Title", "Authors", "FType", "FilePath", "ThumbnailPath" })
                Assert.AreEqual(expected.RootElement.GetProperty(field).GetString(), data.GetProperty(field).GetString(), name + ": " + field);
            Assert.IsTrue(ids.Add(data.GetProperty("WallpaperUid").GetString()!), "Duplicate built-in UID.");
            types.Add(data.GetProperty("FType").GetString()!);
            foreach (string field in new[] { "Title", "Authors", "Desc", "PublishDate" })
                Assert.IsFalse(string.IsNullOrWhiteSpace(data.GetProperty(field).GetString()), name + ": " + field);
            Assert.AreEqual(".", data.GetProperty("FolderPath").GetString());
            var tags = data.GetProperty("Tags").GetString()!.Split(';');
            Assert.Contains("Built-in", tags);
            Assert.Contains("内置", tags);
            foreach (string field in new[] { "FilePath", "ThumbnailPath" }) {
                string path = data.GetProperty(field).GetString()!;
                Assert.IsFalse(Path.IsPathRooted(path), name + ": " + field);
                string resolved = Path.GetFullPath(path, folder);
                Assert.IsTrue(resolved.StartsWith(Path.GetFullPath(folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
                ReleaseBuildSanityTests.AssertFileExists(resolved);
            }
            ReleaseBuildSanityTests.AssertFileExists(Path.Combine(folder, "NOTICE.txt"));
            if (tags.Contains("CC0")) {
                ReleaseBuildSanityTests.AssertFileExists(Path.Combine(folder, "CC0-1.0.txt"));
                Assert.DoesNotContain("AI-generated", tags);
            }
        }
        CollectionAssert.IsSubsetOf(new[] { "FImage", "FGif", "FVideo", "FWebZip" }, types.ToArray());
        CollectionAssert.AreEquivalent(folders.ToArray(), Directory.GetDirectories(root).Select(Path.GetFileName).ToArray(),
            "Stale or missing folders in the build payload.");
    }
}
