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

    [TestInitialize]
    public void Setup() {
        _root = Path.Combine(Path.GetTempPath(), "vp-project-reuse-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        var settings = new Mock<IUserSettingsClient>(MockBehavior.Strict);
        settings.Setup(x => x.UpdateRecetUsedAsync(It.IsAny<string[]>())).Returns(Task.CompletedTask);
        _factory = new(MockBehavior.Strict);
        _loader = new(MockBehavior.Strict);
        _vm = new(settings.Object, _factory.Object, Mock.Of<IWorkspaceSaveCoordinator>(),
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
