using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using VirtualPaper.Common;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UIComponent.Navigation;
using VirtualPaper.UIComponent.Navigation.TabView;
using VirtualPaper.UIComponent.Navigation.TabView.Interfaces;
using VirtualPaper.UIComponent.Utils.Adapter.Interfaces;
using Workloads.Utils.DraftUtils.Interfaces;

namespace VirtualPaper.UI.Test.T_Draft {
    [TestClass]
    public class WorkSpaceViewModel_CheckAllSaveStatusTests {
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

        // ── 全部已保存 ────────────────────────────────────────────────────

        [TestMethod]
        public async Task CheckAllSaveStatusAsync_AllSaved_ReturnsTrue_NoDialog() {
            RegisterRuntime(isSaved: true, "a.vp");
            RegisterRuntime(isSaved: true, "b.vp");

            bool result = await _vm.CheckAllSaveStatusAsync();

            Assert.IsTrue(result);
            _dialogService.Verify(
                d => d.ShowDialogAsync(
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [TestMethod]
        public async Task CheckAllSaveStatusAsync_AllSaved_ClearsAllTabs() {
            RegisterRuntime(isSaved: true, "a.vp");
            RegisterRuntime(isSaved: true, "b.vp");

            await _vm.CheckAllSaveStatusAsync();

            Assert.IsEmpty(_vm.TabViewItems);
        }

        // ── 有未保存，用户选 Primary（保存成功）──────────────────────────

        [TestMethod]
        public async Task CheckAllSaveStatusAsync_Unsaved_UserSavesAll_ReturnsTrue() {
            var r1 = RegisterRuntime(isSaved: false, "a.vp");
            var r2 = RegisterRuntime(isSaved: false, "b.vp");
            r1.Setup(r => r.SaveAsync()).ReturnsAsync(true);
            r2.Setup(r => r.SaveAsync()).ReturnsAsync(true);

            _dialogService
                .Setup(d => d.ShowDialogAsync(
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(DialogResult.Primary);

            bool result = await _vm.CheckAllSaveStatusAsync();

            Assert.IsTrue(result);
            r1.Verify(r => r.SaveAsync(), Times.Once);
            r2.Verify(r => r.SaveAsync(), Times.Once);
        }

        // ── 有未保存，用户选 Primary（保存失败）──────────────────────────

        [TestMethod]
        public async Task CheckAllSaveStatusAsync_Unsaved_SaveFails_ReturnsFalse() {
            var r1 = RegisterRuntime(isSaved: false, "a.vp");
            r1.Setup(r => r.SaveAsync()).ReturnsAsync(false);

            _dialogService
                .Setup(d => d.ShowDialogAsync(
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(DialogResult.Primary);

            bool result = await _vm.CheckAllSaveStatusAsync();

            Assert.IsFalse(result);
        }

        // ── 有未保存，用户选 Secondary（不保存，继续）────────────────────

        [TestMethod]
        public async Task CheckAllSaveStatusAsync_Unsaved_UserDontSave_SkipsSave_ReturnsTrue() {
            var r1 = RegisterRuntime(isSaved: false, "a.vp");

            _dialogService
                .Setup(d => d.ShowDialogAsync(
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(DialogResult.Secondary);

            bool result = await _vm.CheckAllSaveStatusAsync();

            r1.Verify(r => r.SaveAsync(), Times.Never);
            Assert.IsTrue(result);
        }

        // ── 有未保存，用户选 None（取消）─────────────────────────────────

        [TestMethod]
        public async Task CheckAllSaveStatusAsync_Unsaved_UserCancels_ReturnsFalse() {
            RegisterRuntime(isSaved: false, "a.vp");

            _dialogService
                .Setup(d => d.ShowDialogAsync(
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(DialogResult.None);

            bool result = await _vm.CheckAllSaveStatusAsync();

            Assert.IsFalse(result);
        }

        // ── 混合：第一个已保存，第二个未保存且取消 ───────────────────────

        [TestMethod]
        public async Task CheckAllSaveStatusAsync_Mixed_SecondCancels_ReturnsFalse() {
            RegisterRuntime(isSaved: true, "a.vp");
            RegisterRuntime(isSaved: false, "b.vp");

            _dialogService
                .Setup(d => d.ShowDialogAsync(
                    It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(DialogResult.None);

            bool result = await _vm.CheckAllSaveStatusAsync();

            Assert.IsFalse(result);
        }
    }
}
