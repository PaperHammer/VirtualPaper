using System.Text;
using Moq;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UIComponent.Utils;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Creation.WebBackdrop.Models;
using Workloads.Creation.WebBackdrop.ViewModels;

namespace WebBackdrop.Test.T_WebBackdrop;

[TestClass]
public class WebEditorViewModelTests {
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
        Assert.IsTrue(_session.FileManager.ProjectSystem.Documents.IsOpen(path));
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

        bool saved = await _viewModel.SaveFileAsAsync(file, copyPath);

        Assert.IsTrue(saved);
        Assert.AreEqual("copy-content", File.ReadAllText(copyPath));
        Assert.AreEqual(path, file.FilePath);
        Assert.AreSame(file, _viewModel.ActiveFile);
    }

    [TestMethod]
    public async Task BinaryFileCannotBeSavedAsText() {
        string path = Path.Combine(_testRoot, "image.png");
        File.WriteAllBytes(path, [1, 2, 3]);
        await _viewModel.OpenFileAsync(path);

        bool saved = await _viewModel.SaveActiveFileAsync();

        Assert.IsFalse(saved);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
        Assert.IsFalse(_session.FileManager.ProjectSystem.Documents.IsOpen(path));
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
        Assert.IsTrue(_session.FileManager.ProjectSystem.Documents.IsOpen(newPath));

        _viewModel.CloseOpenFile(newPath);
        Assert.IsNull(_viewModel.ActiveFile);
        Assert.IsEmpty(_viewModel.CachedFiles);
        Assert.IsFalse(_session.FileManager.ProjectSystem.Documents.IsOpen(newPath));
    }

    [TestMethod]
    public async Task OpeningBeyondCacheLimit_EvictsOldestSavedInactiveFiles() {
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
    }

    [TestMethod]
    public async Task UpdateRecentUsed_OnlyForwardsNonEmptyPaths() {
        await _viewModel.UpdateRecentUsedAsync(string.Empty);
        await _viewModel.UpdateRecentUsedAsync("project.vpw");

        _userSettings.Verify(client => client.UpdateRecentUsedAsync("project.vpw"), Times.Once);
        _userSettings.Verify(client => client.UpdateRecentUsedAsync(string.Empty), Times.Never);
    }

    private string _testRoot = null!;
    private WebProjectSession _session = null!;
    private WebEditorViewModel _viewModel = null!;
    private Mock<IUserSettingsClient> _userSettings = null!;
    private Mock<IWallpaperControlClient> _wallpaperControl = null!;
}
