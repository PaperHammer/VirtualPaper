using Moq;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UIComponent.Navigation.TabView.Interfaces;
using VirtualPaper.UIComponent.Utils.Adapter.Interfaces;
using Workloads.Utils.DraftUtils.Interfaces;

namespace VirtualPaper.UI.Test.T_Draft {
    [TestClass]
    public class WorkSpaceViewModel_DisposeTests {
        private WorkSpaceViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            _vm = new WorkSpaceViewModel(
                Mock.Of<IUserSettingsClient>(),
                Mock.Of<IGlobalDialogService>());
        }

        [TestMethod]
        public void Dispose_ClearsTabViewItems() {
            var mockTabItem1 = new Mock<IArcTabViewItem>();
            var mockTabItem2 = new Mock<IArcTabViewItem>();

            _vm.TabViewItems.Add(mockTabItem1.Object);
            _vm.TabViewItems.Add(mockTabItem2.Object);

            _vm.Dispose();

            Assert.IsEmpty(_vm.TabViewItems);
        }

        [TestMethod]
        public void Dispose_NullsOutCommands() {
            _vm.Dispose();

            Assert.IsNull(_vm.MFI_SaveCommand);
            Assert.IsNull(_vm.MFI_UndoCommand);
            Assert.IsNull(_vm.MFI_RedoCommand);
            Assert.IsNull(_vm.MFI_ManualCommand);
            Assert.IsNull(_vm.MFI_AboutCommand);
        }

        [TestMethod]
        public void Dispose_CalledTwice_DoesNotThrow() {
            _vm.Dispose();
            _vm.Dispose(); // 不应抛异常
        }

        [TestMethod]
        public void Dispose_ClearsRuntimeToArcTabDict() {
            var dict = (Dictionary<IRuntime, (IArcTabViewItemHeader, IArcTabViewItem)>)
                typeof(WorkSpaceViewModel)
                    .GetField("_runtimeToArcTab",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance)!
                    .GetValue(_vm)!;

            var mockHeader = new Mock<IArcTabViewItemHeader>();
            var mockTabItem = new Mock<IArcTabViewItem>();

            dict[Mock.Of<IRuntime>()] = (mockHeader.Object, mockTabItem.Object);

            _vm.Dispose();

            Assert.IsEmpty(dict);
        }
    }
}
