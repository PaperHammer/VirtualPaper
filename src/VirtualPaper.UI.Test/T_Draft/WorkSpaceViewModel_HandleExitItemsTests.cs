using Moq;
using VirtualPaper.Common;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UIComponent.Navigation.TabView.Interfaces;
using VirtualPaper.UIComponent.Utils.Adapter.Interfaces;
using Workloads.Utils.DraftUtils.Interfaces;

namespace VirtualPaper.UI.Test.T_Draft {
    [TestClass]
    public class WorkSpaceViewModel_HandleExitItemsTests {
        private WorkSpaceViewModel _vm = null!;
        private Mock<IGlobalDialogService> _dialogService = null!;

        [TestInitialize]
        public void Setup() {
            _dialogService = new Mock<IGlobalDialogService>();
            _vm = new WorkSpaceViewModel(
                Mock.Of<IUserSettingsClient>(),
                _dialogService.Object);
        }

        private Mock<IRuntime> RegisterRuntime(bool isSaved, string fileName = "file.vp") {
            var mockRuntime = new Mock<IRuntime>();
            mockRuntime.Setup(r => r.FileName).Returns(fileName);

            var mockHeader = new Mock<IArcTabViewItemHeader>();
            mockHeader.SetupProperty(h => h.IsSaved, isSaved);

            var mockTabItem = new Mock<IArcTabViewItem>();
            mockTabItem.SetupProperty(t => t.Tag, mockRuntime.Object);

            var dict = (Dictionary<IRuntime, (IArcTabViewItemHeader, IArcTabViewItem)>)
                typeof(WorkSpaceViewModel)
                    .GetField("_runtimeToArcTab",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance)!
                    .GetValue(_vm)!;

            dict[mockRuntime.Object] = (mockHeader.Object, mockTabItem.Object);
            _vm.TabViewItems.Add(mockTabItem.Object);

            return mockRuntime;
        }

        private static async Task<List<IArcTabViewItem>> CollectAsync(IAsyncEnumerable<IArcTabViewItem> source) {
            var list = new List<IArcTabViewItem>();
            await foreach (var item in source) list.Add(item);
            return list;
        }

        // ── 已保存，不弹窗，不 yield ──────────────────────────────────────

        [TestMethod]
        public async Task HandleExitItemsAsync_SavedTabs_NotYielded() {
            RegisterRuntime(isSaved: true, "a.vp");

            var yielded = await CollectAsync(_vm.HandleExitItemsAsync());

            Assert.IsEmpty(yielded);
            _dialogService.Verify(
                d => d.ShowDialogAsync(
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        // ── 未保存，用户选 Primary（保存成功），yield ─────────────────────

        [TestMethod]
        public async Task HandleExitItemsAsync_Unsaved_UserSaves_TabYielded() {
            var r = RegisterRuntime(isSaved: false, "a.vp");
            r.Setup(x => x.SaveAsync()).ReturnsAsync(true);

            _dialogService
                .Setup(d => d.ShowDialogAsync(
                    It.IsAny<object>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(DialogResult.Primary);

            var yielded = await CollectAsync(_vm.HandleExitItemsAsync());

            Assert.HasCount(1, yielded);
            r.Verify(x => x.SaveAsync(), Times.Once);
        }

        // ── 未保存，用户选 Primary（保存失败），不 yield ───────────────────

        [TestMethod]
        public async Task HandleExitItemsAsync_Unsaved_SaveFails_TabNotYielded() {
            var r = RegisterRuntime(isSaved: false, "a.vp");
            r.Setup(x => x.SaveAsync()).ReturnsAsync(false);

            _dialogService
                .Setup(d => d.ShowDialogAsync(
                    It.IsAny<object>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(DialogResult.Primary);

            var yielded = await CollectAsync(_vm.HandleExitItemsAsync());

            Assert.IsEmpty(yielded);
        }

        // ── 未保存，用户选 Secondary（不保存），yield ─────────────────────

        [TestMethod]
        public async Task HandleExitItemsAsync_Unsaved_UserDontSave_TabYielded() {
            RegisterRuntime(isSaved: false, "a.vp");

            _dialogService
                .Setup(d => d.ShowDialogAsync(
                    It.IsAny<object>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(DialogResult.Secondary);

            var yielded = await CollectAsync(_vm.HandleExitItemsAsync());

            Assert.HasCount(1, yielded);
        }

        // ── 未保存，用户关闭弹窗（None），不 yield ────────────────────────

        [TestMethod]
        public async Task HandleExitItemsAsync_Unsaved_UserClosesDialog_TabNotYielded() {
            RegisterRuntime(isSaved: false, "a.vp");

            _dialogService
                .Setup(d => d.ShowDialogAsync(
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(DialogResult.None);

            var yielded = await CollectAsync(_vm.HandleExitItemsAsync());

            Assert.IsEmpty(yielded);
        }

        // ── 多个 Tab，部分关闭 ────────────────────────────────────────────

        [TestMethod]
        public async Task HandleExitItemsAsync_Mixed_OnlyConfirmedTabsYielded() {
            RegisterRuntime(isSaved: true, "saved.vp");
            var unsaved = RegisterRuntime(isSaved: false, "unsaved.vp");
            unsaved.Setup(x => x.SaveAsync()).ReturnsAsync(true);

            _dialogService
                .Setup(d => d.ShowDialogAsync(
                    It.IsAny<object>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(DialogResult.Primary);

            var yielded = await CollectAsync(_vm.HandleExitItemsAsync());

            // saved 不弹窗不 yield，unsaved 保存成功后 yield
            Assert.HasCount(1, yielded);
        }
    }
}
