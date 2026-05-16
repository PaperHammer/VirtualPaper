using VirtualPaper.IntelligentPanel.ViewModels;

namespace VirtualPaper.UI.Test.T_Intelligent {
    // ====================================================================
    //  SuperResolutionAddTaskViewModel
    //  — 模式互斥 / 倍率联动 / OutputResolutionText / IsNextEnable / Clean
    // ====================================================================
    [TestClass]
    public class SuperResolutionAddTaskViewModelTests {
        private SuperResolutionAddTaskViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            _vm = new SuperResolutionAddTaskViewModel();
        }

        // 默认初始状态

        [TestMethod]
        [Description("构造后默认应为 SuperResolution 模式，倍率为 2x")]
        public void Constructor_DefaultState_IsSuperResolutionAnd2x() {
            Assert.IsFalse(_vm.IsQualityRestoreMode);
            Assert.IsTrue(_vm.IsSuperResolutionMode);
            Assert.IsTrue(_vm.IsMag2x);
            Assert.IsFalse(_vm.IsMag4x);
            Assert.AreEqual(2, _vm.SelectedMagnification);
        }

        [TestMethod]
        [Description("构造后 IsNextEnable 应为 false（尚未选择文件）")]
        public void Constructor_DefaultState_IsNextEnableIsFalse() {
            Assert.IsFalse(_vm.IsNextEnable);
        }

        // 模式互斥

        [TestMethod]
        [Description("切换到 QualityRestore 模式后，IsSuperResolutionMode 应自动变为 false")]
        public void IsQualityRestoreMode_SetTrue_DisablesSuperResolutionMode() {
            _vm.IsQualityRestoreMode = true;

            Assert.IsTrue(_vm.IsQualityRestoreMode);
            Assert.IsFalse(_vm.IsSuperResolutionMode);
        }

        [TestMethod]
        [Description("切换到 SuperResolution 模式后，IsQualityRestoreMode 应自动变为 false")]
        public void IsSuperResolutionMode_SetTrue_DisablesQualityRestoreMode() {
            _vm.IsQualityRestoreMode = true; // 先切到 QR
            _vm.IsSuperResolutionMode = true;

            Assert.IsTrue(_vm.IsSuperResolutionMode);
            Assert.IsFalse(_vm.IsQualityRestoreMode);
        }

        // 倍率互斥与 SelectedMagnification 同步

        [TestMethod]
        [Description("切换到 4x 后 IsMag2x 应自动变 false，SelectedMagnification 为 4")]
        public void IsMag4x_SetTrue_DisablesMag2xAndSetsSelectedMagnification() {
            _vm.IsMag4x = true;

            Assert.IsTrue(_vm.IsMag4x);
            Assert.IsFalse(_vm.IsMag2x);
            Assert.AreEqual(4, _vm.SelectedMagnification);
        }

        [TestMethod]
        [Description("切换回 2x 后 IsMag4x 应自动变 false，SelectedMagnification 为 2")]
        public void IsMag2x_SetTrue_DisablesMag4xAndSetsSelectedMagnification() {
            _vm.IsMag4x = true; // 先选 4x
            _vm.IsMag2x = true;

            Assert.IsTrue(_vm.IsMag2x);
            Assert.IsFalse(_vm.IsMag4x);
            Assert.AreEqual(2, _vm.SelectedMagnification);
        }

        // OutputResolutionText 计算

        [TestMethod]
        [Description("尺寸为 0 时 OutputResolutionText 应为 null")]
        public void OutputResolutionText_WhenNoSourceSize_IsNull() {
            Assert.IsNull(_vm.OutputResolutionText);
        }

        [TestMethod]
        [Description("SuperResolution 2x 时 OutputResolutionText 应为源尺寸 × 2")]
        public void OutputResolutionText_SuperResolution2x_IsDoubled() {
            SetSourceSize(800, 600);

            Assert.AreEqual("1600 * 1200", _vm.OutputResolutionText);
        }

