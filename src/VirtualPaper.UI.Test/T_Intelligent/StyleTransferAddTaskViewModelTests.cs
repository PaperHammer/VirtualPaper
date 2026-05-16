using VirtualPaper.IntelligentPanel.ViewModels;

namespace VirtualPaper.UI.Test.T_Intelligent {
    // ====================================================================
    //  StyleTransferAddTaskViewModel
    //  — IsNextEnable 联动 / StyleOptions 初始化 / Clean
    // ====================================================================
    [TestClass]
    public class StyleTransferAddTaskViewModelTests {
        private StyleTransferAddTaskViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            _vm = new StyleTransferAddTaskViewModel();
        }

        // 默认初始状态

        [TestMethod]
        [Description("构造后 IsNextEnable 应为 false")]
        public void Constructor_DefaultState_IsNextEnableIsFalse() {
            Assert.IsFalse(_vm.IsNextEnable);
        }

        [TestMethod]
        [Description("构造后 StyleOptions 不应为空，且最后一项应为 Custom")]
        public void Constructor_StyleOptions_NotEmptyAndLastIsCustom() {
            Assert.IsNotNull(_vm.StyleOptions);
            Assert.IsNotEmpty(_vm.StyleOptions, "StyleOptions should contain preset items");
            Assert.IsTrue(_vm.StyleOptions[^1].IsCustom, "Last option should be Custom");
        }

        [TestMethod]
        [Description("构造后 SelectedStyle 默认为 Custom（最后一项）")]
        public void Constructor_SelectedStyle_DefaultIsCustom() {
            Assert.IsNotNull(_vm.SelectedStyle);
            Assert.IsTrue(_vm.SelectedStyle.IsCustom, "Default SelectedStyle should be Custom");
        }

        // IsNextEnable 联动逻辑

        [TestMethod]
        [Description("设置有效 SourceFilePath 且 SelectedStyle.ImagePath 也有效时，IsNextEnable 应为 true")]
        public void IsNextEnable_WhenBothSourceAndStyleSet_IsTrue() {
            // 先切换到有内置 ImagePath 的预设风格（第 0 项：Anime）
            _vm.SelectedStyle = _vm.StyleOptions[0];
            _vm.SourceFilePath = @"C:\test\src.jpg";

            Assert.IsTrue(_vm.IsNextEnable);
        }

        [TestMethod]
        [Description("SourceFilePath 为 null 时，即使 SelectedStyle 有效，IsNextEnable 也应为 false")]
        public void IsNextEnable_WhenSourceFilePathIsNull_IsFalse() {
            _vm.SelectedStyle = _vm.StyleOptions[0]; // 有效的预设
            _vm.SourceFilePath = null;

            Assert.IsFalse(_vm.IsNextEnable);
        }

        [TestMethod]
        [Description("SelectedStyle 的 ImagePath 为 null 时，IsNextEnable 应为 false")]
        public void IsNextEnable_WhenSelectedStyleImagePathIsNull_IsFalse() {
            _vm.SourceFilePath = @"C:\test\src.jpg";
            // Custom 默认 ImagePath = null，所以保持默认即可
            Assert.IsTrue(_vm.SelectedStyle.IsCustom && _vm.SelectedStyle.ImagePath == null,
                "Precondition: Custom style should have null ImagePath");

            Assert.IsFalse(_vm.IsNextEnable);
        }

        // SelectedStyle setter

        [TestMethod]
        [Description("切换为 null 时 SelectedStyle 不应发生变化（防御 null 赋值）")]
        public void SelectedStyle_SetNull_DoesNotChangeValue() {
            var original = _vm.SelectedStyle;

            _vm.SelectedStyle = null!;

            Assert.AreEqual(original, _vm.SelectedStyle);
        }

        [TestMethod]
        [Description("切换到相同对象时不应触发 PropertyChanged")]
        public void SelectedStyle_SetSameValue_DoesNotRaisePropertyChanged() {
            var current = _vm.SelectedStyle;
            var changed = false;
            _vm.PropertyChanged += (_, e) => {
                if (e.PropertyName == nameof(_vm.SelectedStyle)) changed = true;
            };

            _vm.SelectedStyle = current; // 同一对象

            Assert.IsFalse(changed, "PropertyChanged should NOT be raised when setting same value");
        }

        [TestMethod]
        [Description("切换到不同风格时应触发 PropertyChanged")]
        public void SelectedStyle_SetNewValue_RaisesPropertyChanged() {
            var changed = false;
            _vm.PropertyChanged += (_, e) => {
                if (e.PropertyName == nameof(_vm.SelectedStyle)) changed = true;
            };

            _vm.SelectedStyle = _vm.StyleOptions[0]; // 切到 Anime（不同于默认 Custom）

            Assert.IsTrue(changed);
        }

        // Clean

        [TestMethod]
        [Description("Clean 后 SourceFilePath / SourceFileSize / SourceFileExt / SourceFileResolution 应为 null")]
        public void Clean_ResetsSourceFields() {
            _vm.SourceFilePath = @"C:\test\src.jpg";
            _vm.SourceFileSize = "1 MB";
            _vm.SourceFileExt = ".jpg";
            _vm.SourceFileResolution = "1920 * 1080";

            _vm.Clean();

            Assert.IsNull(_vm.SourceFilePath);
            Assert.IsNull(_vm.SourceFileSize);
            Assert.IsNull(_vm.SourceFileExt);
            Assert.IsNull(_vm.SourceFileResolution);
        }

        [TestMethod]
        [Description("Clean 后 IsNextEnable 应为 false")]
        public void Clean_ResetsIsNextEnable() {
            _vm.SelectedStyle = _vm.StyleOptions[0];
            _vm.SourceFilePath = @"C:\test\src.jpg";
            Assert.IsTrue(_vm.IsNextEnable, "Precondition");

            _vm.Clean();

            Assert.IsFalse(_vm.IsNextEnable);
        }

        // StyleOptions 内容完整性

        [TestMethod]
        [Description("所有非 Custom 预设风格都应有 ImagePath")]
        public void StyleOptions_PresetStyles_AllHaveImagePath() {
            var presets = _vm.StyleOptions.Where(s => !s.IsCustom).ToArray();

            Assert.IsNotEmpty(presets, "There should be at least one preset");
            Assert.IsTrue(
                presets.All(s => !string.IsNullOrEmpty(s.ImagePath)),
                "All preset styles should have a non-empty ImagePath");
        }

        [TestMethod]
        [Description("所有预设风格都应有 Name")]
        public void StyleOptions_AllStyles_HaveName() {
            // Name 可能因 LanguageUtil 在测试环境返回 key，只要非 null 即可
            Assert.IsTrue(
                _vm.StyleOptions.All(s => s.Name != null),
                "All style options should have a non-null Name");
        }
    }
}
