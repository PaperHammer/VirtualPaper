using System.Text.Json;
using System.Text.Json.Nodes;
using VirtualPaper.Models.Cores;
using Workloads.Creation.WebBackdrop.Models.SerializableData;

namespace WebBackdrop.Test.T_WebBackdrop;

[TestClass]
public class WebManifestPersistenceTests {
    [TestInitialize]
    public void Initialize() {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VirtualPaper.ManifestPersistence-" + Guid.NewGuid().ToString("N"))).FullName;
        File.WriteAllText(Path.Combine(_root, "index.html"), "<html>original</html>");
        _design = WebDesignFileUtil.Create(Path.Combine(_root, "Project.vpw"));
        _design.EnsureProjectStructure();
        _assets = Directory.CreateDirectory(Path.Combine(_root, "assets")).FullName;
        string nested = Directory.CreateDirectory(Path.Combine(_assets, "nested")).FullName;
        File.WriteAllText(Path.Combine(nested, "keep.js"), "original source bytes");
        _design.AddManifestPathRecursive(_assets);
        var manifest = JsonNode.Parse(File.ReadAllText(_design.ProjectFilePath))!.AsObject();
        manifest["custom"] = new JsonObject { ["preserve"] = true };
        File.WriteAllText(_design.ProjectFilePath, manifest.ToJsonString());
        _design.ReloadManifest();
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, recursive: true);

    [TestMethod]
    [DataRow("add")]
    [DataRow("remove")]
    [DataRow("rename")]
    public void LockedManifestWrite_KeepsCachedInventoryAndDiskUnchanged_AndCanRetry(string operation) {
        var original = _design.GetManifestItems().ToArray();
        byte[] originalBytes = File.ReadAllBytes(_design.ProjectFilePath);
        string added = Path.Combine(_root, "added.js");
        File.WriteAllText(added, "new source bytes");
        Action update = operation switch {
            "add" => () => _design.AddManifestPath(added),
            "remove" => () => _design.RemoveManifestPath(_assets),
            _ => () => _design.RenameManifestPath(_assets, Path.Combine(_root, "renamed-assets"))
        };

        using (var locked = new FileStream(_design.ProjectFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            AssertWriteBlocked(update);
            CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(_design.ProjectFilePath));
            CollectionAssert.AreEqual(original, _design.GetManifestItems().ToArray());
            AssertNoTemporaryFiles();
        }

        update();

        string[] paths = _design.GetManifestItems().Select(item => item.Path).ToArray();
        if (operation == "add") Assert.Contains("added.js", paths);
        else if (operation == "remove") Assert.IsFalse(paths.Any(path => path == "assets" || path.StartsWith("assets/", StringComparison.Ordinal)));
        else {
            Assert.Contains("renamed-assets/nested/keep.js", paths);
            Assert.IsFalse(paths.Any(path => path == "assets" || path.StartsWith("assets/", StringComparison.Ordinal)));
        }
        AssertPersistedInventory();
        Assert.AreEqual("original source bytes", File.ReadAllText(Path.Combine(_assets, "nested/keep.js")));
        Assert.AreEqual("new source bytes", File.ReadAllText(added));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void InterruptedBatch_DoesNotPublishAPartialInventory_AndRetryAddsEveryPath(bool canceled) {
        var original = _design.GetManifestItems().ToArray();
        byte[] originalBytes = File.ReadAllBytes(_design.ProjectFilePath);
        string first = Path.Combine(_root, "first.js"), second = Path.Combine(_root, "second.js");
        File.WriteAllText(first, "first bytes");
        File.WriteAllText(second, "second bytes");
        Exception failure = canceled ? new OperationCanceledException() : new IOException("Directory enumeration failed");
        Action update = () => _design.AddManifestPaths(InterruptedPaths(first, failure));

        if (canceled) Assert.AreSame(failure, Assert.ThrowsExactly<OperationCanceledException>(update));
        else Assert.AreSame(failure, Assert.ThrowsExactly<IOException>(update));

        CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(_design.ProjectFilePath));
        CollectionAssert.AreEqual(original, _design.GetManifestItems().ToArray());
        AssertNoTemporaryFiles();
        _design.AddManifestPaths([first, second]);
        var paths = _design.GetManifestItems().Select(item => item.Path).ToArray();
        Assert.AreEqual(1, paths.Count(path => path == "first.js"));
        Assert.AreEqual(1, paths.Count(path => path == "second.js"));
        AssertPersistedInventory();
        Assert.AreEqual("first bytes", File.ReadAllText(first));
        Assert.AreEqual("second bytes", File.ReadAllText(second));
    }

