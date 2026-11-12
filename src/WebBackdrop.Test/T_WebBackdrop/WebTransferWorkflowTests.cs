using VirtualPaper.Common;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.Models.SerializableData;
using Workloads.Creation.WebBackdrop.ViewModels;

namespace WebBackdrop.Test.T_WebBackdrop;

[TestClass]
[DoNotParallelize]
public class WebTransferWorkflowTests {
    private string _root = null!, _oldRoot = null!;
    private bool _oldMode;
    private WebDesignFileUtil _design = null!;
    private WebFileTreeViewModel _tree = null!;

    [TestInitialize]
    public void Initialize() {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VirtualPaper.WebTransfer-" + Guid.NewGuid().ToString("N"))).FullName;
        _oldRoot = Constants.CommonPaths.TestRootDir; _oldMode = Constants.IsTestMode;
        Constants.IsTestMode = true; Constants.CommonPaths.TestRootDir = Path.Combine(_root, "profile");
        _design = WebDesignFileUtil.Create(Path.Combine(_root, "Project")); _design.EnsureProjectStructure();
        _tree = new WebFileTreeViewModel(); _tree.Refresh(_design);
    }

    [TestCleanup]
    public void Cleanup() {
        _tree.CancelPendingSearch();
        Constants.CommonPaths.TestRootDir = _oldRoot; Constants.IsTestMode = _oldMode;
        Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task ExternalTransfer_PreservesExistingDestinationAndUpdatesDiskManifestAndTree(bool directory, bool move) {
        string source = External(directory);
        string existing = Path.Combine(_design.ProjectFolder, Path.GetFileName(source));
        if (directory) { Directory.CreateDirectory(existing); File.WriteAllText(Path.Combine(existing, "keep.txt"), "existing user file"); }
        else File.WriteAllText(existing, "existing user file");
        _design.AddManifestPathRecursive(existing); _tree.Refresh(_design);
        var before = Directory.GetFileSystemEntries(_design.ProjectFolder);
        Assert.IsTrue(await _tree.TransferExternalAsync(source, _design.ProjectFolder, move));
        string created = Directory.GetFileSystemEntries(_design.ProjectFolder).Except(before, StringComparer.OrdinalIgnoreCase).Single();
        Assert.AreEqual("existing user file", File.ReadAllText(directory ? Path.Combine(existing, "keep.txt") : existing));
        Assert.AreEqual("source bytes", File.ReadAllText(directory ? Path.Combine(created, "nested/data.txt") : created));
        Assert.AreEqual(!move, directory ? Directory.Exists(source) : File.Exists(source));
        if (directory) Assert.IsTrue(Directory.Exists(Path.Combine(created, "empty")));
        Assert.IsNotNull(_tree.FindItem(created));
        string relative = Path.GetRelativePath(_design.ProjectFolder, created).Replace('\\', '/');
        Assert.IsTrue(_design.GetManifestItems().Any(item => item.Path == relative));
        var reopened = WebDesignFileUtil.Create(_design.ProjectFilePath);
        Assert.IsTrue(reopened.GetManifestItems().Any(item => item.Path == relative));
        if (directory) Assert.IsTrue(reopened.GetManifestItems().Any(item => item.Path == relative + "/nested/data.txt"));
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task DirectoryTransfer_RejectsItsOwnDirectoryOrDescendant(bool move, bool nested) {
        string target = _design.ProjectFolder;
        if (nested) {
            target = Directory.CreateDirectory(Path.Combine(target, "..assets")).FullName;
            _design.AddManifestPath(target);
            _tree.Refresh(_design);
        }
        var before = Directory.GetFileSystemEntries(_design.ProjectFolder, "*", SearchOption.AllDirectories);
        byte[] manifest = File.ReadAllBytes(_design.ProjectFilePath);
        Assert.IsFalse(await _tree.TransferExternalAsync(_design.ProjectFolder, target, move));
        CollectionAssert.AreEquivalent(before, Directory.GetFileSystemEntries(_design.ProjectFolder, "*", SearchOption.AllDirectories));
        CollectionAssert.AreEqual(manifest, File.ReadAllBytes(_design.ProjectFilePath));
    }

    [TestMethod]
    public async Task PartialDirectoryCopyFailure_RemovesStagingAndKeepsSourceManifestAndTree() {
        string source = External(directory: true);
        File.WriteAllText(Path.Combine(source, "first.txt"), "copied before the nested failure");
        var before = Directory.GetFileSystemEntries(_design.ProjectFolder);
        byte[] manifest = File.ReadAllBytes(_design.ProjectFilePath);
        using (var locked = new FileStream(Path.Combine(source, "nested/data.txt"), FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.IsFalse(await _tree.TransferExternalAsync(source, _design.ProjectFolder, move: false));
        CollectionAssert.AreEquivalent(before, Directory.GetFileSystemEntries(_design.ProjectFolder));
        CollectionAssert.AreEqual(manifest, File.ReadAllBytes(_design.ProjectFilePath));
        Assert.AreEqual("source bytes", File.ReadAllText(Path.Combine(source, "nested/data.txt")));
        Assert.AreEqual("copied before the nested failure", File.ReadAllText(Path.Combine(source, "first.txt")));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LockedSourceFile_FailedTransferKeepsOriginalAndDoesNotPublishAFile(bool move) {
        string source = External(directory: false);
        var before = Directory.GetFileSystemEntries(_design.ProjectFolder);
        using (var locked = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.IsFalse(await _tree.TransferExternalAsync(source, _design.ProjectFolder, move));
        Assert.AreEqual("source bytes", File.ReadAllText(source));
        CollectionAssert.AreEquivalent(before, Directory.GetFileSystemEntries(_design.ProjectFolder));
    }

    [TestMethod]
    public void ManifestSync_UpdatesInventoryWithoutReplacingUnchangedNodes() {
        string old = Path.Combine(_design.ProjectFolder, "old.txt");
        File.WriteAllText(old, "old"); _design.AddManifestPath(old); _tree.Refresh(_design);
        var root = _tree.FileItems.Single();
        var entry = _tree.FindItem(_design.EntryFilePath);
        string added = Path.Combine(_design.ProjectFolder, "added.css"); File.WriteAllText(added, "body{}");
        _design.AddManifestPath(added); _design.RemoveManifestPath(old);
        _tree.SyncManifest(_design);
        Assert.AreSame(root, _tree.FileItems.Single());
        Assert.AreSame(entry, _tree.FindItem(_design.EntryFilePath));
        Assert.IsNull(_tree.FindItem(old));
        Assert.IsNotNull(_tree.FindItem(added));
        Assert.IsTrue(File.Exists(old), "Removing a manifest entry must not delete its file.");
    }

    [TestMethod]
    public void InternalMove_KeepsNodeIdentityAndRebindsManifestAndDiskPaths() {
        string folder = Directory.CreateDirectory(Path.Combine(_design.ProjectFolder, "target")).FullName;
        string source = Path.Combine(_design.ProjectFolder, "moving.txt"); File.WriteAllText(source, "original");
        _design.AddManifestPath(folder); _design.AddManifestPath(source); _tree.Refresh(_design);
        var item = _tree.FindItem(source)!; var parent = _tree.FindItem(folder)!;
        Assert.HasCount(1, _tree.MoveItems([item], parent));
        string target = Path.Combine(folder, "moving.txt");
        Assert.AreSame(item, _tree.FindItem(target)); Assert.AreSame(parent, item.Parent);
        Assert.IsFalse(File.Exists(source)); Assert.AreEqual("original", File.ReadAllText(target));
        var paths = WebDesignFileUtil.Create(_design.ProjectFilePath).GetManifestItems().Select(entry => entry.Path).ToArray();
        Assert.Contains("target/moving.txt", paths); Assert.DoesNotContain("moving.txt", paths);
    }

    [TestMethod]
    public async Task LockedRestore_KeepsBackupAndOriginalAndCanRetryWithoutStagingResidue() {
        string original = _design.EntryFilePath;
        byte[] before = File.ReadAllBytes(original);
        await WebBackdropRecoveryStore.WriteBackupAsync(_design.ProjectFolder, original, "recovered content", "UTF-8 BOM");
        string backup = WebBackdropRecoveryStore.GetBackupPath(_design.ProjectFolder, original);
        byte[] recovery = File.ReadAllBytes(backup);
        using (var locked = new FileStream(original, FileMode.Open, FileAccess.Read, FileShare.Read))
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => WebBackdropRecoveryStore.RestoreAsync(_design.ProjectFolder, backup));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(original));
        CollectionAssert.AreEqual(recovery, File.ReadAllBytes(backup));
        Assert.IsEmpty(Directory.GetFiles(_design.ProjectFolder, ".*.tmp"));
        await WebBackdropRecoveryStore.RestoreAsync(_design.ProjectFolder, backup);
        CollectionAssert.AreEqual(recovery, File.ReadAllBytes(original));
        CollectionAssert.AreEqual(recovery, File.ReadAllBytes(backup));
    }

    private string External(bool directory) {
        string outside = Directory.CreateDirectory(Path.Combine(_root, "External")).FullName;
        if (!directory) { string file = Path.Combine(outside, "data.txt"); File.WriteAllText(file, "source bytes"); return file; }
        string folder = Directory.CreateDirectory(Path.Combine(outside, "assets")).FullName;
        Directory.CreateDirectory(Path.Combine(folder, "nested")); Directory.CreateDirectory(Path.Combine(folder, "empty"));
        File.WriteAllText(Path.Combine(folder, "nested/data.txt"), "source bytes"); return folder;
    }
}
