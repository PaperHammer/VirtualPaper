using Moq;
using VirtualPaper.EditPanel.Services;
using VirtualPaper.EditPanel.ViewModels;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UIComponent.Navigation.TabView.Interfaces;
using Workloads.Utils.DraftUtils.Interfaces;

namespace VirtualPaper.UI.Test.T_Draft {
    [TestClass]
    public class WorkSpaceViewModel_HandleExitItemsTests {
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
        public async Task HandleExitItemsAsync_AllSaved_YieldsNothing() {
            RegisterRuntime(isSaved: true);
            RegisterRuntime(isSaved: true);

            var result = new List<IArcTabViewItem>();
            await foreach (var item in _vm.HandleExitItemsAsync()) {
                result.Add(item);
            }

            Assert.IsEmpty(result);
            Assert.AreEqual(2, _vm.TabViewItems.Count);
            _saveCoordinator.Verify(x => x.CanCloseAsync(It.IsAny<IRuntime>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
        }

        [TestMethod]
        public async Task HandleExitItemsAsync_UnsavedCanClose_YieldsAndRemovesItems() {
            RegisterRuntime(isSaved: false);
            RegisterRuntime(isSaved: false);

            var result = new List<IArcTabViewItem>();
            await foreach (var item in _vm.HandleExitItemsAsync()) {
                result.Add(item);
            }

            Assert.AreEqual(2, result.Count);
            Assert.IsEmpty(_vm.TabViewItems);
            _saveCoordinator.Verify(x => x.CanCloseAsync(It.IsAny<IRuntime>(), false, false), Times.Exactly(2));
        }

        [TestMethod]
        public async Task HandleExitItemsAsync_UnsavedCannotClose_SkipsItem() {
            var runtime = RegisterRuntime(isSaved: false);
            _saveCoordinator
                .Setup(x => x.CanCloseAsync(runtime.Object, false, false))
                .ReturnsAsync(false);

            var result = new List<IArcTabViewItem>();
            await foreach (var item in _vm.HandleExitItemsAsync()) {
                result.Add(item);
            }

            Assert.IsEmpty(result);
            Assert.AreEqual(1, _vm.TabViewItems.Count);
        }

        [TestMethod]
        public async Task HandleExitItemsAsync_MixedSavedAndUnsaved_YieldsOnlyUnsavedClosableItems() {
            RegisterRuntime(isSaved: true);
            RegisterRuntime(isSaved: false);

            var result = new List<IArcTabViewItem>();
            await foreach (var item in _vm.HandleExitItemsAsync()) {
                result.Add(item);
            }

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, _vm.TabViewItems.Count);
            _saveCoordinator.Verify(x => x.CanCloseAsync(It.IsAny<IRuntime>(), false, false), Times.Once);
        }
    }
}