    [TestMethod]
    [DataRow("{ broken")]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("42")]
    public void CorruptManifest_DoesNotOverwriteOriginalOrInventCachedEntries_AndCanRecoverAfterRepair(string content) {
        byte[] validManifest = File.ReadAllBytes(_design.ProjectFilePath);
        File.WriteAllText(_design.ProjectFilePath, content);
        var damaged = WebDesignFileUtil.Create(_design.ProjectFilePath);
        string added = Path.Combine(_root, "added.js");
        File.WriteAllText(added, "preserve this source");

        damaged.AddManifestPath(added);

        Assert.AreEqual(content, File.ReadAllText(damaged.ProjectFilePath));
        Assert.IsEmpty(damaged.GetManifestItems());
        AssertNoTemporaryFiles();
        File.WriteAllBytes(damaged.ProjectFilePath, validManifest);
        damaged.ReloadManifest();
        damaged.AddManifestPath(added);
        Assert.AreEqual(1, damaged.GetManifestItems().Count(item => item.Path == "added.js"));
        CollectionAssert.AreEqual(damaged.GetManifestItems().ToArray(),
            WebDesignFileUtil.Create(damaged.ProjectFilePath).GetManifestItems().ToArray());
        Assert.AreEqual("preserve this source", File.ReadAllText(added));
    }

    [TestMethod]
    public void LockedProjectInfoWrite_DoesNotReplaceSavedData_AndKeepsTheDraftForRetry() {
        var saved = new WpWebProjectData { Title = "Saved title", Desc = "Saved description", Authors = "Original author" };
        _design.SaveProjectData(saved);
        string path = Path.Combine(_root, "project.json");
        byte[] original = File.ReadAllBytes(path);
        var draft = new WpWebProjectData { Title = "Draft title", Desc = "Draft description", Authors = "New author",
            ExtensionData = new() { ["custom"] = JsonSerializer.SerializeToElement(new[] { 1, 2, 3 }) } };

        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            AssertWriteBlocked(() => _design.SaveProjectData(draft));
            CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
            Assert.AreSame(saved, _design.GetOrCreateProjectData());
            Assert.AreEqual("Saved title", _design.GetOrCreateProjectData().Title);
            Assert.AreEqual("Draft title", draft.Title);
            AssertNoTemporaryFiles();
        }

        _design.SaveProjectData(draft);
        Assert.AreSame(draft, _design.GetOrCreateProjectData());
        var reopened = WebDesignFileUtil.Create(_design.ProjectFilePath).GetOrCreateProjectData();
        Assert.AreEqual(draft.Title, reopened.Title);
        Assert.AreEqual(draft.Desc, reopened.Desc);
        Assert.AreEqual(draft.Authors, reopened.Authors);
        Assert.AreEqual(3, reopened.ExtensionData!["custom"].GetArrayLength());
        AssertNoTemporaryFiles();
    }

    private void AssertPersistedInventory() {
        CollectionAssert.AreEqual(_design.GetManifestItems().ToArray(),
            WebDesignFileUtil.Create(_design.ProjectFilePath).GetManifestItems().ToArray());
        Assert.IsTrue(JsonNode.Parse(File.ReadAllText(_design.ProjectFilePath))!["custom"]!["preserve"]!.GetValue<bool>());
        AssertNoTemporaryFiles();
    }

    private void AssertNoTemporaryFiles() => Assert.IsEmpty(Directory.GetFiles(_root, ".*.tmp", SearchOption.AllDirectories));

    private static IEnumerable<string> InterruptedPaths(string first, Exception failure) {
        yield return first;
        throw failure;
    }

    private static void AssertWriteBlocked(Action write) {
        try { write(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }
        Assert.Fail("The locked destination must reject the write.");
    }

    private string _root = null!, _assets = null!;
    private WebDesignFileUtil _design = null!;
}
