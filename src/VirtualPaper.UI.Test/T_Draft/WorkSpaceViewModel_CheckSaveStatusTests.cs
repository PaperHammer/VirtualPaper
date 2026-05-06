using Moq;
using VirtualPaper.Common;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UIComponent.Navigation.TabView.Interfaces;
using VirtualPaper.UIComponent.Utils.Adapter.Interfaces;
using Workloads.Utils.DraftUtils.Interfaces;

namespace VirtualPaper.UI.Test.T_Draft {
    [TestClass]
    public class WorkSpaceViewModel_CheckSaveStatusTests {
        private WorkSpaceViewModel _vm = null!;
        private Mock<IGlobalDialogService> _dialogService = null!;

        [TestInitialize]
        public void Setup() {
            _dialogService = new Mock<IGlobalDialogService>();
            _vm = new WorkSpaceViewModel(
                Mock.Of<IUserSettingsClient>(),
                _dialogService.Object);
        }

        /// <summary>
        /// 向 ViewModel 内部 _runtimeToArcTab 注册一个 runtime，
        /// 并同步加入 TabViewItems，模拟 AddToWorkSpace 效果。
        /// </summary>
        private (Mock<IRuntime>, IArcTabViewItem) RegisterRuntime(bool isSaved) {
            var mockRuntime = new Mock<IRuntime>();
           
            var mockHeader = new Mock<IArcTabViewItemHeader>();
            mockHeader.SetupProperty(h => h.IsSaved, isSaved);

            var mockTabItem = new Mock<IArcTabViewItem>();
            mockTabItem.SetupProperty(t => t.Tag, mockRuntime.Object);

            // 通过反射写入私有字典
            var dict = (Dictionary<IRuntime, (IArcTabViewItemHeader, IArcTabViewItem)>)
                typeof(WorkSpaceViewModel)
                    .GetField("_runtimeToArcTab",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance)!
                    .GetValue(_vm)!;

            dict[mockRuntime.Object] = (mockHeader.Object, mockTabItem.Object);
            _vm.TabViewItems.Add(mockTabItem.Object);

            return (mockRuntime, mockTabItem.Object);
        }

        // ── 已保存，直接关闭 ──────────────────────────────────────────────

        [TestMethod]
        public async Task CheckSaveStatusAsync_AlreadySaved_ReturnsTrue_NoDialog() {
            var (mockRuntime, _) = RegisterRuntime(isSaved: true);

            bool result = await _vm.CheckSaveStatusAsync(mockRuntime.Object);

            Assert.IsTrue(result);
            _dialogService.Verify(
                d => d.ShowDialogAsync(
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [TestMethod]
        public async Task CheckSaveStatusAsync_AlreadySaved_RemovesTabFromCollection() {
            var (mockRuntime, tabItem) = RegisterRuntime(isSaved: true);

            await _vm.CheckSaveStatusAsync(mockRuntime.Object);

            Assert.DoesNotContain(tabItem, _vm.TabViewItems);
        }

        // ── 未保存，用户选 Primary（保存）────────────────────────────────

        [TestMethod]
        public async Task CheckSaveStatusAsync_Unsaved_UserSaves_CallsSaveAsync() {
            var (mockRuntime, _) = RegisterRuntime(isSaved: false);
            mockRuntime.Setup(r => r.FileName).Returns("test.vp");
            mockRuntime.Setup(r => r.SaveAsync()).ReturnsAsync(true);

            _dialogService
                .Setup(d => d.ShowDialogAsync(
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(DialogResult.Primary);

            bool result = await _vm.CheckSaveStatusAsync(mockRuntime.Object);

            mockRuntime.Verify(r => r.SaveAsync(), Times.Once);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task CheckSaveStatusAsync_Unsaved_UserSaves_SaveFails_ReturnsFalse() {
            var (mockRuntime, tabItem) = RegisterRuntime(isSaved: false);
            mockRuntime.Setup(r => r.FileName).Returns("test.vp");
            mockRuntime.Setup(r => r.SaveAsync()).ReturnsAsync(false);

            _dialogService
                .Setup(d => d.ShowDialogAsync(
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(DialogResult.Primary);

            bool result = await _vm.CheckSaveStatusAsync(mockRuntime.Object);

            Assert.IsFalse(result);
            // 保存失败，Tab 不应被关闭
            Assert.Contains(tabItem, _vm.TabViewItems);
        }

        // ── 未保存，用户选 Secondary（不保存直接关闭）────────────────────

        [TestMethod]
        public async Task CheckSaveStatusAsync_Unsaved_UserDontSave_ReturnsTrue_NoSave() {
            var (mockRuntime, _) = RegisterRuntime(isSaved: false);
            mockRuntime.Setup(r => r.FileName).Returns("test.vp");

            _dialogService
                .Setup(d => d.ShowDialogAsync(
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(DialogResult.Secondary);

            bool result = await _vm.CheckSaveStatusAsync(mockRuntime.Object);

            mockRuntime.Verify(r => r.SaveAsync(), Times.Never);
            Assert.IsTrue(result);
        }

        // ── 未保存，用户选 Close（取消）──────────────────────────────────

        [TestMethod]
        public async Task CheckSaveStatusAsync_Unsaved_UserCancels_ReturnsFalse() {
            var (mockRuntime, tabItem) = RegisterRuntime(isSaved: false);
            mockRuntime.Setup(r => r.FileName).Returns("test.vp");

            _dialogService
                .Setup(d => d.ShowDialogAsync(
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(DialogResult.None);

            bool result = await _vm.CheckSaveStatusAsync(mockRuntime.Object);

            Assert.IsFalse(result);
            // 取消时 Tab 不应被关闭
            Assert.Contains(tabItem, _vm.TabViewItems);
        }
    }
}
