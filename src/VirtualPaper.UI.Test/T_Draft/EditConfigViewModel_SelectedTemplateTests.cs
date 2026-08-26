using VirtualPaper.EditPanel.ViewModels;
using VirtualPaper.Models.EditPanel;

namespace VirtualPaper.UI.Test.T_Draft {
    [TestClass]
    public class EditConfigViewModel_SelectedTemplateTests {
        private EditConfigViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            _vm = new EditConfigViewModel();
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
