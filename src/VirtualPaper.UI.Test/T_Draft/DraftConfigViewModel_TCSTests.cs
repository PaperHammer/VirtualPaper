using VirtualPaper.DraftPanel.Model;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.Models.DraftPanel;

namespace VirtualPaper.UI.Test.T_Draft {
    [TestClass]
    public class DraftConfigViewModel_TCSTests {
        private DraftConfigViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            _vm = new DraftConfigViewModel();
            _vm.ProjectName = "MyProject";
            _vm.SelectedTemplate = new ProjectTemplate { Name = "Template1" };
            _vm.IsFromWorkSpace_AddProj = true;
        }

        [TestMethod]
        public async Task OnNextStepClickedAsync_WorkSpaceMode_TCSSetWithData() {
            var tcs = new TaskCompletionSource<PreProjectData[]?>();
            _vm.DraftConfigTCS = tcs;

            await _vm.OnNextStepClickedAsync();

            Assert.IsTrue(tcs.Task.IsCompleted);
            var result = await tcs.Task;
            Assert.IsNotNull(result);
            Assert.AreEqual("MyProject", result[0].Identity);
        }

        [TestMethod]
        public async Task OnPreviousStepClickedAsync_WorkSpaceMode_TCSSetWithNull() {
            var tcs = new TaskCompletionSource<PreProjectData[]?>();
            _vm.DraftConfigTCS = tcs;

            await _vm.OnPreviousStepClickedAsync();

            Assert.IsTrue(tcs.Task.IsCompleted);
            Assert.IsNull(await tcs.Task);
        }
    }
}
