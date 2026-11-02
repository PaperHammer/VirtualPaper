using VirtualPaper.Common.Utils.ProjectSystem;
using VirtualPaper.Common.Utils.ProjectSystem.Documents;
using VirtualPaper.Common.Utils.ProjectSystem.Events;
using VirtualPaper.Common.Utils.ProjectSystem.FileSystem;
using VirtualPaper.Common.Utils.ProjectSystem.Project;

namespace VirtualPaper.Core.Test.T_Common;

[TestClass]
public class ProjectSystemTests {
    [TestInitialize]
    public void Initialize() {
        _testRoot = Path.Combine(
            Path.GetTempPath(),
            "VirtualPaper.Tests",
            $"ProjectSystem-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    [TestCleanup]
    public void Cleanup() {
        if (Directory.Exists(_testRoot)) Directory.Delete(_testRoot, recursive: true);
    }

    [TestMethod]
    public void ProjectTree_LoadsAddsRemovesAndFindsCaseInsensitively() {
        string folderPath = Directory.CreateDirectory(Path.Combine(_testRoot, "Assets")).FullName;
        string initialPath = Path.Combine(folderPath, "logo.txt");
        File.WriteAllText(initialPath, "logo");
        var tree = new ProjectTree(_testRoot);

        Assert.IsInstanceOfType<ProjectFolder>(tree.Find(folderPath.ToUpperInvariant()));
        Assert.IsInstanceOfType<ProjectFile>(tree.Find(initialPath));

        string addedPath = Path.Combine(folderPath, "added.txt");
        File.WriteAllText(addedPath, "added");
        tree.Add(addedPath);
        Assert.IsInstanceOfType<ProjectFile>(tree.Find(addedPath));

        tree.Remove(addedPath);
        Assert.IsNull(tree.Find(addedPath));
    }

    [TestMethod]
    public void ProjectTree_RenameFolderReparentsAndRebindsDescendants() {
        string left = Directory.CreateDirectory(Path.Combine(_testRoot, "left")).FullName;
        string right = Directory.CreateDirectory(Path.Combine(_testRoot, "right")).FullName;
        string oldFolder = Directory.CreateDirectory(Path.Combine(left, "feature")).FullName;
        string oldChild = Path.Combine(oldFolder, "index.html");
        File.WriteAllText(oldChild, "html");
        var tree = new ProjectTree(_testRoot);
        string newFolder = Path.Combine(right, "feature");
        Directory.Move(oldFolder, newFolder);

        tree.Rename(oldFolder, newFolder);

        string newChild = Path.Combine(newFolder, "index.html");
        ProjectFolder folder = (ProjectFolder)tree.Find(newFolder)!;
        Assert.IsNull(tree.Find(oldFolder));
        Assert.IsNotNull(tree.Find(newChild));
        Assert.AreEqual(right, folder.Parent!.FullPath);
        Assert.AreEqual(newFolder, tree.Find(newChild)!.Parent!.FullPath);
    }

    [TestMethod]
    public void Document_SaveReloadAndRefreshMaintainDiskStampAndDirtyState() {
        string path = Path.Combine(_testRoot, "document.txt");
        File.WriteAllText(path, "one");
        var document = new Document(path) { Text = "two", IsDirty = true };

        document.Save();

        Assert.AreEqual("two", File.ReadAllText(path));
        Assert.IsFalse(document.IsDirty);
        Assert.IsFalse(document.IsDiskChanged());

        File.WriteAllText(path, "three-longer");
        Assert.IsTrue(document.IsDiskChanged());
        document.ReloadFromDisk();
        Assert.AreEqual("three-longer", document.Text);
        Assert.IsFalse(document.IsDirty);
    }

    [TestMethod]
    public void DocumentManager_OpenIsIdempotentAndRenamePreservesInstance() {
        string oldPath = Path.Combine(_testRoot, "old.txt");
        string newPath = Path.Combine(_testRoot, "new.txt");
        File.WriteAllText(oldPath, "text");
        var manager = new DocumentManager();
        Document first = manager.Open(oldPath);

        Assert.AreSame(first, manager.Open(oldPath));
        File.Move(oldPath, newPath);
        manager.Rename(oldPath, newPath);

        Assert.IsFalse(manager.IsOpen(oldPath));
        Assert.AreSame(first, manager.Get(newPath));
        Assert.AreEqual(newPath, first.Path);
        manager.Close(newPath);
        Assert.IsNull(manager.Get(newPath));
    }

    [TestMethod]
    public void ProjectSystem_ModifiedUnopenedFileRaisesModified() {
        string path = Path.Combine(_testRoot, "asset.txt");
        File.WriteAllText(path, "asset");
        var manager = new ProjectSystemManager(_testRoot);
        ProjectChangedEvent? change = null;
        manager.Changed += value => change = value;

        manager.OnModified(path);

        Assert.IsNotNull(change);
        Assert.AreEqual(ProjectChangeType.Modified, change.Type);
        Assert.AreEqual(path, change.Path);
    }

    [TestMethod]
    public void ProjectSystem_ModifiedCleanDocumentReloadsFromDisk() {
        string path = Path.Combine(_testRoot, "open.txt");
        File.WriteAllText(path, "old");
        var manager = new ProjectSystemManager(_testRoot);
        Document document = manager.Documents.Open(path);
        ProjectChangedEvent? change = null;
        manager.Changed += value => change = value;
        File.WriteAllText(path, "new-content");

        manager.OnModified(path);

        Assert.AreEqual(ProjectChangeType.Reloaded, change!.Type);
        Assert.AreEqual("new-content", document.Text);
        Assert.IsFalse(document.IsDirty);
    }

    [TestMethod]
    public void ProjectSystem_ModifiedDirtyDocumentRaisesConflictWithoutReloading() {
        string path = Path.Combine(_testRoot, "open.txt");
        File.WriteAllText(path, "disk-old");
        var manager = new ProjectSystemManager(_testRoot);
        Document document = manager.Documents.Open(path);
        document.Text = "editor-change";
        document.IsDirty = true;
        ProjectChangedEvent? change = null;
        manager.Changed += value => change = value;
        File.WriteAllText(path, "disk-new-and-longer");

        manager.OnModified(path);

        Assert.AreEqual(ProjectChangeType.Conflict, change!.Type);
        Assert.AreEqual("editor-change", document.Text);
        Assert.IsTrue(document.IsDirty);
    }

    [TestMethod]
    public void ProjectSystem_RenameUpdatesTreeOpenDocumentAndEvent() {
        string oldPath = Path.Combine(_testRoot, "old.txt");
        string newPath = Path.Combine(_testRoot, "new.txt");
        File.WriteAllText(oldPath, "text");
        var manager = new ProjectSystemManager(_testRoot);
        Document document = manager.Documents.Open(oldPath);
        ProjectChangedEvent? change = null;
        manager.Changed += value => change = value;
        File.Move(oldPath, newPath);

        manager.OnRenamed(oldPath, newPath);

        Assert.IsNull(manager.Tree.Find(oldPath));
        Assert.IsNotNull(manager.Tree.Find(newPath));
        Assert.AreSame(document, manager.Documents.Get(newPath));
        Assert.AreEqual(ProjectChangeType.Renamed, change!.Type);
        Assert.AreEqual(oldPath, change.OldPath);
    }

    [TestMethod]
    public void ProjectSystem_LateModifiedAfterDeleteDoesNotReloadOrNotify() {
        string path = Path.Combine(_testRoot, "deleted.txt");
        File.WriteAllText(path, "keep in memory");
        using var manager = new ProjectSystemManager(_testRoot);
        Document document = manager.Documents.Open(path);
        var changes = new List<ProjectChangedEvent>();
        manager.Changed += changes.Add;
        File.Delete(path);

        manager.OnModified(path);

        Assert.AreEqual("keep in memory", document.Text);
        Assert.IsEmpty(changes);
    }

    [TestMethod]
    public async Task ProjectWatcher_DisposeWaitsForCallbackAndRejectsFurtherEvents() {
        using var watcher = new ProjectWatcher(_testRoot);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int callbacks = 0;
        watcher.Created += _ => {
            Interlocked.Increment(ref callbacks);
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Test callback was not released");
        };
        watcher.Start();
        Task? disposing = null;
        try {
            File.WriteAllText(Path.Combine(_testRoot, "before.txt"), "content");
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)), "Watcher callback did not start");
            var disposeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            disposing = Task.Run(() => { disposeStarted.SetResult(); watcher.Dispose(); });
            await disposeStarted.Task;
            await Task.Delay(100);
            Assert.IsFalse(disposing.IsCompleted, "Dispose returned while a callback was active");
        }
        finally {
            release.Set();
            if (disposing != null) await disposing.WaitAsync(TimeSpan.FromSeconds(5));
        }
        File.WriteAllText(Path.Combine(_testRoot, "after.txt"), "content");
        await Task.Delay(200);
        Assert.AreEqual(1, callbacks);
    }

    private string _testRoot = null!;
}
