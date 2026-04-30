using Moq;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UIComponent.Navigation.TabView.Interfaces;
using VirtualPaper.UIComponent.Utils.Adapter.Interfaces;
using Workloads.Utils.DraftUtils.Interfaces;

namespace VirtualPaper.UI.Test.T_Draft {
    [TestClass]
    public class WorkSpaceViewModel_GetSelectedRuntimeTests {
        private WorkSpaceViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            _vm = new WorkSpaceViewModel(
                Mock.Of<IUserSettingsClient>(),
                Mock.Of<IGlobalDialogService>());
        }

        [TestMethod]
        public void GetSelectedRuntime_IndexIsMinusOne_ReturnsNull() {
            _vm.SelectedTabIndex = -1;

            Assert.IsNull(_vm.GetSelectedRuntime());
        }

        [TestMethod]
        public void GetSelectedRuntime_IndexOutOfRange_ReturnsNull() {
            _vm.SelectedTabIndex = 99;

            Assert.IsNull(_vm.GetSelectedRuntime());
        }

        [TestMethod]
        public void GetSelectedRuntime_ValidIndex_ReturnsRuntimeFromTag() {
            var mockRuntime = new Mock<IRuntime>();
            var tabItem = new Mock<IArcTabViewItem>();
            tabItem.SetupProperty(t => t.Tag, mockRuntime.Object);
            _vm.TabViewItems.Add(tabItem.Object);
            _vm.SelectedTabIndex = 0;

            var result = _vm.GetSelectedRuntime();

            Assert.AreSame(mockRuntime.Object, result);
        }

        [TestMethod]
        public void GetSelectedRuntime_TagIsNotIRuntime_ReturnsNull() {
            var tabItem = new Mock<IArcTabViewItem>();
            tabItem.SetupProperty(t => t.Tag, "not_a_runtime");
            _vm.TabViewItems.Add(tabItem.Object);
            _vm.SelectedTabIndex = 0;

            Assert.IsNull(_vm.GetSelectedRuntime());
        }

        [TestMethod]
        public void GetSelectedRuntime_TagIsNull_ReturnsNull() {
            var tabItem = new Mock<IArcTabViewItem>();
            tabItem.SetupProperty(t => t.Tag, null);
            _vm.TabViewItems.Add(tabItem.Object);
            _vm.SelectedTabIndex = 0;

            Assert.IsNull(_vm.GetSelectedRuntime());
        }
    }
}
