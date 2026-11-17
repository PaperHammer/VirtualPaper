using System.Text;
using Moq;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.ViewModels;

namespace WebBackdrop.Test.T_WebBackdrop;

[TestClass]
public partial class WebEditorViewModelTests {
    [TestInitialize]
    public void Initialize() {
        _testRoot = Path.Combine(
            Path.GetTempPath(),
            "VirtualPaper.Tests",
            $"WebEditorVm-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
        File.WriteAllText(Path.Combine(_testRoot, "index.html"), "index");
        _session = new WebProjectSession(_testRoot);
        _userSettings = new Mock<IUserSettingsClient>();
        _wallpaperControl = new Mock<IWallpaperControlClient>();
        _viewModel = new WebEditorViewModel(
            _session,
            new ArcPageContextKey(typeof(WebEditorViewModel)),
            _userSettings.Object,
            _wallpaperControl.Object);
    }

    [TestCleanup]
    public void Cleanup() {
        _session.Dispose();
        if (Directory.Exists(_testRoot)) Directory.Delete(_testRoot, recursive: true);
    }

    [TestMethod]
    public async Task OpenFileAsync_CachesInstanceAndTracksTextDocument() {
        string path = Path.Combine(_testRoot, "script.js");
        File.WriteAllText(path, "one");

        await _viewModel.OpenFileAsync(path);
        WebEditorFile first = _viewModel.ActiveFile!;
        first.Content = "in-memory";
        await _viewModel.OpenFileAsync(path.ToUpperInvariant());

        Assert.AreSame(first, _viewModel.ActiveFile);
        Assert.AreEqual("in-memory", _viewModel.ActiveFile!.Content);
        Assert.HasCount(1, _viewModel.CachedFiles);
        Assert.IsTrue(_session.FileManager.ProjectSystem.OpenDocuments.IsOpen(path));
    }

    [TestMethod]
    public async Task SaveActiveFile_WritesSelectedEncodingAndMarksSaved() {
        string path = Path.Combine(_testRoot, "utf16.txt");
        File.WriteAllText(path, "old");
        await _viewModel.OpenFileAsync(path);
        _viewModel.ActiveFile!.Content = "你好";
        _viewModel.ActiveFile.SetEncoding("UTF-16 LE");
        _viewModel.ActiveFile.SetSavedState(false);

        bool saved = await _viewModel.SaveActiveFileAsync();

        Assert.IsTrue(saved);
        Assert.IsTrue(_viewModel.ActiveFile.IsSaved);
        byte[] bytes = File.ReadAllBytes(path);
        CollectionAssert.AreEqual(Encoding.Unicode.Preamble.ToArray(), bytes[..2]);
        Assert.AreEqual("你好", Encoding.Unicode.GetString(bytes[2..]));
    }

    [TestMethod]
    public async Task SaveFileAs_WritesCopyWithoutRebindingOriginal() {
        string path = Path.Combine(_testRoot, "source.txt");
        string copyPath = Path.Combine(_testRoot, "copy.txt");
        File.WriteAllText(path, "old");
        await _viewModel.OpenFileAsync(path);
        WebEditorFile file = _viewModel.ActiveFile!;
        file.Content = "copy-content";
        file.SetSavedState(false);

        bool saved = await _viewModel.SaveFileAsAsync(file, copyPath);

        Assert.IsTrue(saved);
        Assert.AreEqual("copy-content", File.ReadAllText(copyPath));
        Assert.AreEqual("old", File.ReadAllText(path));
        Assert.AreEqual(path, file.FilePath);
        Assert.AreSame(file, _viewModel.ActiveFile);
        Assert.AreEqual("copy-content", file.Content);
        Assert.IsFalse(file.IsSaved);
        Assert.IsFalse(_viewModel.IsAllSaved);
    }

    [TestMethod]
    public async Task SaveAll_WhenSomeFilesFail_PreservesTheirOriginalsAndStillSavesOtherFiles() {
        var files = new List<WebEditorFile>();
        foreach (string name in new[] { "locked.txt", "load-failed.txt", "valid.txt" }) {
            string path = Path.Combine(_testRoot, name);
            File.WriteAllText(path, "original");
            await _viewModel.OpenFileAsync(path);
            var file = _viewModel.ActiveFile!;
            file.Content = "edited " + name;
            file.SetSavedState(false);
            files.Add(file);
        }
        files[1].SetLoadFailed();
        using var fileLock = new FileStream(files[0].FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        Assert.IsFalse(await _viewModel.SaveAllAsync());

        Assert.AreEqual("original", File.ReadAllText(files[0].FilePath));
        Assert.AreEqual("original", File.ReadAllText(files[1].FilePath));
        Assert.IsFalse(files[0].IsSaved);
        Assert.IsTrue(files[1].IsLoadFailed);
        Assert.IsFalse(files[1].IsSaved);
        Assert.AreEqual("edited valid.txt", File.ReadAllText(files[2].FilePath));
        Assert.IsTrue(files[2].IsSaved);
        Assert.IsFalse(_viewModel.IsAllSaved);
    }

    [TestMethod]
    public async Task BinaryFileCannotBeSavedAsText() {
        string path = Path.Combine(_testRoot, "image.png");
        File.WriteAllBytes(path, [1, 2, 3]);
        await _viewModel.OpenFileAsync(path);

        bool saved = await _viewModel.SaveActiveFileAsync();

        Assert.IsFalse(saved);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
        Assert.IsFalse(_session.FileManager.ProjectSystem.OpenDocuments.IsOpen(path));
    }

    [TestMethod]
    public async Task RebindAndCloseOpenFile_PreserveInstanceThenEvictIt() {
        string oldPath = Path.Combine(_testRoot, "old.txt");
        string newPath = Path.Combine(_testRoot, "new.txt");
        File.WriteAllText(oldPath, "text");
        await _viewModel.OpenFileAsync(oldPath);
        WebEditorFile file = _viewModel.ActiveFile!;
        File.Move(oldPath, newPath);

        WebEditorFile? rebound = _viewModel.RebindOpenFilePath(oldPath, newPath);

        Assert.AreSame(file, rebound);
        Assert.AreSame(file, _viewModel.GetOpenFile(newPath));
        Assert.IsNull(_viewModel.GetOpenFile(oldPath));
        Assert.IsTrue(_session.FileManager.ProjectSystem.OpenDocuments.IsOpen(newPath));

        _viewModel.CloseOpenFile(newPath);
        Assert.IsNull(_viewModel.ActiveFile);
        Assert.IsEmpty(_viewModel.CachedFiles);
        Assert.IsFalse(_session.FileManager.ProjectSystem.OpenDocuments.IsOpen(newPath));
    }

    [TestMethod]
    public async Task OpeningBeyondCacheLimit_EvictsOldestSavedInactiveFilesAndRestoresThemOnReopen() {
        var evicted = new List<string>();
        _viewModel.FileCacheEvicted += evicted.Add;

        for (var index = 0; index < 34; index++) {
            string path = Path.Combine(_testRoot, $"file-{index:D2}.txt");
            File.WriteAllText(path, index.ToString());
            await _viewModel.OpenFileAsync(path);
        }

        Assert.HasCount(32, _viewModel.CachedFiles);
        CollectionAssert.AreEqual(
            new[] {
                Path.Combine(_testRoot, "file-00.txt"),
                Path.Combine(_testRoot, "file-01.txt"),
            },
            evicted);
        Assert.AreEqual("file-33.txt", _viewModel.ActiveFile!.FileName);

        string firstPath = Path.Combine(_testRoot, "file-00.txt");
        Assert.IsNull(_viewModel.GetOpenFile(firstPath));
        Assert.IsFalse(_session.FileManager.ProjectSystem.OpenDocuments.IsOpen(firstPath));
        File.WriteAllText(firstPath, "updated on disk");

        await _viewModel.OpenFileAsync(firstPath);

        Assert.AreEqual(firstPath, _viewModel.ActiveFile!.FilePath);
        Assert.AreEqual("updated on disk", _viewModel.ActiveFile.Content);
        Assert.AreSame(_viewModel.GetOpenFile(firstPath), _viewModel.ActiveFile);
        Assert.IsTrue(_viewModel.ActiveFile.IsSaved);
        Assert.IsTrue(_session.FileManager.ProjectSystem.OpenDocuments.IsOpen(firstPath));
        Assert.HasCount(32, _viewModel.CachedFiles);
    }

    [TestMethod]
    public async Task UpdateRecentUsed_OnlyForwardsNonEmptyPaths() {
        await _viewModel.UpdateRecentUsedAsync(string.Empty);
        await _viewModel.UpdateRecentUsedAsync("project.vpw");

        _userSettings.Verify(client => client.UpdateRecentUsedAsync("project.vpw"), Times.Once);
        _userSettings.Verify(client => client.UpdateRecentUsedAsync(string.Empty), Times.Never);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CloseDeletedFiles_DirectoryClosesSavedDescendantsAndPreservesDirtyBuffers(bool activeSaved) {
        _session.FileManager.Stop();
        string folder = Directory.CreateDirectory(Path.Combine(_testRoot, "assets")).FullName;
        Directory.CreateDirectory(Path.Combine(folder, "nested"));
        string saved = Path.Combine(folder, "saved.css");
        string nested = Path.Combine(folder, "nested/saved.js");
        string dirty = Path.Combine(folder, "draft.js");
        string unrelated = Path.Combine(Directory.CreateDirectory(folder + "-backup").FullName, "saved.css");
        foreach (string path in new[] { saved, nested, dirty, unrelated }) {
            File.WriteAllText(path, "original");
            await _viewModel.OpenFileAsync(path);
        }
        var draft = _viewModel.GetOpenFile(dirty)!;
        draft.Content = "unsaved user content";
        draft.SetSavedState(false);
        _viewModel.ActiveFile = activeSaved ? _viewModel.GetOpenFile(saved) : draft;
        var evicted = new List<string>();
        _viewModel.FileCacheEvicted += evicted.Add;
        Directory.Delete(folder, recursive: true);

        _viewModel.CloseDeletedFiles(folder.ToUpperInvariant());

        CollectionAssert.AreEquivalent(new[] { dirty, unrelated }, _viewModel.CachedFiles.Select(file => file.FilePath).ToArray());
        CollectionAssert.AreEquivalent(activeSaved ? new[] { nested } : new[] { saved, nested }, evicted);
        Assert.AreSame(draft, _viewModel.GetOpenFile(dirty));
        Assert.AreEqual("unsaved user content", draft.Content);
        Assert.IsFalse(draft.IsSaved);
        if (activeSaved) Assert.IsNull(_viewModel.ActiveFile);
        else Assert.AreSame(draft, _viewModel.ActiveFile);
        Assert.IsFalse(_session.FileManager.ProjectSystem.OpenDocuments.IsOpen(saved));
        Assert.IsFalse(_session.FileManager.ProjectSystem.OpenDocuments.IsOpen(nested));
        Assert.IsTrue(_session.FileManager.ProjectSystem.OpenDocuments.IsOpen(dirty));
        Assert.IsTrue(_session.FileManager.ProjectSystem.OpenDocuments.IsOpen(unrelated));
        Assert.AreEqual("original", File.ReadAllText(unrelated));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CloseDeletedFiles_FileKeepsDirtyBufferAndDoesNotMatchSimilarNames(bool saved) {
        _session.FileManager.Stop();
        string path = Path.Combine(_testRoot, "source.js");
        string other = path + ".backup";
        foreach (string filePath in new[] { other, path }) {
            File.WriteAllText(filePath, "original");
            await _viewModel.OpenFileAsync(filePath);
        }
        var file = _viewModel.ActiveFile!;
        if (!saved) { file.Content = "unsaved user content"; file.SetSavedState(false); }
        var evicted = new List<string>();
        _viewModel.FileCacheEvicted += evicted.Add;
        File.Delete(path);

        _viewModel.CloseDeletedFiles(path);

        Assert.IsNotNull(_viewModel.GetOpenFile(other));
        Assert.AreEqual("original", File.ReadAllText(other));
        Assert.IsEmpty(evicted, "The active Monaco model is replaced by the existing selection flow.");
        Assert.AreEqual(!saved, _session.FileManager.ProjectSystem.OpenDocuments.IsOpen(path));
        if (saved) {
            Assert.IsNull(_viewModel.GetOpenFile(path));
            Assert.IsNull(_viewModel.ActiveFile);
        }
        else {
            Assert.AreSame(file, _viewModel.GetOpenFile(path));
            Assert.AreSame(file, _viewModel.ActiveFile);
            Assert.AreEqual("unsaved user content", file.Content);
        }
    }

    private string _testRoot = null!;
    private WebProjectSession _session = null!;
    private WebEditorViewModel _viewModel = null!;
    private Mock<IUserSettingsClient> _userSettings = null!;
    private Mock<IWallpaperControlClient> _wallpaperControl = null!;
}
