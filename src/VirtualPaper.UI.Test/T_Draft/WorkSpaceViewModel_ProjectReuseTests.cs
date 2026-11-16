using System.Reflection;
using Moq;
using VirtualPaper.Common;
using VirtualPaper.EditPanel.Model;
using VirtualPaper.EditPanel.Services;
using VirtualPaper.EditPanel.ViewModels;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UIComponent.Navigation.TabView.Interfaces;
using Workloads.Entry.FileLoaders;
using Workloads.Entry.Interfaces;
using Workloads.Utils.DraftUtils.Interfaces;

namespace VirtualPaper.UI.Test.T_Draft;

[TestClass]
public class WorkSpaceViewModel_ProjectReuseTests {
    private string _root = null!;
    private WorkSpaceViewModel _vm = null!;
    private Mock<IRuntimeFactory> _factory = null!;
    private Mock<IProjectFileLoader> _loader = null!;
    private Mock<IUserSettingsClient> _settings = null!;

    [TestInitialize]
    public void Setup() {
        _root = Path.Combine(Path.GetTempPath(), "vp-project-reuse-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _settings = new(MockBehavior.Strict);
        _settings.Setup(x => x.UpdateRecetUsedAsync(It.IsAny<string[]>())).Returns(Task.CompletedTask);
        _factory = new(MockBehavior.Strict);
        _loader = new(MockBehavior.Strict);
        _vm = new(_settings.Object, _factory.Object, Mock.Of<IWorkspaceSaveCoordinator>(),
            new ProjectFileLoaderRegistry([_loader.Object]));
    }

    [TestCleanup]
    public void Cleanup() {
        _vm.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    [DataRow(".vpd")]
    [DataRow(".vpw")]
    public async Task Reopen_EquivalentPaths_ActivatesExistingTabWithoutReloading(string extension) {
        string path = Path.Combine(_root, "项目 with spaces" + extension);
        var (runtime, tab) = RegisterOpenProject(path);
        foreach (string requested in new[] { path, path.ToUpperInvariant(), Path.Combine(_root, "unused", "..", Path.GetFileName(path)) }) {
            _vm.SelectedTabIndex = 1;
            Assert.IsTrue(await OpenAsync(requested));
            AssertExistingSelected(runtime.Object, tab);
        }
        runtime.VerifyNoOtherCalls();
        _loader.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow(".vpd")]
    [DataRow(".vpw")]
    public async Task Reopen_SourceDirectoryRemoved_PreservesOpenEditor(string extension) {
        string path = Path.Combine(_root, "project" + extension);
        var (runtime, tab) = RegisterOpenProject(path);
        Directory.Delete(_root);

        Assert.IsTrue(await OpenAsync(path));

        AssertExistingSelected(runtime.Object, tab);
        runtime.VerifyNoOtherCalls();
        _loader.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow(".vpd")]
    [DataRow(".vpw")]
    public async Task Reopen_AfterSaveAs_UsesNewIdentityWithoutReplacingEditor(string extension) {
        var (runtime, tab) = RegisterOpenProject(Path.Combine(_root, "original" + extension));
        string savedPath = Path.Combine(_root, "saved copy" + extension);
        runtime.Setup(x => x.SaveAsAsync()).ReturnsAsync(savedPath);
        var saveAs = typeof(WorkSpaceViewModel).GetMethod("InternalSaveAsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)saveAs.Invoke(_vm, [runtime.Object])!;

        Assert.IsTrue(await OpenAsync(savedPath));

        AssertExistingSelected(runtime.Object, tab);
        runtime.Verify(x => x.SaveAsAsync(), Times.Once);
        runtime.VerifyNoOtherCalls();
        _loader.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow(".vpd", FileType.FDesign)]
    [DataRow(".vpw", FileType.FWebDesign)]
    public async Task Reopen_LoaderResolvesToOpenProject_ActivatesExistingTab(string extension, FileType fileType) {
        string path = Path.Combine(_root, "open" + extension);
        string alias = Path.Combine(_root, "alias" + extension);
        var (runtime, tab) = RegisterOpenProject(path);
        _loader.Setup(x => x.CanLoad(fileType)).Returns(true);
        _loader.Setup(x => x.LoadAsync(alias, fileType)).ReturnsAsync(new ProjectFileLoadResult(path, fileType));

        Assert.IsTrue(await OpenAsync(alias));

        AssertExistingSelected(runtime.Object, tab);
        _loader.Verify(x => x.LoadAsync(alias, fileType), Times.Once);
        runtime.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow(".vpd", false)]
    [DataRow(".vpw", false)]
    [DataRow(".vpd", true)]
    [DataRow(".vpw", true)]
    public async Task SaveAs_CancelledOrFailed_KeepsOriginalTabIdentity(string extension, bool fails) {
        string path = Path.Combine(_root, "original" + extension);
        var (runtime, tab) = RegisterOpenProject(path);
        if (fails) runtime.Setup(x => x.SaveAsAsync()).ThrowsAsync(new IOException("Cannot save the copy"));
        else runtime.Setup(x => x.SaveAsAsync()).ReturnsAsync((string?)null);
        var saveAs = typeof(WorkSpaceViewModel).GetMethod("InternalSaveAsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Task save = (Task)saveAs.Invoke(_vm, [runtime.Object])!;
        if (fails) await Assert.ThrowsExactlyAsync<IOException>(() => save);
        else await save;

        Assert.IsTrue(await OpenAsync(path));
        AssertExistingSelected(runtime.Object, tab);
        runtime.Verify(x => x.SaveAsAsync(), Times.Once);
        runtime.VerifyNoOtherCalls();
        _loader.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow(".vpd", false)]
    [DataRow(".vpw", false)]
    [DataRow(".vpd", true)]
    [DataRow(".vpw", true)]
    public async Task Save_UpdatesIdentityOnlyAfterSuccess(string extension, bool succeeds) {
        string original = Path.Combine(_root, "original" + extension);
        string saved = Path.Combine(_root, "saved" + extension);
        var (runtime, tab) = RegisterOpenProject(original);
        runtime.Setup(x => x.SaveAsync()).ReturnsAsync(succeeds);
        runtime.SetupGet(x => x.ProjectFilePath).Returns(saved);
        var save = typeof(WorkSpaceViewModel).GetMethod("InternalSaveAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)save.Invoke(_vm, [runtime.Object])!;

        Assert.IsTrue(await OpenAsync(succeeds ? saved : original));
        AssertExistingSelected(runtime.Object, tab);
        runtime.Verify(x => x.SaveAsync(), Times.Once);
        _loader.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow(".vpd", FileType.FDesign, false)]
    [DataRow(".vpd", FileType.FDesign, true)]
    [DataRow(".vpw", FileType.FWebDesign, false)]
    [DataRow(".vpw", FileType.FWebDesign, true)]
    public async Task Batch_FailedItemDoesNotPreventLaterReuse_AndRecentItemsExcludeFailures(string extension, FileType type, bool throws) {
        string failed = Path.Combine(_root, "damaged" + extension);
        string alias = Path.Combine(_root, "alias" + extension);
        string project = Path.Combine(_root, "open" + extension);
        File.WriteAllText(failed, "damaged source");
        File.WriteAllText(alias, "alias source");
        var (runtime, tab) = RegisterOpenProject(project);
        _loader.Setup(x => x.CanLoad(type)).Returns(true);
        if (throws) _loader.Setup(x => x.LoadAsync(failed, type)).ThrowsAsync(new InvalidDataException("Damaged project"));
        else _loader.Setup(x => x.LoadAsync(failed, type)).ReturnsAsync((ProjectFileLoadResult?)null);
        _loader.Setup(x => x.LoadAsync(alias, type)).ReturnsAsync(new ProjectFileLoadResult(project, type));
        string[]? recent = null;
        _settings.Setup(x => x.UpdateRecetUsedAsync(It.IsAny<string[]>()))
            .Callback<string[]>(paths => recent = paths.ToArray()).Returns(Task.CompletedTask);

        bool opened = await _vm.AddNewItemsAsync([Existing(failed), Existing(alias), Existing(project), Existing(project)]);

        Assert.IsTrue(opened);
        AssertExistingSelected(runtime.Object, tab);
        Assert.IsNotNull(recent);
        CollectionAssert.AreEquivalent(new[] { alias, project }, recent);
        _settings.Verify(x => x.UpdateRecetUsedAsync(It.IsAny<string[]>()), Times.Once);
        _loader.Verify(x => x.LoadAsync(failed, type), Times.Once);
        _loader.Verify(x => x.LoadAsync(alias, type), Times.Once);
        _loader.Verify(x => x.CanLoad(type), Times.Exactly(2));
        _loader.VerifyNoOtherCalls();
        runtime.VerifyNoOtherCalls();
        Assert.AreEqual("damaged source", File.ReadAllText(failed));
        Assert.AreEqual("alias source", File.ReadAllText(alias));
    }

    [TestMethod]
    [DataRow(".vpd", FileType.FDesign)]
    [DataRow(".vpw", FileType.FWebDesign)]
    public async Task Batch_AllItemsFail_DoesNotCreateTabsOrWriteRecentItems(string extension, FileType type) {
        string first = Path.Combine(_root, "damaged" + extension);
        string second = Path.Combine(_root, "rejected" + extension);
        _loader.Setup(x => x.CanLoad(type)).Returns(true);
        _loader.Setup(x => x.LoadAsync(first, type)).ThrowsAsync(new InvalidDataException("Damaged project"));
        _loader.Setup(x => x.LoadAsync(second, type)).ReturnsAsync((ProjectFileLoadResult?)null);

        Assert.IsFalse(await _vm.AddNewItemsAsync([Existing(first), Existing(second)]));

        Assert.IsEmpty(_vm.TabViewItems);
        Assert.AreEqual(-1, _vm.SelectedTabIndex);
        _settings.VerifyNoOtherCalls();
        _factory.VerifyNoOtherCalls();
        _loader.Verify(x => x.LoadAsync(first, type), Times.Once);
        _loader.Verify(x => x.LoadAsync(second, type), Times.Once);
    }

    [TestMethod]
    [DataRow(".vpd")]
    [DataRow(".vpw")]
    public async Task Batch_RecentSaveFails_PreservesOpenEditorAndCanRetry(string extension) {
        string project = Path.Combine(_root, "open" + extension);
        var (runtime, tab) = RegisterOpenProject(project);
        _settings.SetupSequence(x => x.UpdateRecetUsedAsync(It.IsAny<string[]>()))
            .ThrowsAsync(new IOException("Recent items are locked"))
            .Returns(Task.CompletedTask);

        await Assert.ThrowsExactlyAsync<IOException>(() => _vm.AddNewItemsAsync([Existing(project), Existing(project)]));
        AssertExistingSelected(runtime.Object, tab);
        Assert.IsTrue(await _vm.AddNewItemsAsync([Existing(project)]));

        AssertExistingSelected(runtime.Object, tab);
        _settings.Verify(x => x.UpdateRecetUsedAsync(It.Is<string[]>(paths => paths.Length == 1 && paths[0] == project)), Times.Exactly(2));
        _settings.VerifyNoOtherCalls();
        _loader.VerifyNoOtherCalls();
        runtime.VerifyNoOtherCalls();
    }

    private static PreProjectData Existing(string path) => new(path, ProjectType.P_StaticImage, ProjectOpenIntent.OpenExisting);

    private Task<bool> OpenAsync(string path) => _vm.AddNewItemsAsync([
        new(path, ProjectType.P_StaticImage, ProjectOpenIntent.OpenExisting)
    ]);

    private (Mock<IRuntime> Runtime, IArcTabViewItem Tab) RegisterOpenProject(string path) {
        var runtime = new Mock<IRuntime>(MockBehavior.Strict);
        var tab = new Mock<IArcTabViewItem>();
        tab.SetupProperty(x => x.Tag, runtime.Object);
        _vm.TabViewItems.Add(tab.Object);
        _vm.TabViewItems.Add(Mock.Of<IArcTabViewItem>());
        _vm.SelectedTabIndex = 1;
        // Match the existing workspace test fixtures without creating WinUI controls.
        var identities = (Dictionary<IRuntime, string>)typeof(WorkSpaceViewModel)
            .GetField("_runtimeProjectPaths", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_vm)!;
        identities.Add(runtime.Object, path);
        return (runtime, tab.Object);
    }

    private void AssertExistingSelected(IRuntime runtime, IArcTabViewItem tab) {
        Assert.HasCount(2, _vm.TabViewItems, "Opening again must not add a tab.");
        Assert.AreEqual(0, _vm.SelectedTabIndex);
        Assert.AreSame(tab, _vm.TabViewItems[0]);
        Assert.AreSame(runtime, _vm.GetSelectedRuntime(), "Keep the editor and its unsaved state.");
        _factory.VerifyNoOtherCalls();
    }
}
