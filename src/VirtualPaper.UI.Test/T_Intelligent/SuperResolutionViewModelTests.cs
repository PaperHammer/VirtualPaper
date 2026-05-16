using VirtualPaper.IntelligentPanel.Models;
using VirtualPaper.IntelligentPanel.ViewModels;

namespace VirtualPaper.UI.Test.T_Intelligent {
    // ====================================================================
    //  SuperResolutionViewModel — AddTask / HasTasks / RemoveTask / Dispose
    // ====================================================================
    [TestClass]
    public class SuperResolutionViewModel_AddTaskTests {
        private SuperResolutionViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            _vm = new SuperResolutionViewModel();
        }

        [TestCleanup]
        public void Cleanup() {
            _vm.Dispose();
        }

        // 数据验证：null / 空路径

        [TestMethod]
        [Description("data 为 null 时 AddTask 应返回 false，Tasks 不变")]
        public void AddTask_WhenDataIsNull_ReturnsFalse() {
            bool result = _vm.AddTask(null!);

            Assert.IsFalse(result);
            Assert.HasCount(0, _vm.Tasks);
        }

        [TestMethod]
        [Description("SourceFilePath 为空时 AddTask 应返回 false")]
        public void AddTask_WhenSourceFilePathIsEmpty_ReturnsFalse() {
            var data = MakeData(sourcePath: string.Empty);

            bool result = _vm.AddTask(data);

            Assert.IsFalse(result);
        }

        // 正常添加

        [TestMethod]
        [Description("有效数据时 AddTask 应返回 true 并把任务加入 Tasks")]
        public void AddTask_WithValidData_ReturnsTrueAndAddsToTasks() {
            var data = MakeData();

            bool result = _vm.AddTask(data);

            Assert.IsTrue(result);
            Assert.HasCount(1, _vm.Tasks);
        }

        [TestMethod]
        [Description("新增第一个任务后 HasTasks 应变为 true")]
        public void AddTask_FirstTask_HasTasksBecomesTrue() {
            Assert.IsFalse(_vm.HasTasks, "Precondition: HasTasks should be false before adding");

            _vm.AddTask(MakeData());

            Assert.IsTrue(_vm.HasTasks);
        }

        [TestMethod]
        [Description("每次 AddTask 任务的命令属性不应为 null")]
        public void AddTask_TaskItem_CommandsAreNotNull() {
            _vm.AddTask(MakeData());

            var item = _vm.Tasks[0];
            Assert.IsNotNull(item.RemoveCommand, "RemoveCommand should not be null");
            Assert.IsNotNull(item.PreviewCommand, "PreviewCommand should not be null");
            Assert.IsNotNull(item.SaveCommand, "SaveCommand should not be null");
            Assert.IsNotNull(item.ImportCommand, "ImportCommand should not be null");
        }

        [TestMethod]
        [Description("添加多个任务时，Tasks.Count 应与添加次数一致")]
        public void AddTask_MultipleTasks_CountMatchesAddCount() {
            _vm.AddTask(MakeData("file1.jpg"));
            _vm.AddTask(MakeData("file2.jpg"));
            _vm.AddTask(MakeData("file3.jpg"));

            Assert.HasCount(3, _vm.Tasks);
        }

        // HasTasks 联动

        [TestMethod]
        [Description("HasTasks 应随 PropertyChanged 同步更新")]
        public void AddTask_RaisesPropertyChangedForHasTasks() {
            var changedProps = new List<string?>();
            _vm.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

            _vm.AddTask(MakeData());

            Assert.Contains(nameof(_vm.HasTasks), changedProps, "PropertyChanged should be raised for HasTasks");
        }

        [TestMethod]
        [Description("Dispose 应清空 Tasks 集合")]
        public void Dispose_ClearsTasks() {
            _vm.AddTask(MakeData());
            Assert.HasCount(1, _vm.Tasks, "Precondition");

            _vm.Dispose();

            Assert.HasCount(0, _vm.Tasks);
        }

        [TestMethod]
        [Description("Dispose 连续调用不应抛出异常")]
        public void Dispose_CalledTwice_DoesNotThrow() {
            _vm.Dispose();
            _vm.Dispose(); // 第二次调用不应抛出
        }

        private static SuperResolutionData MakeData(string sourcePath = @"C:\test\src.jpg") =>
            new SuperResolutionData(
                sourceFilePath: sourcePath,
                sourceFileSize: "1 MB",
                sourceFileExt: ".jpg",
                width: 1920, height: 1080,
                mode: EnhanceMode.SuperResolution,
                magnification: 2,
                targetWidth: 3840, targetHeight: 2160);
    }
}
