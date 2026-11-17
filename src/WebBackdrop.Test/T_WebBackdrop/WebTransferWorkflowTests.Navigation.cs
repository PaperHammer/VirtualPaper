using Workloads.Creation.WebBackdrop.Models;

namespace WebBackdrop.Test.T_WebBackdrop;

public partial class WebTransferWorkflowTests {
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SelectDeepFile_LoadsAndExpandsOnlyItsAncestors(bool useManifest) {
        string file = WriteNavigationFile("scripts/modules/main.js");
        string siblingFile = WriteNavigationFile("unrelated/deep/other.js");
        _design.AddManifestPathRecursive(Path.Combine(_design.ProjectFolder, "scripts"));
        _design.AddManifestPathRecursive(Path.Combine(_design.ProjectFolder, "unrelated"));
        if (useManifest) _tree.Refresh(_design); else _tree.Refresh(_design.ProjectFolder);
        var root = _tree.FileItems.Single();
        var scripts = _tree.FindItem(Path.Combine(_design.ProjectFolder, "scripts"))!;
        var sibling = _tree.FindItem(Path.Combine(_design.ProjectFolder, "unrelated"))!;
        Assert.IsFalse(scripts.IsChildrenLoaded);
        Assert.IsFalse(sibling.IsChildrenLoaded);
        Assert.IsNull(_tree.FindItem(file));
        byte[] manifest = File.ReadAllBytes(_design.ProjectFilePath);

        _tree.SelectFile(file.Replace('\\', '/'));

        var leaf = _tree.FindItem(file);
        Assert.IsNotNull(leaf);
        Assert.AreSame(root, _tree.FileItems.Single());
        Assert.AreSame(scripts, leaf.Parent!.Parent);
        Assert.IsTrue(scripts.IsChildrenLoaded && scripts.IsExpanded);
        Assert.IsTrue(leaf.Parent.IsChildrenLoaded && leaf.Parent.IsExpanded);
        Assert.IsFalse(sibling.IsChildrenLoaded || sibling.IsExpanded);
        Assert.IsTrue(sibling.Children.Single().IsPlaceholder);
        Assert.IsNull(_tree.FindItem(siblingFile));
        Assert.AreEqual("original bytes", File.ReadAllText(file));
        CollectionAssert.AreEqual(manifest, File.ReadAllBytes(_design.ProjectFilePath));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SelectUntrackedFile_MaterializesItsPathWithoutPersistingOrLoadingSiblingFiles(bool trackedParent) {
        string assets = Directory.CreateDirectory(Path.Combine(_design.ProjectFolder, "assets")).FullName;
        if (trackedParent) _design.AddManifestPath(assets);
        _tree.Refresh(_design);
        string file = WriteNavigationFile("assets/new/main.js");
        string ignored = WriteNavigationFile("assets/unrelated/other.js");
        byte[] manifest = File.ReadAllBytes(_design.ProjectFilePath);

        _tree.SelectFile(file);

        var leaf = _tree.FindItem(file);
        Assert.IsNotNull(leaf);
        Assert.AreEqual(WebFileItemType.File, leaf.Type);
        Assert.AreEqual("new", leaf.Parent!.FileName);
        Assert.AreEqual(assets, leaf.Parent.Parent!.FilePath);
        Assert.IsTrue(leaf.Parent.IsExpanded && leaf.Parent.Parent.IsExpanded);
        Assert.IsNull(_tree.FindItem(Path.GetDirectoryName(ignored)!));
        Assert.IsNull(_tree.FindItem(ignored));
        Assert.AreEqual("original bytes", File.ReadAllText(file));
        CollectionAssert.AreEqual(manifest, File.ReadAllBytes(_design.ProjectFilePath));
    }

    [TestMethod]
    public void ReselectFile_PreservesItsNodeUnsavedStateAndRenameDraft() {
        string file = WriteNavigationFile("scripts/main.js");
        _design.AddManifestPathRecursive(Path.Combine(_design.ProjectFolder, "scripts"));
        _tree.Refresh(_design);
        _tree.SelectFile(file);
        var original = _tree.FindItem(file)!;
        _tree.SetFileSaved(file, false);
        _tree.BeginRename(original);
        original.RenameText = "draft.js";

        _tree.SelectFile(file.ToUpperInvariant());

        Assert.AreSame(original, _tree.FindItem(file));
        Assert.IsFalse(original.IsSaved);
        Assert.IsTrue(original.IsRenaming);
        Assert.AreEqual("draft.js", original.RenameText);
        Assert.AreEqual(1, original.Parent!.Children.Count(item => item.FilePath.Equals(file, StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual("original bytes", File.ReadAllText(file));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SelectOutsideProject_DoesNotChangeTreeOrLoadUnrelatedBranches(bool samePrefix) {
        string lazyFile = WriteNavigationFile("scripts/main.js");
        _design.AddManifestPathRecursive(Path.Combine(_design.ProjectFolder, "scripts"));
        _tree.Refresh(_design);
        var root = _tree.FileItems.Single();
        var children = root.Children.ToArray();
        string externalRoot = samePrefix ? _design.ProjectFolder + "-backup" : _root;
        Directory.CreateDirectory(externalRoot);
        string external = Path.Combine(externalRoot, "outside.js");
        File.WriteAllText(external, "outside project");
        byte[] manifest = File.ReadAllBytes(_design.ProjectFilePath);

        _tree.SelectFile(external);

        Assert.AreSame(root, _tree.FileItems.Single());
        CollectionAssert.AreEqual(children, root.Children.ToArray());
        Assert.IsFalse(_tree.FindItem(Path.GetDirectoryName(lazyFile)!)!.IsChildrenLoaded);
        Assert.IsNull(_tree.FindItem(external));
        Assert.AreEqual("outside project", File.ReadAllText(external));
        CollectionAssert.AreEqual(manifest, File.ReadAllBytes(_design.ProjectFilePath));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SelectInvalidDescendant_DoesNotInventMissingNodes(bool belowFile) {
        string existing = WriteNavigationFile("scripts/main.js");
        _design.AddManifestPathRecursive(Path.Combine(_design.ProjectFolder, "scripts"));
        _tree.Refresh(_design);
        string missing = belowFile ? Path.Combine(existing, "child.js") : Path.Combine(_design.ProjectFolder, "scripts/missing.js");
        byte[] manifest = File.ReadAllBytes(_design.ProjectFilePath);

        _tree.SelectFile(missing);

        Assert.IsNull(_tree.FindItem(missing));
        Assert.IsNotNull(_tree.FindItem(existing));
        Assert.IsEmpty(_tree.FindItem(existing)!.Children);
        Assert.AreEqual("original bytes", File.ReadAllText(existing));
        CollectionAssert.AreEqual(manifest, File.ReadAllBytes(_design.ProjectFilePath));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("note.js")]
    public async Task CancelledOrUnchangedRename_LeavesFileAndManifestUntouched(string name) {
        string file = WriteNavigationFile("note.js");
        _design.AddManifestPath(file); _tree.Refresh(_design);
        var item = _tree.FindItem(file)!;
        _tree.BeginRename(item);
        byte[] manifest = File.ReadAllBytes(_design.ProjectFilePath);
        var files = Directory.GetFiles(_design.ProjectFolder);

        await _tree.RenameToAsync(item, name);

        Assert.IsFalse(item.IsRenaming || item.IsRenameInvalid);
        Assert.AreSame(item, _tree.FindItem(file));
        Assert.AreEqual("original bytes", File.ReadAllText(file));
        CollectionAssert.AreEquivalent(files, Directory.GetFiles(_design.ProjectFolder));
        CollectionAssert.AreEqual(manifest, File.ReadAllBytes(_design.ProjectFilePath));
    }

    [TestMethod]
    public async Task StaleRenameRequest_DoesNotRenamePreviousItemOrCancelCurrentDraft() {
        string first = WriteNavigationFile("first.js");
        string second = WriteNavigationFile("second.js");
        _design.AddManifestPath(first); _design.AddManifestPath(second); _tree.Refresh(_design);
        var previous = _tree.FindItem(first)!;
        var current = _tree.FindItem(second)!;
        _tree.BeginRename(previous);
        _tree.BeginRename(current);
        current.RenameText = "current-draft.js";
        byte[] manifest = File.ReadAllBytes(_design.ProjectFilePath);

        await _tree.RenameToAsync(previous, "stale.js");

        Assert.IsFalse(previous.IsRenaming);
        Assert.IsTrue(current.IsRenaming);
        Assert.AreEqual("current-draft.js", current.RenameText);
        Assert.AreSame(previous, _tree.FindItem(first));
        Assert.AreSame(current, _tree.FindItem(second));
        Assert.IsFalse(File.Exists(Path.Combine(_design.ProjectFolder, "stale.js")));
        Assert.AreEqual("original bytes", File.ReadAllText(first));
        Assert.AreEqual("original bytes", File.ReadAllText(second));
        CollectionAssert.AreEqual(manifest, File.ReadAllBytes(_design.ProjectFilePath));
    }

    private string WriteNavigationFile(string relative) {
        string path = Path.Combine(_design.ProjectFolder, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "original bytes");
        return path;
    }
}