        [TestMethod]
        [Description("SuperResolution 4x 时 OutputResolutionText 应为源尺寸 × 4")]
        public void OutputResolutionText_SuperResolution4x_IsQuadrupled() {
            SetSourceSize(800, 600);
            _vm.IsMag4x = true;

            Assert.AreEqual("3200 * 2400", _vm.OutputResolutionText);
        }

        [TestMethod]
        [Description("QualityRestore 模式时 OutputResolutionText 应与源尺寸相同")]
        public void OutputResolutionText_QualityRestoreMode_EqualsSourceSize() {
            SetSourceSize(1280, 720);
            _vm.IsQualityRestoreMode = true;

            Assert.AreEqual("1280 * 720", _vm.OutputResolutionText);
        }

        // IsNextEnable 验证逻辑

        [TestMethod]
        [Description("设置了 SourceFilePath 且尺寸有效时 IsNextEnable 应为 true")]
        public void IsNextEnable_WhenFilePathAndSizeSet_IsTrue() {
            SetSourceFile(@"C:\test\img.jpg", 1920, 1080);

            Assert.IsTrue(_vm.IsNextEnable);
        }

        [TestMethod]
        [Description("SourceFilePath 设为 null 时 IsNextEnable 应变为 false")]
        public void IsNextEnable_WhenFilePathClearedToNull_IsFalse() {
            SetSourceFile(@"C:\test\img.jpg", 1920, 1080);
            _vm.SourceFilePath = null;

            Assert.IsFalse(_vm.IsNextEnable);
        }

        // Clean

        [TestMethod]
        [Description("Clean 后所有字段应恢复默认值")]
        public void Clean_ResetsAllFieldsToDefault() {
            SetSourceFile(@"C:\test\img.jpg", 1920, 1080);
            _vm.IsMag4x = true;
            _vm.IsQualityRestoreMode = true;

            _vm.Clean();

            Assert.IsNull(_vm.SourceFilePath);
            Assert.IsNull(_vm.SourceFileSize);
            Assert.IsNull(_vm.SourceFileExt);
            Assert.IsNull(_vm.SourceFileResolution);
            Assert.IsFalse(_vm.IsQualityRestoreMode);
            Assert.IsTrue(_vm.IsSuperResolutionMode);
            Assert.IsTrue(_vm.IsMag2x);
            Assert.IsFalse(_vm.IsMag4x);
            Assert.IsNull(_vm.OutputResolutionText);
            Assert.IsFalse(_vm.IsNextEnable);
        }

        // PropertyChanged 通知

        [TestMethod]
        [Description("切换模式应触发 PropertyChanged")]
        public void IsQualityRestoreMode_SetTrue_RaisesPropertyChanged() {
            var changed = new List<string?>();
            _vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            _vm.IsQualityRestoreMode = true;

            Assert.Contains(nameof(_vm.IsQualityRestoreMode), changed);
            Assert.Contains(nameof(_vm.IsSuperResolutionMode), changed);
        }

        [TestMethod]
        [Description("切换倍率应触发 PropertyChanged")]
        public void IsMag4x_SetTrue_RaisesPropertyChanged() {
            var changed = new List<string?>();
            _vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            _vm.IsMag4x = true;

            Assert.Contains(nameof(_vm.IsMag4x), changed);
            Assert.Contains(nameof(_vm.IsMag2x), changed);
        }

        /// <summary>
        /// 注入源图片宽高（通过 internal SetSourceSize），
        /// 并以占位路径触发 OutputResolutionText / IsNextEnable 更新。
        /// </summary>
        private void SetSourceSize(uint width, uint height) {
            _vm.SetSourceSize(width, height);
            _vm.SourceFilePath = @"C:\placeholder\dummy.jpg";
        }

        /// <summary>
        /// 注入源文件路径和宽高，模拟图片已选择后的完整状态。
        /// </summary>
        private void SetSourceFile(string path, uint width, uint height) {
            _vm.SetSourceSize(width, height);
            _vm.SourceFilePath = path;
        }
    }
}
