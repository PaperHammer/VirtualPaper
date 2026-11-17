using Workloads.Creation.WebBackdrop.Models.SerializableData;

namespace WebBackdrop.Test.T_WebBackdrop;

public partial class WebTransferWorkflowTests {
    [TestMethod]
    [DataRow(false, ".")]
    [DataRow(false, "child")]
    [DataRow(false, "..cache")]
    [DataRow(true, ".")]
    [DataRow(true, "child")]
    [DataRow(true, "..cache")]
    public void ClipboardFolder_RejectsSelfAndAllDescendantsWithoutChangingProject(bool cut, string childName) {
        string source = Directory.CreateDirectory(Path.Combine(_design.ProjectFolder, "assets")).FullName;
        string target = childName == "." ? source : Directory.CreateDirectory(Path.Combine(source, childName)).FullName;
        string file = Path.Combine(source, "keep.txt");
        File.WriteAllText(file, "original bytes");
        _design.AddManifestPathRecursive(source);
        _tree.Refresh(_design);
        var item = _tree.FindItem(source)!;
        _tree.ToggleFolder(item);
        var destination = _tree.FindItem(target)!;
        if (cut) _tree.Cut(item);
        else _tree.Copy(item);
        string[] before = Directory.GetFileSystemEntries(_design.ProjectFolder, "*", SearchOption.AllDirectories);
        byte[] manifest = File.ReadAllBytes(_design.ProjectFilePath);

        // Check eligibility first: a broken guard must not recursively copy a folder into itself.
        Assert.IsFalse(_tree.CanPasteTo(destination));
        Assert.IsFalse(_tree.PasteTo(destination));

        CollectionAssert.AreEquivalent(before, Directory.GetFileSystemEntries(_design.ProjectFolder, "*", SearchOption.AllDirectories));
        CollectionAssert.AreEqual(manifest, File.ReadAllBytes(_design.ProjectFilePath));
        Assert.AreEqual("original bytes", File.ReadAllText(file));
        Assert.AreSame(item, _tree.FindItem(source));
        Assert.AreEqual(cut, item.IsCut);
        Assert.AreEqual(cut, _tree.HasCutItem);
    }

