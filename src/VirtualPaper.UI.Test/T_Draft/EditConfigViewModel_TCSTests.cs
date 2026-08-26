using VirtualPaper.EditPanel.Model;
using VirtualPaper.EditPanel.ViewModels;
using VirtualPaper.Models.EditPanel;

namespace VirtualPaper.UI.Test.T_Draft {
    [TestClass]
    public class EditConfigViewModel_TCSTests {
        private EditConfigViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            _vm = new EditConfigViewModel();
            _vm.ProjectName = "MyProject";
            _vm.SelectedTemplate = new ProjectTemplate { Name = "Template1" };
            _vm.IsFromWorkSpaceForAddProj = true;
        }

        [TestMethod]
        public async Task OnNextStepClickedAsync_WorkSpaceMode_TCSSetWithData() {
            var tcs = new TaskCompletionSource<PreProjectData[]?>();
            _vm.EditConfigTCS = tcs;

            await _vm.OnNextStepClickedAsync();

            Assert.IsTrue(tcs.Task.IsCompleted);
            var result = await tcs.Task;
            Assert.IsNotNull(result);
            Assert.AreEqual("MyProject", result[0].Identity);
        }

        [TestMethod]
        public async Task OnPreviousStepClickedAsync_WorkSpaceMode_DoesNotCloseAddProjectFlow() {
            var tcs = new TaskCompletionSource<PreProjectData[]?>();
            _vm.EditConfigTCS = tcs;

            await _vm.OnPreviousStepClickedAsync();

            Assert.IsFalse(tcs.Task.IsCompleted);
        }
    }
}
