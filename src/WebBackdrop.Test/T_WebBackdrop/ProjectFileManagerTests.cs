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
    public void IgnoreNextCreated_IsConsumedExactlyOnce() {
        string path = Path.Combine(_testRoot, "save-as.txt");
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
    public void DocumentHelpers_TrackDirtySaveAndCloseState() {
        string path = Path.Combine(_testRoot, "document.txt");
        File.WriteAllText(path, "first");
        _manager.UpdateSnapshot(path);

        _manager.SetDirty(path, true);
        Assert.IsTrue(_manager.ProjectSystem.Documents.Get(path)!.IsDirty);

        File.WriteAllText(path, "second");
        _manager.NotifySaved(path);
        Assert.IsFalse(_manager.ProjectSystem.Documents.Get(path)!.IsDirty);
        Assert.IsFalse(_manager.ProjectSystem.Documents.Get(path)!.IsDiskChanged());

        _manager.CloseDocument(path);
        Assert.IsFalse(_manager.ProjectSystem.Documents.IsOpen(path));
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