    [TestMethod]
    public void ClipboardDirectoryCopy_PartialFailurePublishesNothing_AndRetryUsesTheOriginalDestination() {
        string source = Directory.CreateDirectory(Path.Combine(_design.ProjectFolder, "assets")).FullName;
        string nested = Directory.CreateDirectory(Path.Combine(source, "nested")).FullName;
        File.WriteAllText(Path.Combine(source, "first.txt"), "copied before the failure");
        string lockedPath = Path.Combine(nested, "locked.txt");
        File.WriteAllText(lockedPath, "source bytes");
        string target = Directory.CreateDirectory(Path.Combine(_design.ProjectFolder, "target")).FullName;
        _design.AddManifestPathRecursive(source);
        _design.AddManifestPath(target);
        _tree.Refresh(_design);
        var original = _tree.FindItem(source)!;
        var destination = _tree.FindItem(target)!;
        _tree.Copy(original);
        byte[] manifest = File.ReadAllBytes(_design.ProjectFilePath);

        using (var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.IsFalse(_tree.PasteTo(destination));

        Assert.IsEmpty(Directory.GetFileSystemEntries(target), "A failed copy must not leave a partial destination or staging folder.");
        CollectionAssert.AreEqual(manifest, File.ReadAllBytes(_design.ProjectFilePath));
        Assert.AreSame(original, _tree.FindItem(source));
        Assert.IsTrue(_tree.CanPasteTo(destination));
        Assert.AreEqual("copied before the failure", File.ReadAllText(Path.Combine(source, "first.txt")));
        Assert.AreEqual("source bytes", File.ReadAllText(lockedPath));

        Assert.IsTrue(_tree.PasteTo(destination));
        string copied = Path.Combine(target, "assets");
        CollectionAssert.AreEqual(new[] { copied }, Directory.GetDirectories(target));
        var copy = _tree.FindItem(copied)!;
        Assert.IsNotNull(copy);
        Assert.AreNotSame(original, copy);
        _tree.ToggleFolder(copy);
        var copiedNested = _tree.FindItem(Path.Combine(copied, "nested"))!;
        _tree.ToggleFolder(copiedNested);
        Assert.IsNotNull(_tree.FindItem(Path.Combine(copied, "nested/locked.txt")));
        var paths = WebDesignFileUtil.Create(_design.ProjectFilePath).GetManifestItems().Select(item => item.Path).ToArray();
        Assert.Contains("target/assets/first.txt", paths);
        Assert.Contains("target/assets/nested/locked.txt", paths);
        Assert.IsFalse(paths.Any(path => path.Contains(".vp-import-", StringComparison.Ordinal)));
        File.WriteAllText(Path.Combine(copied, "nested/locked.txt"), "independent copy");
        Assert.AreEqual("source bytes", File.ReadAllText(lockedPath));
    }

    [TestMethod]
    public void ClipboardCut_LockedFileKeepsCutStateAndIdentity_AndRetryMovesItOnce() {
        string source = Path.Combine(_design.ProjectFolder, "draft.js");
        File.WriteAllText(source, "unsaved project source");
        string target = Directory.CreateDirectory(Path.Combine(_design.ProjectFolder, "target")).FullName;
        _design.AddManifestPath(source);
        _design.AddManifestPath(target);
        _tree.Refresh(_design);
        var item = _tree.FindItem(source)!;
        var destination = _tree.FindItem(target)!;
        item.IsSaved = false;
        _tree.Cut(item);
        byte[] manifest = File.ReadAllBytes(_design.ProjectFilePath);
        using (var locked = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.IsFalse(_tree.PasteTo(destination));

        Assert.AreEqual("unsaved project source", File.ReadAllText(source));
        Assert.IsEmpty(Directory.GetFileSystemEntries(target));
        CollectionAssert.AreEqual(manifest, File.ReadAllBytes(_design.ProjectFilePath));
        Assert.AreSame(item, _tree.FindItem(source));
        Assert.IsFalse(item.IsSaved);
        Assert.IsTrue(item.IsCut);
        Assert.IsTrue(_tree.HasCutItem);

        Assert.IsTrue(_tree.PasteTo(destination));
        string moved = Path.Combine(target, "draft.js");
        Assert.AreSame(item, _tree.FindItem(moved));
        Assert.AreSame(destination, item.Parent);
        Assert.IsFalse(item.IsSaved);
        Assert.IsFalse(item.IsCut);
        Assert.IsFalse(_tree.HasCutItem);
        Assert.IsFalse(_tree.CanPasteTo(destination));
        Assert.IsFalse(File.Exists(source));
        Assert.AreEqual("unsaved project source", File.ReadAllText(moved));
        var paths = WebDesignFileUtil.Create(_design.ProjectFilePath).GetManifestItems().Select(entry => entry.Path).ToArray();
        Assert.Contains("target/draft.js", paths);
        Assert.DoesNotContain("draft.js", paths);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ClipboardCopy_AllowsParentAndSimilarSiblingNames_WithoutOverwritingSources(bool sibling) {
        string source = Directory.CreateDirectory(Path.Combine(_design.ProjectFolder, "assets")).FullName;
        File.WriteAllText(Path.Combine(source, "keep.txt"), "source bytes");
        string target = sibling
            ? Directory.CreateDirectory(Path.Combine(_design.ProjectFolder, "assets-backup")).FullName
            : _design.ProjectFolder;
        _design.AddManifestPathRecursive(source);
        if (sibling) _design.AddManifestPath(target);
        _tree.Refresh(_design);
        var original = _tree.FindItem(source)!;
        var destination = _tree.FindItem(target)!;
        _tree.Copy(original);

        Assert.IsTrue(_tree.CanPasteTo(destination));
        Assert.IsTrue(_tree.PasteTo(destination));

        string copied = Path.Combine(target, sibling ? "assets" : "assets(1)");
        Assert.AreEqual("source bytes", File.ReadAllText(Path.Combine(copied, "keep.txt")));
        Assert.AreEqual("source bytes", File.ReadAllText(Path.Combine(source, "keep.txt")));
        Assert.AreSame(original, _tree.FindItem(source));
        Assert.AreNotSame(original, _tree.FindItem(copied));
        Assert.IsTrue(_tree.CanPasteTo(destination), "Copy retains its source for a later paste.");
        string relative = Path.GetRelativePath(_design.ProjectFolder, copied).Replace('\\', '/');
        Assert.IsTrue(WebDesignFileUtil.Create(_design.ProjectFilePath).GetManifestItems()
            .Any(entry => entry.Path == relative + "/keep.txt"));
    }
}
