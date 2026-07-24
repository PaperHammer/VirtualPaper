using Moq;
using VirtualPaper.DraftPanel.Services;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UIComponent.Navigation.TabView.Interfaces;
using Workloads.Utils.DraftUtils.Interfaces;

namespace VirtualPaper.UI.Test.T_Draft {
    [TestClass]
    public class WorkSpaceViewModel_CheckAllSaveStatusTests {
        private WorkSpaceViewModel _vm = null!;
        private Mock<IWorkspaceSaveCoordinator> _saveCoordinator = null!;

        [TestInitialize]
        public void Setup() {
            _saveCoordinator = new Mock<IWorkspaceSaveCoordinator>();
            _saveCoordinator
                .Setup(x => x.CanCloseAsync(It.IsAny<IRuntime>(), It.IsAny<bool>(), It.IsAny<bool>()))
                .ReturnsAsync(true);
            _vm = new WorkSpaceViewModel(
                Mock.Of<IUserSettingsClient>(),
                Mock.Of<Workloads.Entry.Interfaces.IRuntimeFactory>(),
                _saveCoordinator.Object,
                new Mock<Workloads.Entry.FileLoaders.ProjectFileLoaderRegistry>(new Workloads.Entry.FileLoaders.IProjectFileLoader[] { }).Object);
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

        [TestMethod]
        public async Task CheckAllSaveStatusAsync_AllCanClose_ReturnsTrue() {
            RegisterRuntime(isSaved: true, "a.vp");
            RegisterRuntime(isSaved: true, "b.vp");

            bool result = await _vm.CheckAllSaveStatusAsync();

            Assert.IsTrue(result);
            _saveCoordinator.Verify(x => x.CanCloseAsync(It.IsAny<IRuntime>(), It.IsAny<bool>(), true), Times.Exactly(2));
        }

        [TestMethod]
        public async Task CheckAllSaveStatusAsync_AllCanClose_ClearsAllTabs() {
            RegisterRuntime(isSaved: true, "a.vp");
            RegisterRuntime(isSaved: true, "b.vp");

            await _vm.CheckAllSaveStatusAsync();

            Assert.IsEmpty(_vm.TabViewItems);
        }

        [TestMethod]
        public async Task CheckAllSaveStatusAsync_UnsavedCanClose_ReturnsTrue() {
            var r1 = RegisterRuntime(isSaved: false, "a.vp");
            var r2 = RegisterRuntime(isSaved: false, "b.vp");

            bool result = await _vm.CheckAllSaveStatusAsync();

            Assert.IsTrue(result);
            _saveCoordinator.Verify(x => x.CanCloseAsync(r1.Object, false, true), Times.Once);
            _saveCoordinator.Verify(x => x.CanCloseAsync(r2.Object, false, true), Times.Once);
        }

        [TestMethod]
        public async Task CheckAllSaveStatusAsync_FirstCannotClose_ReturnsFalse() {
            var r1 = RegisterRuntime(isSaved: false, "a.vp");
            _saveCoordinator
                .Setup(x => x.CanCloseAsync(r1.Object, false, true))
                .ReturnsAsync(false);

            bool result = await _vm.CheckAllSaveStatusAsync();

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task CheckAllSaveStatusAsync_Mixed_SecondCannotClose_ReturnsFalse() {
            RegisterRuntime(isSaved: true, "a.vp");
            var r2 = RegisterRuntime(isSaved: false, "b.vp");
            _saveCoordinator
                .Setup(x => x.CanCloseAsync(r2.Object, false, true))
                .ReturnsAsync(false);

            bool result = await _vm.CheckAllSaveStatusAsync();

            Assert.IsFalse(result);
        }
    }
}
