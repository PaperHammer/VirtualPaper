using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.Models.SerializableData;
using VirtualPaper.Common.Utils.ProjectSystem.Events;
using Workloads.Creation.WebBackdrop.Core.Utils;

namespace WebBackdrop.Test.T_WebBackdrop;

public partial class WebTransferWorkflowTests {
    public TestContext TestContext { get; set; } = null!;
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Rename_ManifestWriteFails_RestoresDiskAndNodes_AndCanRetry(bool folder) {
        var state = PrepareManifestFailure(folder) with {
            Destination = Path.Combine(_design.ProjectFolder, folder ? "renamed-assets" : "renamed-source.js")
        };
        byte[] manifest = File.ReadAllBytes(_design.ProjectFilePath);
        var inventory = _design.GetManifestItems().ToArray();
        using (var locked = LockManifest())
            await _tree.RenameAsync(state.Item, _ => Task.FromResult<string?>(state.Destination));

        AssertManifestFailureRestored(state);
        CollectionAssert.AreEqual(manifest, File.ReadAllBytes(_design.ProjectFilePath));
        CollectionAssert.AreEqual(inventory, _design.GetManifestItems().ToArray());
        await _tree.RenameAsync(state.Item, _ => Task.FromResult<string?>(state.Destination));
        AssertCommittedMove(state);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void Paste_ManifestWriteFails_RestoresSourceAndPreservesExistingDestination_AndCanRetry(bool folder, bool cut) {
        var state = PrepareManifestFailure(folder, occupiedDestination: true);
        string[] destinationBefore = Directory.GetFileSystemEntries(state.Target.FilePath, "*", SearchOption.AllDirectories);
        byte[] manifest = File.ReadAllBytes(_design.ProjectFilePath);
        var inventory = _design.GetManifestItems().ToArray();
        if (cut) _tree.Cut(state.Item);
        else _tree.Copy(state.Item);
        using (var locked = LockManifest()) Assert.IsFalse(_tree.PasteTo(state.Target));

        AssertManifestFailureRestored(state);
        Assert.AreEqual(cut, state.Item.IsCut);
        Assert.AreEqual(cut, _tree.HasCutItem);
        Assert.IsTrue(_tree.CanPasteTo(state.Target));
        CollectionAssert.AreEquivalent(destinationBefore, Directory.GetFileSystemEntries(state.Target.FilePath, "*", SearchOption.AllDirectories));
        CollectionAssert.AreEqual(manifest, File.ReadAllBytes(_design.ProjectFilePath));
        CollectionAssert.AreEqual(inventory, _design.GetManifestItems().ToArray());
        Assert.AreEqual("destination bytes", File.ReadAllText(Payload(Path.Combine(state.Target.FilePath, Path.GetFileName(state.Source)), folder)));

        Assert.IsTrue(_tree.PasteTo(state.Target));
        if (cut) {
            AssertCommittedMove(state);
            Assert.IsFalse(_tree.HasCutItem);
        }
        else {
            Assert.AreSame(state.Item, _tree.FindItem(state.Source));
            Assert.AreNotSame(state.Item, _tree.FindItem(state.Destination));
            Assert.AreEqual("source bytes", File.ReadAllText(Payload(state.Source, folder)));
            Assert.AreEqual("source bytes", File.ReadAllText(Payload(state.Destination, folder)));
            AssertRegisteredDestination(state);
        }
        Assert.AreEqual("destination bytes", File.ReadAllText(Payload(Path.Combine(state.Target.FilePath, Path.GetFileName(state.Source)), folder)));
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void DragMove_ManifestWriteFails_RestoresNativeCollectionPlacement_AndCanRetry(bool folder, bool nativeDrop) {
        var state = PrepareManifestFailure(folder);
        byte[] manifest = File.ReadAllBytes(_design.ProjectFilePath);
        if (nativeDrop) {
            _tree.ToggleFolder(state.Target);
            state.OriginalParent.Children.Remove(state.Item);
            state.Target.Children.Add(state.Item);
        }
        using (var locked = LockManifest()) Assert.IsEmpty(_tree.MoveItems([state.Item], state.Target));

        AssertManifestFailureRestored(state);
        CollectionAssert.AreEqual(manifest, File.ReadAllBytes(_design.ProjectFilePath));
        Assert.AreEqual(1, state.OriginalParent.Children.Count(item => ReferenceEquals(item, state.Item)));
        Assert.IsFalse(state.Target.Children.Contains(state.Item));
        CollectionAssert.AreEqual(new[] { state.Item }, _tree.MoveItems([state.Item], state.Target).ToArray());
        AssertCommittedMove(state);
        Assert.AreSame(state.Target, state.Item.Parent);
    }

    [TestMethod]
    [DataRow("rename")]
    [DataRow("cut")]
    [DataRow("copy")]
    [DataRow("drag")]
    public async Task InvalidManifest_DoesNotCommitFileOperations_AndCanRetryAfterRepair(string operation) {
        var state = PrepareManifestFailure(folder: false);
        if (operation == "rename") state = state with { Destination = Path.Combine(_design.ProjectFolder, "renamed-source.js") };
        byte[] validManifest = File.ReadAllBytes(_design.ProjectFilePath);
        const string broken = "{ invalid manifest";
        File.WriteAllText(_design.ProjectFilePath, broken);
        _design.ReloadManifest();
        if (operation == "rename") await _tree.RenameAsync(state.Item, _ => Task.FromResult<string?>(state.Destination));
        else if (operation == "drag") Assert.IsEmpty(_tree.MoveItems([state.Item], state.Target));
        else {
            if (operation == "cut") _tree.Cut(state.Item);
            else _tree.Copy(state.Item);
            Assert.IsFalse(_tree.PasteTo(state.Target));
        }

        AssertManifestFailureRestored(state);
        Assert.AreEqual(broken, File.ReadAllText(_design.ProjectFilePath));
        File.WriteAllBytes(_design.ProjectFilePath, validManifest);
        _design.ReloadManifest();
        if (operation == "rename") await _tree.RenameAsync(state.Item, _ => Task.FromResult<string?>(state.Destination));
        else if (operation == "drag") Assert.HasCount(1, _tree.MoveItems([state.Item], state.Target));
        else Assert.IsTrue(_tree.PasteTo(state.Target));
        Assert.AreEqual("source bytes", File.ReadAllText(state.Destination));
        AssertRegisteredDestination(state);
    }

    [TestMethod]
    public void Copy_LockedSourceDoesNotPublishAPartialFile_AndCanRetry() {
        var state = PrepareManifestFailure(folder: false, occupiedDestination: true);
        string[] before = Directory.GetFileSystemEntries(state.Target.FilePath);
        byte[] manifest = File.ReadAllBytes(_design.ProjectFilePath);
        _tree.Copy(state.Item);
        using (var locked = new FileStream(state.Source, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.IsFalse(_tree.PasteTo(state.Target));
        AssertManifestFailureRestored(state);
        CollectionAssert.AreEquivalent(before, Directory.GetFileSystemEntries(state.Target.FilePath));
        CollectionAssert.AreEqual(manifest, File.ReadAllBytes(_design.ProjectFilePath));
        Assert.IsTrue(_tree.PasteTo(state.Target));
        Assert.AreEqual("source bytes", File.ReadAllText(state.Destination));
        Assert.AreEqual("source bytes", File.ReadAllText(state.Source));
        AssertRegisteredDestination(state);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RealWatcher_DoesNotReintroduceRolledBackPaths(bool folder) {
        var state = PrepareManifestFailure(folder);
        var inventory = _design.GetManifestItems().ToArray();
        string marker = Path.Combine(_design.ProjectFolder, "rollback-barrier.txt");
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var manager = new ProjectFileManager(_design.ProjectFolder,
            path => _design.AddManifestPath(path), _design.RemoveManifestPath, _design.RenameManifestPath,
            manifestSyncRoot: _design.ManifestSyncRoot);
        manager.Changed += change => {
            if (change.Type == ProjectChangeType.Created && change.Path.Equals(marker, StringComparison.OrdinalIgnoreCase)) observed.TrySetResult();
        };
        manager.Start();
        _tree.Cut(state.Item);
        using (var locked = LockManifest()) Assert.IsFalse(_tree.PasteTo(state.Target));
        File.WriteAllText(marker, "watcher barrier");
        await observed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken);

        AssertManifestFailureRestored(state);
        CollectionAssert.AreEqual(inventory, _design.GetManifestItems().Where(item => item.Path != "rollback-barrier.txt").ToArray());
        var reopened = WebDesignFileUtil.Create(_design.ProjectFilePath).GetManifestItems();
        CollectionAssert.AreEqual(inventory, reopened.Where(item => item.Path != "rollback-barrier.txt").ToArray());
    }

    private ManifestFailureState PrepareManifestFailure(bool folder, bool occupiedDestination = false) {
        string source = Path.Combine(_design.ProjectFolder, folder ? "rollback-assets" : "rollback-source.js");
        WritePayload(source, folder, "source bytes");
        string target = Directory.CreateDirectory(Path.Combine(_design.ProjectFolder, "rollback-target")).FullName;
        if (occupiedDestination) WritePayload(Path.Combine(target, Path.GetFileName(source)), folder, "destination bytes");
        _design.AddManifestPathRecursive(source);
        _design.AddManifestPathRecursive(target);
        _tree.Refresh(_design);
        var item = _tree.FindItem(source)!;
        if (folder) {
            _tree.ToggleFolder(item);
            _tree.ToggleFolder(_tree.FindItem(Path.Combine(source, "nested"))!);
        }
        var leaf = _tree.FindItem(Payload(source, folder))!;
        item.IsSaved = false;
        leaf.IsSaved = false;
        string name = occupiedDestination ? (folder ? "rollback-assets(1)" : "rollback-source(1).js") : Path.GetFileName(source);
        return new(item, leaf, _tree.FindItem(target)!, item.Parent!, source, Path.Combine(target, name), folder);
    }

    private FileStream LockManifest() => new(_design.ProjectFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

    private void AssertManifestFailureRestored(ManifestFailureState state) {
        Assert.AreEqual("source bytes", File.ReadAllText(Payload(state.Source, state.Folder)));
        Assert.IsFalse(File.Exists(state.Destination) || Directory.Exists(state.Destination));
        Assert.AreSame(state.Item, _tree.FindItem(state.Source));
        Assert.AreSame(state.Leaf, _tree.FindItem(Payload(state.Source, state.Folder)));
        Assert.AreSame(state.OriginalParent, state.Item.Parent);
        Assert.IsFalse(state.Item.IsSaved);
        Assert.IsFalse(state.Leaf.IsSaved);
        Assert.IsEmpty(Directory.GetFiles(_design.ProjectFolder, ".*.tmp", SearchOption.AllDirectories));
    }

    private void AssertCommittedMove(ManifestFailureState state) {
        Assert.IsFalse(File.Exists(state.Source) || Directory.Exists(state.Source));
        Assert.AreEqual("source bytes", File.ReadAllText(Payload(state.Destination, state.Folder)));
        Assert.AreSame(state.Item, _tree.FindItem(state.Destination));
        Assert.AreSame(state.Leaf, _tree.FindItem(Payload(state.Destination, state.Folder)));
        Assert.IsFalse(state.Item.IsSaved);
        Assert.IsFalse(state.Leaf.IsSaved);
        AssertRegisteredDestination(state);
    }

    private void AssertRegisteredDestination(ManifestFailureState state) {
        var reopened = WebDesignFileUtil.Create(_design.ProjectFilePath).GetManifestItems();
        string relative = Path.GetRelativePath(_design.ProjectFolder, Payload(state.Destination, state.Folder)).Replace('\\', '/');
        Assert.IsTrue(reopened.Any(item => item.Path == relative));
        CollectionAssert.AreEqual(_design.GetManifestItems().ToArray(), reopened.ToArray());
    }

    private static string Payload(string path, bool folder) => folder ? Path.Combine(path, "nested/source.txt") : path;

    private static void WritePayload(string path, bool folder, string content) {
        if (folder) {
            Directory.CreateDirectory(Path.Combine(path, "nested"));
            Directory.CreateDirectory(Path.Combine(path, "empty"));
        }
        File.WriteAllText(Payload(path, folder), content);
    }

    private sealed record ManifestFailureState(WebFileItem Item, WebFileItem Leaf, WebFileItem Target,
        WebFileItem OriginalParent, string Source, string Destination, bool Folder);
}
