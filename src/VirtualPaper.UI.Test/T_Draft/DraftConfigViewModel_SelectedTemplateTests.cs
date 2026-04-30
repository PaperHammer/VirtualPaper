using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.Models.DraftPanel;

namespace VirtualPaper.UI.Test.T_Draft {
    [TestClass]
    public class DraftConfigViewModel_SelectedTemplateTests {
        private DraftConfigViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            _vm = new DraftConfigViewModel();
        }

        [TestMethod]
        public void SelectedTemplate_SetWithValidName_IsNextEnableTrue() {
            _vm.ProjectName = "MyProject";
            _vm.SelectedTemplate = new ProjectTemplate { Name = "Template1" };
            Assert.IsTrue(_vm.IsNextEnable);
        }

        [TestMethod]
        public void SelectedTemplate_SetToNull_IsNextEnableFalse() {
            _vm.ProjectName = "MyProject";
            _vm.SelectedTemplate = null;
            Assert.IsFalse(_vm.IsNextEnable);
        }

        [TestMethod]
        public void SelectedTemplate_SetWithInvalidName_IsNextEnableFalse() {
            _vm.ProjectName = "";
            _vm.SelectedTemplate = new ProjectTemplate { Name = "Template1" };
            Assert.IsFalse(_vm.IsNextEnable);
        }
    }
}
