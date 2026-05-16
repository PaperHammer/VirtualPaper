using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.Models.DraftPanel;

namespace VirtualPaper.UI.Test.T_Draft {
    [TestClass]
    public class DraftConfigViewModel_ProjectNameTests {
        private DraftConfigViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            _vm = new DraftConfigViewModel();
        }

        [TestMethod]
        public void ProjectName_ValidName_IsNameOkTrue() {
            _vm.ProjectName = "MyProject";
            Assert.IsTrue(_vm.IsNameOk);
        }

        [TestMethod]
        public void ProjectName_NullName_IsNameOkFalse() {
            _vm.ProjectName = null;
            Assert.IsFalse(_vm.IsNameOk);
        }

        [TestMethod]
        public void ProjectName_EmptyName_IsNameOkFalse() {
            _vm.ProjectName = "";
            Assert.IsFalse(_vm.IsNameOk);
        }

        [TestMethod]
        public void ProjectName_ValidName_WithNoTemplate_IsNextEnableFalse() {
            _vm.SelectedTemplate = null;
            _vm.ProjectName = "MyProject";
            Assert.IsFalse(_vm.IsNextEnable);
        }

        [TestMethod]
        public void ProjectName_ValidName_WithTemplate_IsNextEnableTrue() {
            _vm.SelectedTemplate = new ProjectTemplate { Name = "Template1" };
            _vm.ProjectName = "MyProject";
            Assert.IsTrue(_vm.IsNextEnable);
        }

        [TestMethod]
        public void ProjectName_InvalidName_WithTemplate_IsNextEnableFalse() {
            _vm.SelectedTemplate = new ProjectTemplate { Name = "Template1" };
            _vm.ProjectName = "";
            Assert.IsFalse(_vm.IsNextEnable);
        }
    }
}
