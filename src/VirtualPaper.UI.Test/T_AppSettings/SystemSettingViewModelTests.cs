using System.Globalization;
using Moq;
using VirtualPaper.AppSettingsPanel.ViewModels;
using VirtualPaper.Common.Utils.Storage.Adapter;
using VirtualPaper.Grpc.Client.Interfaces;
using Windows.Storage;

namespace VirtualPaper.UI.Test.T_AppSettings {
    [TestClass]
    public class SystemSettingViewModelTests {
        private Mock<ICommandsClient> _commandsClient = null!;
        private Mock<IStoragePicker> _storagePicker = null!;
        private SystemSettingViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            _commandsClient = new Mock<ICommandsClient>();
            _storagePicker = new Mock<IStoragePicker>();

            _vm = new SystemSettingViewModel(_commandsClient.Object, _storagePicker.Object);
        }

        // ── DebugCommand ─────────────────────────────────────────────

        [TestMethod]
        public void DebugCommand_IsNotNull_AfterConstruction() {
            Assert.IsNotNull(_vm.DebugCommand);
        }

        [TestMethod]
        public void DebugCommand_CanExecute_ReturnsTrue() {
            Assert.IsTrue(_vm.DebugCommand!.CanExecute(null));
        }

        [TestMethod]
        public void DebugCommand_WhenExecuted_CallsShowDebugView() {
            _vm.DebugCommand!.Execute(null);

            _commandsClient.Verify(c => c.ShowDebugView(), Times.Once);
        }

        // ── LogCommand ───────────────────────────────────────────────

        [TestMethod]
        public void LogCommand_IsNotNull_AfterConstruction() {
            Assert.IsNotNull(_vm.LogCommand);
        }

        [TestMethod]
        public void LogCommand_CanExecute_ReturnsTrue() {
            Assert.IsTrue(_vm.LogCommand!.CanExecute(null));
        }

        [TestMethod]
        public async Task InternalExportLogsAsync_PickerCalledOnce() {
            _storagePicker
                .Setup(p => p.PickSaveFileAsync(
                    It.IsAny<IntPtr>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string[]>>()))
                .Returns(Task.FromResult<IStorageFile?>(null));

            await _vm.InternalExportLogsAsync();

            _storagePicker.Verify(p => p.PickSaveFileAsync(
                It.IsAny<IntPtr>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string[]>>()), Times.Once);
        }

        // ── 建议文件名前缀 ────────────────────────────────────────────────

        [TestMethod]
        public async Task InternalExportLogsAsync_SuggestedFileNameStartsWithPrefix() {
            string? capturedName = null;
            _storagePicker
                .Setup(p => p.PickSaveFileAsync(
                    It.IsAny<IntPtr>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string[]>>()))
                .Callback<IntPtr, string, Dictionary<string, string[]>>(
                    (_, name, _) => capturedName = name)
                .Returns(Task.FromResult<IStorageFile?>(null));

            await _vm.InternalExportLogsAsync();

            Assert.IsNotNull(capturedName);
            StringAssert.StartsWith(capturedName, "virtualpaper_log_");
        }

        // ── 建议文件名包含合法时间戳 ──────────────────────────────────────

        [TestMethod]
        public async Task InternalExportLogsAsync_SuggestedFileNameContainsValidTimestamp() {
            string? capturedName = null;
            _storagePicker
                .Setup(p => p.PickSaveFileAsync(
                    It.IsAny<IntPtr>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string[]>>()))
                .Callback<IntPtr, string, Dictionary<string, string[]>>(
                    (_, name, _) => capturedName = name)
                .Returns(Task.FromResult<IStorageFile?>(null));

            await _vm.InternalExportLogsAsync();

            Assert.IsNotNull(capturedName);
            string timestamp = capturedName!["virtualpaper_log_".Length..];
            bool isParsed = DateTime.TryParseExact(
                timestamp,
                "yyyyMMdd_HHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _);
            Assert.IsTrue(isParsed, $"时间戳格式不符，实际值：{timestamp}");
        }

        // ── fileTypeChoices key 为 "Compressed archive" ───────────────────

        [TestMethod]
        public async Task InternalExportLogsAsync_FileTypeChoicesKeyIsCompressedArchive() {
            Dictionary<string, string[]>? capturedChoices = null;
            _storagePicker
                .Setup(p => p.PickSaveFileAsync(
                    It.IsAny<IntPtr>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string[]>>()))
                .Callback<IntPtr, string, Dictionary<string, string[]>>(
                    (_, _, choices) => capturedChoices = choices)
                .Returns(Task.FromResult<IStorageFile?>(null));

            await _vm.InternalExportLogsAsync();

            Assert.IsNotNull(capturedChoices);
            Assert.IsTrue(
                capturedChoices!.ContainsKey("Compressed archive"),
                "fileTypeChoices 中未找到键 'Compressed archive'");
        }

        // ── fileTypeChoices 包含 .zip ─────────────────────────────────────

        [TestMethod]
        public async Task InternalExportLogsAsync_FileTypeChoicesContainZip() {
            Dictionary<string, string[]>? capturedChoices = null;
            _storagePicker
                .Setup(p => p.PickSaveFileAsync(
                    It.IsAny<IntPtr>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string[]>>()))
                .Callback<IntPtr, string, Dictionary<string, string[]>>(
                    (_, _, choices) => capturedChoices = choices)
                .Returns(Task.FromResult<IStorageFile?>(null));

            await _vm.InternalExportLogsAsync();

            Assert.IsNotNull(capturedChoices);
            Assert.IsTrue(
                capturedChoices!.Values.Any(exts => exts.Contains(".zip")),
                "fileTypeChoices 中未找到 .zip");
        }

        // ── Picker 返回 null 时方法也返回 null ────────────────────────────

        [TestMethod]
        public async Task InternalExportLogsAsync_WhenPickerReturnsNull_ReturnsNull() {
            _storagePicker
                .Setup(p => p.PickSaveFileAsync(
                    It.IsAny<IntPtr>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string[]>>()))
                .Returns(Task.FromResult<IStorageFile?>(null));

            IStorageFile? result = await _vm.InternalExportLogsAsync();

            Assert.IsNull(result);
        }

        // ── Picker 返回文件时方法透传该文件 ──────────────────────────────

        [TestMethod]
        public async Task InternalExportLogsAsync_WhenPickerReturnsFile_ReturnsSameFile() {
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.log");
            await File.WriteAllTextAsync(tempPath, string.Empty);

            try {
                var expectedFile = await StorageFile.GetFileFromPathAsync(tempPath);

                _storagePicker
                    .Setup(p => p.PickSaveFileAsync(
                        It.IsAny<IntPtr>(),
                        It.IsAny<string>(),
                        It.IsAny<Dictionary<string, string[]>>()))
                    .ReturnsAsync(expectedFile);

                IStorageFile? result = await _vm.InternalExportLogsAsync();

                Assert.AreSame(expectedFile, result);
            }
            finally {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
    }
}
