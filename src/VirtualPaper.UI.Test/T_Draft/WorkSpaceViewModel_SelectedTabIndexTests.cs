using Moq;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UIComponent.Utils.Adapter.Interfaces;

namespace VirtualPaper.UI.Test.T_Draft {
    [TestClass]
    public class WorkSpaceViewModel_SelectedTabIndexTests {
        private WorkSpaceViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            _vm = new WorkSpaceViewModel(
                Mock.Of<IUserSettingsClient>(),
                Mock.Of<IGlobalDialogService>());
        }

        [TestMethod]
        public void SelectedTabIndex_SameValue_DoesNotFirePropertyChanged() {
            _vm.SelectedTabIndex = 0;
            int changeCount = 0;
            _vm.PropertyChanged += (_, e) => {
                if (e.PropertyName == nameof(_vm.SelectedTabIndex)) changeCount++;
            };

            _vm.SelectedTabIndex = 0;

            Assert.AreEqual(0, changeCount);
        }

        [TestMethod]
        public void SelectedTabIndex_DifferentValue_FiresPropertyChanged() {
            _vm.SelectedTabIndex = 0;
            int changeCount = 0;
            _vm.PropertyChanged += (_, e) => {
                if (e.PropertyName == nameof(_vm.SelectedTabIndex)) changeCount++;
            };

            _vm.SelectedTabIndex = 1;

            Assert.AreEqual(1, changeCount);
        }

        [TestMethod]
        public void SelectedTabIndex_DefaultValue_IsMinusOne() {
            Assert.AreEqual(-1, _vm.SelectedTabIndex);
        }
    }
}
