using System.Reflection;
using Moq;
using VirtualPaper.EditPanel.Services;
using VirtualPaper.EditPanel.ViewModels;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UIComponent.Navigation.TabView.Interfaces;
using Workloads.Entry.FileLoaders;
using Workloads.Entry.Interfaces;
using Workloads.Utils.DraftUtils.Interfaces;
using Workloads.Utils.DraftUtils.Models;

namespace VirtualPaper.UI.Test.T_Draft;

[TestClass]
public class WorkSpaceViewModel_CommandTests {
    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize() => _vm = new WorkSpaceViewModel(Mock.Of<IUserSettingsClient>(),
        Mock.Of<IRuntimeFactory>(), Mock.Of<IWorkspaceSaveCoordinator>(), new ProjectFileLoaderRegistry([]));

    [TestCleanup]
    public void Cleanup() => _vm.Dispose();

    [TestMethod]
    [DataRow("UndoAsync")]
    [DataRow("RedoAsync")]
    [DataRow("SaveAsync")]
    [DataRow("SaveAsAsync")]
    public async Task Command_DispatchesOnlyToSelectedRuntime(string command) {
        var first = AddRuntime(); var selected = AddRuntime();
        _vm.SelectedTabIndex = 1;
        await Invoke(command);
        first.Verify(runtime => runtime.UndoAsync(), Times.Never);
        first.Verify(runtime => runtime.RedoAsync(), Times.Never);
        first.Verify(runtime => runtime.SaveAsync(), Times.Never);
        first.Verify(runtime => runtime.SaveAsAsync(), Times.Never);
        switch (command) {
            case "UndoAsync": selected.Verify(runtime => runtime.UndoAsync(), Times.Once); break;
            case "RedoAsync": selected.Verify(runtime => runtime.RedoAsync(), Times.Once); break;
            case "SaveAsync": selected.Verify(runtime => runtime.SaveAsync(), Times.Once); break;
            case "SaveAsAsync": selected.Verify(runtime => runtime.SaveAsAsync(), Times.Once); break;
        }
    }

