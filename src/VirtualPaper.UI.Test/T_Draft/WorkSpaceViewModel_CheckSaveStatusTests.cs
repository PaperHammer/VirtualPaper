using Moq;
using VirtualPaper.EditPanel.Services;
using VirtualPaper.EditPanel.ViewModels;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UIComponent.Navigation.TabView.Interfaces;
using Workloads.Utils.DraftUtils.Interfaces;

namespace VirtualPaper.UI.Test.T_Draft {
    [TestClass]
    public class WorkSpaceViewModel_CheckSaveStatusTests {
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
                new Mock<Workloads.Entry.FileLoaders.ProjectFileLoaderRegistry>(
                    new Workloads.Entry.FileLoaders.IProjectFileLoader[] { }).Object);
        }

        private (Mock<IRuntime>, IArcTabViewItem) RegisterRuntime(bool isSaved) {
            var mockRuntime = new Mock<IRuntime>();
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

            return (mockRuntime, mockTabItem.Object);
        }

        [TestMethod]
        public async Task CheckSaveStatusAsync_AlreadySaved_ReturnsTrue() {
            var (mockRuntime, _) = RegisterRuntime(isSaved: true);

            bool result = await _vm.CheckSaveStatusAsync(mockRuntime.Object);

            Assert.IsTrue(result);
            _saveCoordinator.Verify(x => x.CanCloseAsync(mockRuntime.Object, true, true), Times.Once);
        }

        [TestMethod]
        public async Task CheckSaveStatusAsync_AlreadySaved_RemovesTabFromCollection() {
            var (mockRuntime, tabItem) = RegisterRuntime(isSaved: true);

            await _vm.CheckSaveStatusAsync(mockRuntime.Object);

            Assert.DoesNotContain(tabItem, _vm.TabViewItems);
        }

        [TestMethod]
        public async Task CheckSaveStatusAsync_Unsaved_CanClose_ReturnsTrue() {
            var (mockRuntime, _) = RegisterRuntime(isSaved: false);

            bool result = await _vm.CheckSaveStatusAsync(mockRuntime.Object);

            Assert.IsTrue(result);
            _saveCoordinator.Verify(x => x.CanCloseAsync(mockRuntime.Object, false, true), Times.Once);
        }

        [TestMethod]
        public async Task CheckSaveStatusAsync_Unsaved_CannotClose_ReturnsFalse() {
            var (mockRuntime, tabItem) = RegisterRuntime(isSaved: false);
            _saveCoordinator
                .Setup(x => x.CanCloseAsync(mockRuntime.Object, false, true))
                .ReturnsAsync(false);

            bool result = await _vm.CheckSaveStatusAsync(mockRuntime.Object);

            Assert.IsFalse(result);
            Assert.Contains(tabItem, _vm.TabViewItems);
        }

        [TestMethod]
        public async Task CheckSaveStatusAsync_RuntimeNotRegistered_ReturnsFalse() {
            var mockRuntime = new Mock<IRuntime>();

            bool result = await _vm.CheckSaveStatusAsync(mockRuntime.Object);

            Assert.IsFalse(result);
            _saveCoordinator.Verify(x => x.CanCloseAsync(It.IsAny<IRuntime>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
        }
    }
}
