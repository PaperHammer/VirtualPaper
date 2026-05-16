using Moq;
using VirtualPaper.IntelligentPanel.Models;
using VirtualPaper.IntelligentPanel.Utils.Interfaces;
using VirtualPaper.IntelligentPanel.ViewModels;

namespace VirtualPaper.UI.Test.T_Intelligent {
    // ====================================================================
    //  IntelligentViewModel.AddTask
    // ====================================================================
    [TestClass]
    public class IntelligentViewModel_AddTaskTests {
        private IntelligentViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            _vm = new IntelligentViewModel();
        }

        [TestMethod]
        [Description("SelectedIntelliPage 为 null 时 AddTask 应返回 false，不调用 page")]
        public void AddTask_WhenPageIsNull_ReturnsFalse() {
            var data = new Mock<IIntelliData>().Object;

            bool result = _vm.AddTask(data);

            Assert.IsFalse(result);
        }

        [TestMethod]
        [Description("data 为 null 时 AddTask 应返回 false，不调用 page")]
        public void AddTask_WhenDataIsNull_ReturnsFalse() {
            _vm.SelectedIntelliPage = new Mock<IIntelligentPage>().Object;

            bool result = _vm.AddTask(null);

            Assert.IsFalse(result);
        }

        [TestMethod]
        [Description("page 和 data 均有效时，应委托给 page.AddTask 并返回其结果（true）")]
        public void AddTask_WhenBothValid_DelegatesAndReturnsTrue() {
            var page = new Mock<IIntelligentPage>();
            var data = new Mock<IIntelliData>().Object;
            page.Setup(p => p.AddTask(data)).Returns(true);
            _vm.SelectedIntelliPage = page.Object;

            bool result = _vm.AddTask(data);

            Assert.IsTrue(result);
            page.Verify(p => p.AddTask(data), Times.Once);
        }

        [TestMethod]
        [Description("page.AddTask 返回 false 时，IntelligentViewModel.AddTask 也应返回 false")]
        public void AddTask_WhenPageReturnsFalse_ReturnsFalse() {
            var page = new Mock<IIntelligentPage>();
            var data = new Mock<IIntelliData>().Object;
            page.Setup(p => p.AddTask(data)).Returns(false);
            _vm.SelectedIntelliPage = page.Object;

            bool result = _vm.AddTask(data);

            Assert.IsFalse(result);
        }
    }
}