    [TestMethod]
    public async Task NoSelection_CommandsDoNotInvokeAnOpenBackgroundProject() {
        var background = AddRuntime();
        foreach (string command in new[] { "UndoAsync", "RedoAsync", "SaveAsync", "SaveAsAsync" }) await Invoke(command);
        await _vm.ExportAsync(ExportImageFormat.Png);
        Assert.IsFalse(await _vm.AddToLibraryAsync());
        background.Verify(runtime => runtime.UndoAsync(), Times.Never);
        background.Verify(runtime => runtime.RedoAsync(), Times.Never);
        background.Verify(runtime => runtime.SaveAsync(), Times.Never);
        background.Verify(runtime => runtime.SaveAsAsync(), Times.Never);
        background.Verify(runtime => runtime.ExportAsync(It.IsAny<ExportImageFormat>()), Times.Never);
        background.Verify(runtime => runtime.AddToLibraryAsync(), Times.Never);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AddToLibrary_ReturnsSelectedRuntimeResult(bool result) {
        var background = AddRuntime(); var selected = AddRuntime(); _vm.SelectedTabIndex = 1;
        selected.Setup(runtime => runtime.AddToLibraryAsync()).ReturnsAsync(result);
        Assert.AreEqual(result, await _vm.AddToLibraryAsync());
        selected.Verify(runtime => runtime.AddToLibraryAsync(), Times.Once);
        background.Verify(runtime => runtime.AddToLibraryAsync(), Times.Never);
    }

    [TestMethod]
    [DataRow(ExportImageFormat.Png)]
    [DataRow(ExportImageFormat.FullZip)]
    public async Task Export_ForwardsFormatAndFailure_WithoutTouchingBackgroundProject(ExportImageFormat format) {
        var background = AddRuntime(); var selected = AddRuntime(); _vm.SelectedTabIndex = 1;
        var failure = new IOException("Cannot export");
        selected.Setup(runtime => runtime.ExportAsync(format)).ThrowsAsync(failure);
        Assert.AreSame(failure, await Assert.ThrowsAsync<IOException>(() => _vm.ExportAsync(format)));
        selected.Verify(runtime => runtime.ExportAsync(format), Times.Once);
        background.Verify(runtime => runtime.ExportAsync(It.IsAny<ExportImageFormat>()), Times.Never);
        Assert.AreSame(selected.Object, _vm.GetSelectedRuntime());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Save_ChangesTrackedProjectPathOnlyOnSuccess(bool saved) {
        var runtime = AddRuntime(); _vm.SelectedTabIndex = 0;
        Paths[runtime.Object] = "old.vpd";
        runtime.Setup(item => item.ProjectFilePath).Returns("new.vpd");
        runtime.Setup(item => item.SaveAsync()).ReturnsAsync(saved);
        await Invoke("SaveAsync");
        Assert.AreEqual(saved ? "new.vpd" : "old.vpd", Paths[runtime.Object]);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    [DataRow("copy.vpw")]
    public async Task SaveAs_CancellationKeepsExistingIdentity_AndSuccessTracksCopy(string? result) {
        var runtime = AddRuntime(); var background = AddRuntime(); _vm.SelectedTabIndex = 0;
        Paths[runtime.Object] = "original.vpw"; Paths[background.Object] = "background.vpd";
        runtime.Setup(item => item.SaveAsAsync()).ReturnsAsync(result);
        await Invoke("SaveAsAsync");
        Assert.AreEqual(string.IsNullOrWhiteSpace(result) ? "original.vpw" : result, Paths[runtime.Object]);
        Assert.AreEqual("background.vpd", Paths[background.Object]);
        Assert.AreSame(runtime.Object, _vm.GetSelectedRuntime());
        Assert.HasCount(2, _vm.TabViewItems);
    }

    [TestMethod]
    public async Task SaveAll_IsSequential_AndUsesTheInitialRuntimeSnapshot() {
        var first = AddRuntime(); var second = AddRuntime();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<string>();
        first.Setup(runtime => runtime.SaveAsync()).Returns(() => { order.Add("first"); entered.SetResult(); return release.Task; });
        second.Setup(runtime => runtime.SaveAsync()).Returns(() => { order.Add("second"); return Task.FromResult(true); });
        Task saving = Invoke("SaveAllAsync");
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
            second.Verify(runtime => runtime.SaveAsync(), Times.Never);
            _vm.TabViewItems.RemoveAt(1);
            var addedLater = AddRuntime();
            release.SetResult(true);
            await saving.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
            CollectionAssert.AreEqual(new[] { "first", "second" }, order);
            addedLater.Verify(runtime => runtime.SaveAsync(), Times.Never);
        }
        finally { release.TrySetResult(true); await saving; }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SaveAll_FalseResultAllowsLaterSaves_ButExceptionStopsAndPreservesPaths(bool throws) {
        var first = AddRuntime(); var second = AddRuntime();
        Paths[first.Object] = "original.vpd";
        if (throws) first.Setup(runtime => runtime.SaveAsync()).ThrowsAsync(new IOException("Disk unavailable"));
        else first.Setup(runtime => runtime.SaveAsync()).ReturnsAsync(false);
        if (throws) await Assert.ThrowsAsync<IOException>(() => Invoke("SaveAllAsync"));
        else await Invoke("SaveAllAsync");
        Assert.AreEqual("original.vpd", Paths[first.Object]);
        second.Verify(runtime => runtime.SaveAsync(), throws ? Times.Never : Times.Once);
        Assert.HasCount(2, _vm.TabViewItems);
    }

    private Mock<IRuntime> AddRuntime() {
        var runtime = new Mock<IRuntime>();
        runtime.Setup(item => item.UndoAsync()).Returns(Task.CompletedTask);
        runtime.Setup(item => item.RedoAsync()).Returns(Task.CompletedTask);
        runtime.Setup(item => item.SaveAsync()).ReturnsAsync(true);
        runtime.Setup(item => item.SaveAsAsync()).ReturnsAsync((string?)null);
        var tab = new Mock<IArcTabViewItem>(); tab.SetupProperty(item => item.Tag, runtime.Object);
        _vm.TabViewItems.Add(tab.Object);
        return runtime;
    }

    // Invoke the awaitable command handlers without async-void ICommand wrappers or real controls.
    private Task Invoke(string name) => (Task)typeof(WorkSpaceViewModel)
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_vm, null)!;
    private Dictionary<IRuntime, string> Paths => (Dictionary<IRuntime, string>)typeof(WorkSpaceViewModel)
        .GetField("_runtimeProjectPaths", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_vm)!;
    private WorkSpaceViewModel _vm = null!;
}
