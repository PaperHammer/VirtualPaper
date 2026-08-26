using VirtualPaper.EditPanel.ViewModels;
using VirtualPaper.Models.EditPanel;

namespace VirtualPaper.UI.Test.T_Draft {
    [TestClass]
    public class EditConfigViewModel_CardUIStateChangedTests {
        private EditConfigViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            _vm = new EditConfigViewModel();
        }

        [TestMethod]
        public void IsNextEnable_Changed_CardUIStateChangedInvoked() {
            int invokeCount = 0;
            _vm.CardUIStateChanged = () => invokeCount++;

            _vm.ProjectName = "MyProject";
            _vm.SelectedTemplate = new ProjectTemplate { Name = "Template1" };

            Assert.IsGreaterThan(0, invokeCount);
        }

        [TestMethod]
        public void IsNextEnable_SameValue_CardUIStateChangedStillInvoked() {
            // IsNextEnable setter 没有防重复触发，每次赋值都会调用
            int invokeCount = 0;
            _vm.CardUIStateChanged = () => invokeCount++;

            _vm.IsNextEnable = false;
            _vm.IsNextEnable = false;

            Assert.AreEqual(2, invokeCount);
        }
    }
}
