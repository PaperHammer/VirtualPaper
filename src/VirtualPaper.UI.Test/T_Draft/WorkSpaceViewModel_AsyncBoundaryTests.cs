using System.Reflection;
using Moq;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.EditPanel.Services;
using VirtualPaper.EditPanel.ViewModels;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UI.Test.Utils;
using VirtualPaper.UIComponent.Navigation.TabView.Interfaces;
using Workloads.Entry.FileLoaders;
using Workloads.Entry.Interfaces;
using Workloads.Utils.DraftUtils.Interfaces;

namespace VirtualPaper.UI.Test.T_Draft;

[TestClass]
[DoNotParallelize]
public class WorkSpaceViewModel_AsyncBoundaryTests {
    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize() {
        _previousUi = (IUiSynchronizationContext?)typeof(CrossThreadInvoker)
            .GetField("_uiSyncContext", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
        CrossThreadInvoker.Initialize(new TUiSynchronizationContext());
        _coordinator = new Mock<IWorkspaceSaveCoordinator>();
        _coordinator.Setup(x => x.CanCloseAsync(It.IsAny<IRuntime>(), It.IsAny<bool>(), true)).ReturnsAsync(true);
        _vm = new WorkSpaceViewModel(Mock.Of<IUserSettingsClient>(), Mock.Of<IRuntimeFactory>(),
            _coordinator.Object, new ProjectFileLoaderRegistry([]));
    }

    [TestCleanup]
    public void Cleanup() {
        _vm.Dispose();
        CrossThreadInvoker.Initialize(_previousUi!);
    }

    [TestMethod]
    [DataRow("SaveAsync")]
    [DataRow("SaveAsAsync")]
    public async Task Save_SelectionChangesWhileAwaiting_UpdatesOnlyTheOriginalProject(string command) {
        var first = Register("original.vpd");
        var second = Register("background.vpw");
        _vm.SelectedTabIndex = 0;
        var completion = PendingSave(first.Runtime);
        Task saving = Invoke(command);
        try {
            Assert.IsFalse(saving.IsCompleted);
            _vm.SelectedTabIndex = 1;
            completion.SetResult(true);
            await saving.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

            Assert.AreEqual("copy.vpd", Paths[first.Runtime.Object]);
            Assert.AreEqual("background.vpw", Paths[second.Runtime.Object]);
            Assert.AreSame(second.Runtime.Object, _vm.GetSelectedRuntime());
            CollectionAssert.AreEqual(new[] { first.Tab, second.Tab }, _vm.TabViewItems.ToArray());
            second.Runtime.Verify(x => x.SaveAsync(), Times.Never);
            second.Runtime.Verify(x => x.SaveAsAsync(), Times.Never);
        }
        finally { completion.TrySetResult(true); await saving; }
    }

    [TestMethod]
    [DataRow("SaveAsync", false)]
    [DataRow("SaveAsAsync", false)]
    [DataRow("SaveAsync", true)]
    [DataRow("SaveAsAsync", true)]
    public async Task Save_CompletesAfterCloseOrDispose_DoesNotRestoreClosedProject(string command, bool dispose) {
        var first = Register("original.vpd");
        var second = Register("background.vpw");
        _vm.SelectedTabIndex = 0;
        var completion = PendingSave(first.Runtime);
        Task saving = Invoke(command);
        try {
            Assert.IsFalse(saving.IsCompleted);
            if (dispose) _vm.Dispose();
            else Assert.IsTrue(await _vm.CheckSaveStatusAsync(first.Runtime.Object));
            Assert.IsFalse(Paths.ContainsKey(first.Runtime.Object));

            completion.SetResult(true);
            await saving.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

            Assert.IsFalse(Paths.ContainsKey(first.Runtime.Object), "A late save must not retain a closed runtime.");
            Assert.IsFalse(Tabs.ContainsKey(first.Runtime.Object));
            if (dispose) {
                Assert.IsEmpty(Paths);
                Assert.IsEmpty(_vm.TabViewItems);
            }
            else {
                Assert.AreEqual("background.vpw", Paths[second.Runtime.Object]);
                CollectionAssert.AreEqual(new[] { second.Tab }, _vm.TabViewItems.ToArray());
            }
        }
        finally { completion.TrySetResult(true); await saving; }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CloseAll_StopsAtDeclinedOrFailedSave_RetainsRemainingProjectsForRetry(bool throws) {
        var first = Register("first.vpd");
        var second = Register("second.vpw");
        var third = Register("third.vpd");
        var failure = new IOException("Save failed");
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _coordinator.Setup(x => x.CanCloseAsync(second.Runtime.Object, false, true)).Returns(completion.Task);

        Task<bool> closing = _vm.CheckAllSaveStatusAsync();
        try {
            Assert.IsFalse(closing.IsCompleted);
            Assert.IsFalse(Paths.ContainsKey(first.Runtime.Object));
            Assert.IsFalse(Tabs.ContainsKey(first.Runtime.Object));
            CollectionAssert.AreEqual(new[] { second.Tab, third.Tab }, _vm.TabViewItems.ToArray());
            if (throws) {
                completion.SetException(failure);
                Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<IOException>(() => closing));
            }
            else {
                completion.SetResult(false);
                Assert.IsFalse(await closing);
            }

            CollectionAssert.AreEqual(new[] { second.Tab, third.Tab }, _vm.TabViewItems.ToArray());
            Assert.AreEqual("second.vpw", Paths[second.Runtime.Object]);
            Assert.AreEqual("third.vpd", Paths[third.Runtime.Object]);
            Assert.IsFalse(second.Header.IsSaved);
            Assert.IsFalse(third.Header.IsSaved);
            _coordinator.Verify(x => x.CanCloseAsync(third.Runtime.Object, It.IsAny<bool>(), true), Times.Never);

            _coordinator.Setup(x => x.CanCloseAsync(second.Runtime.Object, false, true)).ReturnsAsync(true);
            Assert.IsTrue(await _vm.CheckAllSaveStatusAsync());
            Assert.IsEmpty(Paths);
            Assert.IsEmpty(Tabs);
            Assert.IsEmpty(_vm.TabViewItems);
            _coordinator.Verify(x => x.CanCloseAsync(first.Runtime.Object, false, true), Times.Once);
        }
        finally {
            completion.TrySetResult(false);
            // Observe the fault even when an earlier assertion fails.
            try { await closing; }
            catch (IOException) when (throws) { }
        }
    }

    private (Mock<IRuntime> Runtime, IArcTabViewItem Tab, IArcTabViewItemHeader Header) Register(string path) {
        var runtime = new Mock<IRuntime>();
        runtime.Setup(x => x.FileName).Returns(path);
        runtime.Setup(x => x.ProjectFilePath).Returns("copy.vpd");
        var header = new Mock<IArcTabViewItemHeader>();
        header.SetupProperty(x => x.IsSaved, false);
        var tab = new Mock<IArcTabViewItem>();
        tab.SetupProperty(x => x.Tag, runtime.Object);
        _vm.TabViewItems.Add(tab.Object);
        Tabs[runtime.Object] = (header.Object, tab.Object);
        Paths[runtime.Object] = path;
        return (runtime, tab.Object, header.Object);
    }

    private static TaskCompletionSource<bool> PendingSave(Mock<IRuntime> runtime) {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.Setup(x => x.SaveAsync()).Returns(completion.Task);
        runtime.Setup(x => x.SaveAsAsync()).Returns(async () => { await completion.Task; return "copy.vpd"; });
        return completion;
    }

    private Task Invoke(string name) => (Task)typeof(WorkSpaceViewModel)
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_vm, null)!;
    private Dictionary<IRuntime, string> Paths => Field<Dictionary<IRuntime, string>>("_runtimeProjectPaths");
    private Dictionary<IRuntime, (IArcTabViewItemHeader Header, IArcTabViewItem Item)> Tabs =>
        Field<Dictionary<IRuntime, (IArcTabViewItemHeader, IArcTabViewItem)>>("_runtimeToArcTab");
    private T Field<T>(string name) => (T)typeof(WorkSpaceViewModel)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_vm)!;

    private WorkSpaceViewModel _vm = null!;
    private Mock<IWorkspaceSaveCoordinator> _coordinator = null!;
    private IUiSynchronizationContext? _previousUi;
}
