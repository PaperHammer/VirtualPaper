using VirtualPaper.Common.Utils.ProjectSystem.Events;
using Workloads.Creation.WebBackdrop.Core.Utils;

namespace WebBackdrop.Test.T_WebBackdrop;

[TestClass]
public class ProjectFileManagerTests {
    [TestInitialize]
    public void Initialize() {
        _testRoot = Path.Combine(
            Path.GetTempPath(),
            "VirtualPaper.Tests",
            $"ProjectFiles-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
        _manager = new ProjectFileManager(
            _testRoot,
            path => _added.Add(path),
            path => _removed.Add(path),
            (oldPath, newPath) => _renamed.Add((oldPath, newPath)));
        _manager.Changed += change => _changes.Add(change);
    }

    [TestCleanup]
    public void Cleanup() {
        _manager.Dispose();
        if (Directory.Exists(_testRoot)) Directory.Delete(_testRoot, recursive: true);
    }

    [TestMethod]
    public void Created_AddsManifestTracksExternalChangeAndRaisesEvent() {
        string path = Path.Combine(_testRoot, "created.txt");
        File.WriteAllText(path, "created");

        Raise(ProjectChangeType.Created, path);

        CollectionAssert.AreEqual(new[] { path }, _added);
        Assert.HasCount(1, _changes);
        Assert.IsTrue(_manager.TryConsumeExternalChange(path, out FileChangeType type));
        Assert.AreEqual(FileChangeType.Created, type);
        Assert.IsFalse(_manager.TryConsumeExternalChange(path, out _));
    }

    [TestMethod]
    public void TransientAndDebugArtifacts_AreIgnoredCompletely() {
        Raise(ProjectChangeType.Created, Path.Combine(_testRoot, ".project.vpw.123.tmp"));
        Raise(ProjectChangeType.Created, Path.Combine(_testRoot, "wp_metadata_basic.json"));

        Assert.IsEmpty(_added);
        Assert.IsEmpty(_removed);
        Assert.IsEmpty(_renamed);
        Assert.IsEmpty(_changes);
    }

    [TestMethod]
    [DataRow(false, ".vp-import-")]
    [DataRow(true, ".vp-import-")]
    [DataRow(false, ".vp-delete-")]
    [DataRow(true, ".vp-delete-")]
    public void DirectoryStaging_IgnoresRootChildrenAndPublishRename(bool nested, string prefix) {
        string staging = Path.Combine(_testRoot, prefix + Guid.NewGuid().ToString("N"));
        string path = nested ? Path.Combine(staging, "nested/app.js") : staging;
        string published = Path.Combine(_testRoot, "assets");
        Raise(ProjectChangeType.Created, path);
        Raise(ProjectChangeType.Modified, path);
        Raise(ProjectChangeType.Deleted, path);
        _manager.OnProjectChanged(new ProjectChangedEvent { Type = ProjectChangeType.Renamed, Path = published, OldPath = path });

        Assert.IsEmpty(_added);
        Assert.IsEmpty(_removed);
        Assert.IsEmpty(_renamed);
        Assert.IsEmpty(_changes);
        Assert.IsFalse(_manager.TryConsumeExternalChange(path, out _));
        Assert.IsFalse(_manager.TryConsumeExternalChange(published, out _));

        // Completed copies are explicitly registered by their owner; ordinary later edits remain visible.
        string existingFile = Path.Combine(published, "app.js");
        Directory.CreateDirectory(published);
        File.WriteAllText(existingFile, "published");
        Raise(ProjectChangeType.Created, existingFile);
        CollectionAssert.AreEqual(new[] { existingFile }, _added);
        Assert.HasCount(1, _changes);
    }

    [TestMethod]
    [DataRow(".vp-import-assets")]
    [DataRow(".vp-import-0123456789abcdef0123456789abcde")]
    [DataRow("vp-import-0123456789abcdef0123456789abcdef")]
    [DataRow(".vp-delete-assets")]
    [DataRow(".vp-delete-0123456789abcdef0123456789abcde")]
    [DataRow("vp-delete-0123456789abcdef0123456789abcdef")]
    public void DirectoryStaging_DoesNotIgnoreSimilarUserDirectoryNames(string folder) {
        string path = Path.Combine(_testRoot, folder, "app.js");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "user content");
        Raise(ProjectChangeType.Created, path);

        CollectionAssert.AreEqual(new[] { path }, _added);
        Assert.HasCount(1, _changes);
        Assert.IsTrue(_manager.TryConsumeExternalChange(path, out FileChangeType type));
        Assert.AreEqual(FileChangeType.Created, type);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void StagedDelete_NotifiesOriginalPathOnlyAfterCommit(bool folder, bool restored) {
        string original = Path.Combine(_testRoot, folder ? "assets" : "app.js");
        if (folder) Directory.CreateDirectory(original);
        else File.WriteAllText(original, "user bytes");
        string staging = Path.Combine(_testRoot, ".vp-delete-" + Guid.NewGuid().ToString("N"));
        if (!restored) {
            if (folder) Directory.Move(original, staging);
            else File.Move(original, staging);
        }

        _manager.OnProjectChanged(new ProjectChangedEvent { Type = ProjectChangeType.Renamed, Path = staging, OldPath = original });

        Assert.IsEmpty(_added);
        Assert.IsEmpty(_renamed);
        if (restored) {
            Assert.IsEmpty(_removed);
            Assert.IsEmpty(_changes);
            Assert.IsFalse(_manager.TryConsumeExternalChange(original, out _));
        }
        else {
            CollectionAssert.AreEqual(new[] { original }, _removed);
            Assert.HasCount(1, _changes);
            Assert.AreEqual(ProjectChangeType.Deleted, _changes[0].Type);
            Assert.AreEqual(original, _changes[0].Path);
            Assert.IsTrue(_manager.TryConsumeExternalChange(original, out var type));
            Assert.AreEqual(FileChangeType.Deleted, type);
        }
        Assert.IsFalse(_manager.TryConsumeExternalChange(staging, out _));
    }

    [TestMethod]
    public void IgnoreNextCreated_IsConsumedExactlyOnce() {
        string path = Path.Combine(_testRoot, "save-as.txt");
        File.WriteAllText(path, "saved");
        _manager.IgnoreNextCreated(path);

        Raise(ProjectChangeType.Created, path);
        Raise(ProjectChangeType.Created, path);

        CollectionAssert.AreEqual(new[] { path }, _added);
        Assert.HasCount(1, _changes);
    }

    [TestMethod]
    public void DeletedExistingFile_IsTreatedAsAtomicReplacement() {
        string path = Path.Combine(_testRoot, "still-there.txt");
        File.WriteAllText(path, "content");

        Raise(ProjectChangeType.Deleted, path);

        Assert.IsEmpty(_removed);
        Assert.IsEmpty(_changes);

        File.Delete(path);
        Raise(ProjectChangeType.Deleted, path);

        CollectionAssert.AreEqual(new[] { path }, _removed);
        Assert.IsTrue(_manager.TryConsumeExternalChange(path, out FileChangeType type));
        Assert.AreEqual(FileChangeType.Deleted, type);
    }

    [TestMethod]
    public void Renamed_UpdatesManifestAndTracksDestination() {
        string oldPath = Path.Combine(_testRoot, "old.txt");
        string newPath = Path.Combine(_testRoot, "new.txt");
        File.WriteAllText(newPath, "renamed");

        _manager.OnProjectChanged(new ProjectChangedEvent {
            Type = ProjectChangeType.Renamed,
            OldPath = oldPath,
            Path = newPath,
        });

        CollectionAssert.AreEqual(new[] { (oldPath, newPath) }, _renamed);
        Assert.IsTrue(_manager.TryConsumeExternalChange(newPath, out FileChangeType type));
        Assert.AreEqual(FileChangeType.Renamed, type);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RolledBackChange_IgnoresLateCreateDeleteAndRenameEvents(bool folder) {
        string original = Path.Combine(_testRoot, "original"), destination = Path.Combine(_testRoot, "destination");
        if (folder) Directory.CreateDirectory(original);
        else File.WriteAllText(original, "restored bytes");

        Raise(ProjectChangeType.Created, destination);
        Raise(ProjectChangeType.Deleted, original);
        _manager.OnProjectChanged(new ProjectChangedEvent { Type = ProjectChangeType.Renamed, OldPath = original, Path = destination });

        Assert.IsEmpty(_added);
        Assert.IsEmpty(_removed);
        Assert.IsEmpty(_renamed);
        Assert.IsEmpty(_changes);
        Assert.IsFalse(_manager.TryConsumeExternalChange(original, out _));
        Assert.IsFalse(_manager.TryConsumeExternalChange(destination, out _));
        if (folder) Directory.Move(original, destination);
        else File.Move(original, destination);
        _manager.OnProjectChanged(new ProjectChangedEvent { Type = ProjectChangeType.Renamed, OldPath = original, Path = destination });
        CollectionAssert.AreEqual(new[] { (original, destination) }, _renamed);
        Assert.HasCount(1, _changes);
    }

    [TestMethod]
    public async Task RepeatedModifiedEvents_AreDebouncedAndLatestEventWins() {
        string path = Path.Combine(_testRoot, "index.html");

        Raise(ProjectChangeType.Modified, path);
        Raise(ProjectChangeType.Reloaded, path);
        Raise(ProjectChangeType.Conflict, path);

        await WaitUntilAsync(() => _changes.Count == 1);

        Assert.HasCount(1, _changes);
        Assert.AreEqual(ProjectChangeType.Conflict, _changes[0].Type);
        Assert.IsTrue(_manager.TryConsumeExternalChange(path, out FileChangeType type));
        Assert.AreEqual(FileChangeType.Changed, type);
    }

    [TestMethod]
    public async Task Dispose_DropsPendingAndLateEventsBeforeProjectCleanup() {
        string path = Path.Combine(_testRoot, "pending.txt");
        Raise(ProjectChangeType.Modified, path);

        _manager.Dispose();
        Raise(ProjectChangeType.Created, path);
        await Task.Delay(250);

        Assert.IsEmpty(_changes);
        Assert.IsEmpty(_added);
        Assert.IsFalse(_manager.TryConsumeExternalChange(path, out _));
    }

    [TestMethod]
    public void DocumentHelpers_TrackDirtySaveAndCloseState() {
        string path = Path.Combine(_testRoot, "document.txt");
        File.WriteAllText(path, "first");
        _manager.UpdateSnapshot(path);

        _manager.SetDirty(path, true);
        Assert.IsTrue(_manager.ProjectSystem.OpenDocuments.Get(path)!.IsDirty);

        File.WriteAllText(path, "second");
        _manager.NotifySaved(path);
        Assert.IsFalse(_manager.ProjectSystem.OpenDocuments.Get(path)!.IsDirty);
        Assert.IsFalse(_manager.ProjectSystem.OpenDocuments.Get(path)!.IsDiskChanged());

        _manager.CloseDocument(path);
        Assert.IsFalse(_manager.ProjectSystem.OpenDocuments.IsOpen(path));
    }

    private void Raise(ProjectChangeType type, string path) =>
        _manager.OnProjectChanged(new ProjectChangedEvent { Type = type, Path = path });

    private static async Task WaitUntilAsync(Func<bool> condition) {
        for (var attempt = 0; attempt < 150; attempt++) {
            if (condition()) return;
            await Task.Delay(20);
        }
        Assert.Fail("Expected asynchronous project change was not observed.");
    }

    private string _testRoot = null!;
    private ProjectFileManager _manager = null!;
    private readonly List<string> _added = [];
    private readonly List<string> _removed = [];
    private readonly List<(string OldPath, string NewPath)> _renamed = [];
    private readonly List<ProjectChangedEvent> _changes = [];
}
