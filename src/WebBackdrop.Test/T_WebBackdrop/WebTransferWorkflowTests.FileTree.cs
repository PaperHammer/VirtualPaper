using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.Models.SerializableData;

namespace WebBackdrop.Test.T_WebBackdrop;

public partial class WebTransferWorkflowTests {
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LockedDelete_KeepsManifestNodeAndCutState_AndCanRetry(bool folder) {
        string parent = folder ? Directory.CreateDirectory(Path.Combine(_design.ProjectFolder, "assets")).FullName : _design.ProjectFolder;
        string file = Path.Combine(parent, "locked.txt");
        File.WriteAllText(file, "user content");
        string path = folder ? parent : file;
        _design.AddManifestPathRecursive(path);
        _tree.Refresh(_design);
        var item = _tree.FindItem(path)!;
        _tree.Cut(item);
        byte[] manifest = File.ReadAllBytes(_design.ProjectFilePath);
        using (var locked = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            _tree.Delete(item);
        }
        Assert.AreEqual("user content", File.ReadAllText(file));
        Assert.AreSame(item, _tree.FindItem(path));
        Assert.IsTrue(item.IsCut);
        Assert.IsTrue(_tree.HasCutItem);
        CollectionAssert.AreEqual(manifest, File.ReadAllBytes(_design.ProjectFilePath));
        Assert.IsTrue(WebDesignFileUtil.Create(_design.ProjectFilePath).GetManifestItems()
            .Any(entry => entry.Path == Path.GetRelativePath(_design.ProjectFolder, path).Replace('\\', '/')));

        _tree.Delete(item);
        Assert.IsFalse(File.Exists(file));
        Assert.IsNull(_tree.FindItem(path));
        Assert.IsFalse(_tree.HasCutItem);
        Assert.IsFalse(WebDesignFileUtil.Create(_design.ProjectFilePath).GetManifestItems()
            .Any(entry => entry.Path == Path.GetRelativePath(_design.ProjectFolder, path).Replace('\\', '/')));
    }

    [TestMethod]
    public async Task LockedRename_PreservesOriginalAndManifest_ThenRetryReusesNode() {
        string source = Path.Combine(_design.ProjectFolder, "original.js");
        File.WriteAllText(source, "original script");
        _design.AddManifestPath(source); _tree.Refresh(_design);
        var item = _tree.FindItem(source)!;
        byte[] manifest = File.ReadAllBytes(_design.ProjectFilePath);
        _tree.BeginRename(item);
        using (var locked = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            await _tree.RenameToAsync(item, "renamed.js");
        Assert.AreSame(item, _tree.FindItem(source));
        Assert.IsFalse(item.IsRenaming);
        CollectionAssert.AreEqual(manifest, File.ReadAllBytes(_design.ProjectFilePath));
        Assert.IsFalse(File.Exists(Path.Combine(_design.ProjectFolder, "renamed.js")));
        _tree.BeginRename(item);
        await _tree.RenameToAsync(item, "renamed.js");
        Assert.AreSame(item, _tree.FindItem(Path.Combine(_design.ProjectFolder, "renamed.js")));
        Assert.IsFalse(File.Exists(source));
        Assert.AreEqual("original script", File.ReadAllText(item.FilePath));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MoveFolder_RebindsLoadedDescendants_AndLoadsLazyChildrenFromNewManifest(bool loadNested) {
        string source = Directory.CreateDirectory(Path.Combine(_design.ProjectFolder, "assets")).FullName;
        string nested = Directory.CreateDirectory(Path.Combine(source, "nested")).FullName;
        string file = Path.Combine(nested, "image.txt"); File.WriteAllText(file, "image bytes");
        string target = Directory.CreateDirectory(Path.Combine(_design.ProjectFolder, "destination")).FullName;
        _design.AddManifestPathRecursive(source); _design.AddManifestPath(target); _tree.Refresh(_design);
        var folder = _tree.FindItem(source)!;
        _tree.ToggleFolder(folder);
        var child = _tree.FindItem(nested)!;
        if (loadNested) _tree.ToggleFolder(child);
        var leaf = loadNested ? _tree.FindItem(file) : null;
        var destination = _tree.FindItem(target)!;
        CollectionAssert.AreEqual(new[] { folder }, _tree.MoveItems([folder], destination).ToArray());
        string moved = Path.Combine(target, "assets");
        Assert.AreSame(folder, _tree.FindItem(moved));
        Assert.AreSame(destination, folder.Parent);
        Assert.AreSame(child, _tree.FindItem(Path.Combine(moved, "nested")));
        Assert.AreSame(folder, child.Parent);
        Assert.AreEqual(loadNested, child.IsChildrenLoaded);
        if (!loadNested) _tree.ToggleFolder(child);
        var actual = _tree.FindItem(Path.Combine(moved, "nested/image.txt"));
        Assert.IsNotNull(actual);
        Assert.AreSame(child, actual.Parent);
        if (loadNested) Assert.AreSame(leaf, actual);
        Assert.AreEqual("image bytes", File.ReadAllText(actual.FilePath));
        var paths = WebDesignFileUtil.Create(_design.ProjectFilePath).GetManifestItems().Select(entry => entry.Path).ToArray();
        Assert.Contains("destination/assets/nested/image.txt", paths);
        Assert.IsFalse(paths.Any(path => path == "assets" || path.StartsWith("assets/", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void MixedMove_LockedItemStaysInPlace_AndLaterItemsStillMove() {
        string target = Directory.CreateDirectory(Path.Combine(_design.ProjectFolder, "target")).FullName;
        string blocked = Path.Combine(_design.ProjectFolder, "a-locked.txt");
        string movable = Path.Combine(_design.ProjectFolder, "z-valid.txt");
        File.WriteAllText(blocked, "keep"); File.WriteAllText(movable, "move");
        foreach (var path in new[] { target, blocked, movable }) _design.AddManifestPath(path);
        _tree.Refresh(_design);
        var first = _tree.FindItem(blocked)!; var second = _tree.FindItem(movable)!;
        using (var locked = new FileStream(blocked, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            CollectionAssert.AreEqual(new[] { second }, _tree.MoveItems([first, second], _tree.FindItem(target)).ToArray());
        }
        Assert.AreSame(first, _tree.FindItem(blocked));
        Assert.AreEqual("keep", File.ReadAllText(blocked));
        Assert.AreSame(second, _tree.FindItem(Path.Combine(target, "z-valid.txt")));
        Assert.AreEqual("move", File.ReadAllText(second.FilePath));
        var paths = WebDesignFileUtil.Create(_design.ProjectFilePath).GetManifestItems().Select(entry => entry.Path).ToArray();
        Assert.Contains("a-locked.txt", paths);
        Assert.Contains("target/z-valid.txt", paths);
        Assert.DoesNotContain("z-valid.txt", paths);
    }
}
