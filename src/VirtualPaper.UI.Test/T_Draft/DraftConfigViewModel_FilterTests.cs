using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.DraftPanel.ViewModels;
using VirtualPaper.Models.DraftPanel;
using VirtualPaper.Models.Mvvm;
using VirtualPaper.UI.Test.Utils;

namespace VirtualPaper.UI.Test.T_Draft {
    [TestClass]
    public class DraftConfigViewModel_FilterTests {
        private DraftConfigViewModel _vm = null!;


        [TestInitialize]
        public void Setup() {
            CrossThreadInvoker.Initialize(new T_UiSynchronizationContext());
            _vm = new DraftConfigViewModel();

            var templates = new List<ProjectTemplate> {
            new() { Name = "Image Template" },
            new() { Name = "Video Template" },
            new() { Name = "Audio Config" },
        };

            // 初始化内部 _availableTemplates 和 AvailableTemplates
            // 需要 InitContentAsync 或直接反射/internal 赋值
            _vm.AvailableTemplates.SetRange(templates);
            typeof(DraftConfigViewModel)
                .GetField("_availableTemplates",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(_vm, templates.AsEnumerable());
        }

        [TestMethod]
        public void ApplyFilter_MatchingKeyword_OnlyMatchingTemplatesRemain() {
            _vm.ApplyFilter("Template");

            Assert.HasCount(2, _vm.AvailableTemplates);
            Assert.IsTrue(_vm.AvailableTemplates.All(t => t.Name!.Contains("Template")));
        }

        [TestMethod]
        public void ApplyFilter_EmptyKeyword_AllTemplatesShown() {
            _vm.ApplyFilter("Template"); // 先过滤
            _vm.ApplyFilter("");         // 再清空过滤

            Assert.HasCount(3, _vm.AvailableTemplates);
        }

        [TestMethod]
        public void ApplyFilter_NoMatchingKeyword_CollectionEmpty() {
            _vm.ApplyFilter("xyz_no_match");

            Assert.HasCount(0, _vm.AvailableTemplates);
        }

        [TestMethod]
        public void ApplyFilter_CaseInsensitive_Matches() {
            _vm.ApplyFilter("template"); // 小写

            Assert.HasCount(2, _vm.AvailableTemplates);
        }
    }
}
