using VirtualPaper.Common.Utils.ProjectSystem.Events;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Creation.WebBackdrop.Models.SerializableData;

namespace WebBackdrop.Test.T_WebBackdrop;

public partial class WebTransferWorkflowTests {
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Delete_ManifestWriteFails_RestoresContentNodesAndClipboard_AndCanRetry(bool folder) {
        var state = PrepareManifestFailure(folder);
        _tree.Cut(state.Item);
        byte[] manifest = File.ReadAllBytes(_design.ProjectFilePath);
        using (var locked = LockManifest()) _tree.Delete(state.Item);

        AssertManifestFailureRestored(state);
        Assert.IsTrue(state.Item.IsCut);
        Assert.IsTrue(_tree.HasCutItem);
        CollectionAssert.AreEqual(manifest, File.ReadAllBytes(_design.ProjectFilePath));
        AssertNoTransferStaging();

        _tree.Delete(state.Item);
        AssertDeleted(state);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Delete_InvalidManifest_KeepsOriginal_AndCanRetryAfterRepair(bool folder) {
        var state = PrepareManifestFailure(folder);
        byte[] validManifest = File.ReadAllBytes(_design.ProjectFilePath);
        const string broken = "{ invalid delete manifest";
        File.WriteAllText(_design.ProjectFilePath, broken);
        _design.ReloadManifest();
        _tree.Delete(state.Item);

        AssertManifestFailureRestored(state);
        Assert.AreEqual(broken, File.ReadAllText(_design.ProjectFilePath));
        AssertNoTransferStaging();
        File.WriteAllBytes(_design.ProjectFilePath, validManifest);
        _design.ReloadManifest();
        _tree.Delete(state.Item);
        AssertDeleted(state);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Delete_AlreadyRemovedOnDisk_ClearsStaleNodeManifestAndClipboard(bool folder) {
        var state = PrepareManifestFailure(folder);
        _tree.Cut(state.Item);
        if (folder) Directory.Delete(state.Source, recursive: true);
        else File.Delete(state.Source);
        Assert.IsTrue(state.Item.ExistsOnDisk, "The node still holds the pre-watcher state.");

        _tree.Delete(state.Item);

        AssertDeleted(state);
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, false, false)]
    [DataRow(true, true, false)]
    [DataRow(false, true, true)]
    [DataRow(true, true, true)]
    public async Task ExternalTransfer_ManifestWriteFails_KeepsSourceAndExistingDestination_AndCanRetry(
        bool folder, bool move, bool forceCopyFallback) {
        string source = External(folder);
        string existing = Path.Combine(_design.ProjectFolder, Path.GetFileName(source));
        WritePayload(existing, folder, "existing user bytes");
        _design.AddManifestPathRecursive(existing);
        _tree.Refresh(_design);
        var existingNode = _tree.FindItem(existing);
        var before = Directory.GetFileSystemEntries(_design.ProjectFolder, "*", SearchOption.AllDirectories);
        var sourceBefore = folder ? Directory.GetFileSystemEntries(source, "*", SearchOption.AllDirectories) : [source];
        byte[] manifest = File.ReadAllBytes(_design.ProjectFilePath);
        using (var sourceLock = forceCopyFallback ? LockExternalSource(source, folder) : null)
        using (var manifestLock = LockManifest())
            Assert.IsFalse(await _tree.TransferExternalAsync(source, _design.ProjectFolder, move));

        Assert.AreEqual("source bytes", File.ReadAllText(ExternalPayload(source, folder)));
        Assert.AreEqual("existing user bytes", File.ReadAllText(Payload(existing, folder)));
        Assert.AreSame(existingNode, _tree.FindItem(existing));
        CollectionAssert.AreEquivalent(sourceBefore,
            folder ? Directory.GetFileSystemEntries(source, "*", SearchOption.AllDirectories) : [source]);
        CollectionAssert.AreEquivalent(before, Directory.GetFileSystemEntries(_design.ProjectFolder, "*", SearchOption.AllDirectories));
        CollectionAssert.AreEqual(manifest, File.ReadAllBytes(_design.ProjectFilePath));
        AssertNoTransferStaging();

        Assert.IsTrue(await _tree.TransferExternalAsync(source, _design.ProjectFolder, move));
        string destination = Path.Combine(_design.ProjectFolder, folder ? "assets(1)" : "data(1).txt");
        Assert.AreEqual("source bytes", File.ReadAllText(ExternalPayload(destination, folder)));
        Assert.AreEqual(!move, Path.Exists(source));
        Assert.IsNotNull(_tree.FindItem(destination));
        AssertPersistedTransfer(destination, folder);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task ExternalTransfer_InvalidManifest_KeepsOriginal_AndCanRetryAfterRepair(bool folder, bool move) {
        string source = External(folder);
        var before = Directory.GetFileSystemEntries(_design.ProjectFolder);
        var sourceBefore = folder ? Directory.GetFileSystemEntries(source, "*", SearchOption.AllDirectories) : [source];
        byte[] validManifest = File.ReadAllBytes(_design.ProjectFilePath);
        const string broken = "{ invalid import manifest";
        File.WriteAllText(_design.ProjectFilePath, broken);
        _design.ReloadManifest();
        Assert.IsFalse(await _tree.TransferExternalAsync(source, _design.ProjectFolder, move));

        Assert.AreEqual("source bytes", File.ReadAllText(ExternalPayload(source, folder)));
        Assert.AreEqual(broken, File.ReadAllText(_design.ProjectFilePath));
        CollectionAssert.AreEquivalent(sourceBefore,
            folder ? Directory.GetFileSystemEntries(source, "*", SearchOption.AllDirectories) : [source]);
        CollectionAssert.AreEquivalent(before, Directory.GetFileSystemEntries(_design.ProjectFolder));
        AssertNoTransferStaging();
        File.WriteAllBytes(_design.ProjectFilePath, validManifest);
        _design.ReloadManifest();
        Assert.IsTrue(await _tree.TransferExternalAsync(source, _design.ProjectFolder, move));
        string destination = Path.Combine(_design.ProjectFolder, Path.GetFileName(source));
        Assert.AreEqual("source bytes", File.ReadAllText(ExternalPayload(destination, folder)));
        Assert.AreEqual(!move, Path.Exists(source));
        AssertPersistedTransfer(destination, folder);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExternalMove_SourceCleanupFails_KeepsCommittedCopyAndReportsSuccess(bool folder) {
        string source = External(folder);
        string destination = Path.Combine(_design.ProjectFolder, Path.GetFileName(source));
        // Permit copying but reject renaming/deleting, exercising the copy fallback without a second drive.
        using (var sourceLock = LockExternalSource(source, folder))
            Assert.IsTrue(await _tree.TransferExternalAsync(source, _design.ProjectFolder, move: true));

        Assert.AreEqual("source bytes", File.ReadAllText(ExternalPayload(source, folder)));
        Assert.AreEqual("source bytes", File.ReadAllText(ExternalPayload(destination, folder)));
        if (folder) Assert.IsTrue(Directory.Exists(Path.Combine(destination, "empty")));
        Assert.IsNotNull(_tree.FindItem(destination));
        AssertPersistedTransfer(destination, folder);
        AssertNoTransferStaging();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RealWatcher_DeleteFailureAndRetry_DoNotRegisterStagingOrLoseRestoredItems(bool folder) {
        var state = PrepareManifestFailure(folder);
        using var manager = StartTransferWatcher();
        string trackedPath = Payload(state.Source, folder);
        manager.UpdateSnapshot(trackedPath);
        var document = manager.ProjectSystem.OpenDocuments.Get(trackedPath);
        var deleted = new System.Collections.Concurrent.ConcurrentQueue<string>();
        manager.Changed += change => { if (change.Type == ProjectChangeType.Deleted) deleted.Enqueue(change.Path); };
        using (var manifestLock = LockManifest()) _tree.Delete(state.Item);
        await WaitForTransferWatcherAsync(manager);
        AssertManifestFailureRestored(state);
        Assert.IsTrue(_design.GetManifestItems().Any(entry => entry.Path == Path.GetFileName(state.Source)));
        Assert.IsEmpty(deleted);
        Assert.AreSame(document, manager.ProjectSystem.OpenDocuments.Get(trackedPath));
        Assert.AreEqual(trackedPath, document!.Path);

        _tree.Delete(state.Item);
        await WaitForTransferWatcherAsync(manager);
        AssertDeleted(state);
        AssertNoStagingInManifest();
        Assert.Contains(state.Source, deleted);
        Assert.AreSame(document, manager.ProjectSystem.OpenDocuments.Get(trackedPath));
        Assert.AreEqual(trackedPath, document.Path);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RealWatcher_ExternalCopyFallbackFailure_KeepsOriginalAndRemovesUnregisteredCopy(bool folder) {
        string source = External(folder);
        string destination = Path.Combine(_design.ProjectFolder, Path.GetFileName(source));
        using var manager = StartTransferWatcher();
        using (var sourceLock = LockExternalSource(source, folder))
        using (var manifestLock = LockManifest())
            Assert.IsFalse(await _tree.TransferExternalAsync(source, _design.ProjectFolder, move: true));
        await WaitForTransferWatcherAsync(manager);

        Assert.AreEqual("source bytes", File.ReadAllText(ExternalPayload(source, folder)));
        Assert.IsFalse(Path.Exists(destination));
        Assert.IsNull(_tree.FindItem(destination));
        Assert.IsFalse(_design.GetManifestItems().Any(entry => entry.Path == Path.GetFileName(source)
            || entry.Path.StartsWith(Path.GetFileName(source) + "/", StringComparison.Ordinal)));
        AssertNoTransferStaging();
        AssertNoStagingInManifest();
    }

    private void AssertDeleted(ManifestFailureState state) {
        Assert.IsFalse(Path.Exists(state.Source));
        Assert.IsNull(_tree.FindItem(state.Source));
        Assert.IsFalse(_tree.HasCutItem);
        string relative = Path.GetRelativePath(_design.ProjectFolder, state.Source).Replace('\\', '/');
        Assert.IsFalse(WebDesignFileUtil.Create(_design.ProjectFilePath).GetManifestItems()
            .Any(entry => entry.Path == relative || entry.Path.StartsWith(relative + "/", StringComparison.Ordinal)));
        AssertNoTransferStaging();
    }

    private void AssertPersistedTransfer(string destination, bool folder) {
        string relative = Path.GetRelativePath(_design.ProjectFolder, destination).Replace('\\', '/');
        var persisted = WebDesignFileUtil.Create(_design.ProjectFilePath).GetManifestItems();
        Assert.IsTrue(persisted.Any(entry => entry.Path == relative));
        if (folder) {
            Assert.IsTrue(persisted.Any(entry => entry.Path == relative + "/nested/data.txt"));
            Assert.IsTrue(persisted.Any(entry => entry.Path == relative + "/empty"));
        }
        CollectionAssert.AreEqual(_design.GetManifestItems().ToArray(), persisted.ToArray());
    }

    private void AssertNoTransferStaging() => Assert.IsEmpty(Directory
        .GetFileSystemEntries(_design.ProjectFolder, "*", SearchOption.AllDirectories)
        .Where(path => Path.GetFileName(path).StartsWith(".vp-", StringComparison.Ordinal)
            || (Path.GetFileName(path).StartsWith('.') && path.EndsWith(".tmp", StringComparison.Ordinal))));

    private void AssertNoStagingInManifest() {
        var persisted = WebDesignFileUtil.Create(_design.ProjectFilePath).GetManifestItems();
        Assert.IsFalse(persisted.Any(entry => entry.Path.Contains(".vp-", StringComparison.Ordinal)));
        CollectionAssert.AreEqual(_design.GetManifestItems().ToArray(), persisted.ToArray());
    }

    private ProjectFileManager StartTransferWatcher() {
        var manager = new ProjectFileManager(_design.ProjectFolder,
            path => _design.AddManifestPath(path), _design.RemoveManifestPath, _design.RenameManifestPath,
            manifestSyncRoot: _design.ManifestSyncRoot);
        manager.Start();
        return manager;
    }

    private async Task WaitForTransferWatcherAsync(ProjectFileManager manager) {
        string marker = Path.Combine(_design.ProjectFolder, $"barrier-{Guid.NewGuid():N}.txt");
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(ProjectChangedEvent change) {
            if (change.Type == ProjectChangeType.Created && change.Path.Equals(marker, StringComparison.OrdinalIgnoreCase)) observed.TrySetResult();
        }
        manager.Changed += OnChanged;
        try {
            File.WriteAllText(marker, "watcher barrier");
            await observed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken);
        }
        finally { manager.Changed -= OnChanged; }
    }

    private static string ExternalPayload(string source, bool folder) => folder ? Path.Combine(source, "nested/data.txt") : source;
    private static FileStream LockExternalSource(string source, bool folder) =>
        new(ExternalPayload(source, folder), FileMode.Open, FileAccess.Read, FileShare.Read);
}
