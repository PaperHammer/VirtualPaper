using Moq;
using VirtualPaper.Common.Utils.Storage.Adapter;
using VirtualPaper.WpSettingsPanel.ViewModels;
using Windows.Storage;

namespace VirtualPaper.UI.Test.T_WpSettings {
    [TestClass]
    public class AddToLibViewModelTests {
        private AddToLibViewModel _vm = null!;
        private Mock<IStoragePicker> _storagePicker = null!;

        [TestInitialize]
        public void Setup() {
            _storagePicker = new Mock<IStoragePicker>();

            _vm = new AddToLibViewModel(_storagePicker.Object);
        }

        // ── Command 初始化 ─────────────────────────────────────────────

        [TestMethod]
        public void HandleAddFilesCommand_IsNotNull_AfterConstruction() {
            Assert.IsNotNull(_vm.HandleAddFilesCommand);
        }

        [TestMethod]
        public void HandleAddFoldersCommand_IsNotNull_AfterConstruction() {
            Assert.IsNotNull(_vm.HandleAddFoldersCommand);
        }

        [TestMethod]
        public void HandleAddFilesCommand_CanExecute_ReturnsTrue() {
            Assert.IsTrue(_vm.HandleAddFilesCommand!.CanExecute(null));
        }

        [TestMethod]
        public void HandleAddFoldersCommand_CanExecute_ReturnsTrue() {
            Assert.IsTrue(_vm.HandleAddFoldersCommand!.CanExecute(null));
        }

        // ── IsElevated ─────────────────────────────────────────────────

        [TestMethod]
        public void IsElevated_IsAssigned_AfterConstruction() {
            // UAC.IsElevated 为静态，只能验证属性已被赋值（bool 默认 false）
            // 在非提权环境下通常为 false，仅做类型安全断言
            var _ = _vm.IsElevated; // 不抛异常即通过
            Assert.IsInstanceOfType(_vm.IsElevated, typeof(bool));
        }

        // ── AddWallpaperFiles → OnRequestAddFile 事件 ──────────────────

        [TestMethod]
        public void AddWallpaperFiles_WhenCalled_RaisesOnRequestAddFileEvent() {
            IReadOnlyList<IStorageItem>? received = null;
            _vm.OnRequestAddFile += (_, e) => received = e;

            var files = new List<IStorageItem>().AsReadOnly();
            _vm.AddWallpaperFiles(files);

            Assert.IsNotNull(received);
        }

        [TestMethod]
        public void AddWallpaperFiles_WhenCalled_PassesCorrectFilesInEvent() {
            IReadOnlyList<IStorageItem>? received = null;
            _vm.OnRequestAddFile += (_, e) => received = e;

            var mockItem = new Mock<IStorageItem>();
            var files = new List<IStorageItem> { mockItem.Object }.AsReadOnly();
            _vm.AddWallpaperFiles(files);

            Assert.AreSame(files, received);
        }

        [TestMethod]
        public void AddWallpaperFiles_WhenNoSubscriber_DoesNotThrow() {
            var files = new List<IStorageItem>().AsReadOnly();

            // 无订阅者，不应抛出 NullReferenceException
            _vm.AddWallpaperFiles(files);
        }

        [TestMethod]
        public void AddWallpaperFiles_SenderIsViewModel() {
            object? senderReceived = null;
            _vm.OnRequestAddFile += (s, _) => senderReceived = s;

            _vm.AddWallpaperFiles(new List<IStorageItem>().AsReadOnly());

            Assert.AreSame(_vm, senderReceived);
        }

        // ── AddWallpaperFolder → OnRequestAddFolder 事件 ───────────────

        [TestMethod]
        public void AddWallpaperFolder_WhenCalled_RaisesOnRequestAddFolderEvent() {
            IStorageFolder? received = null;
            _vm.OnRequestAddFolder += (_, e) => received = e;

            var mockFolder = new Mock<IStorageFolder>();
            _vm.AddWallpaperFolder(mockFolder.Object);

            Assert.IsNotNull(received);
        }

        [TestMethod]
        public void AddWallpaperFolder_WhenCalled_PassesCorrectFolderInEvent() {
            IStorageFolder? received = null;
            _vm.OnRequestAddFolder += (_, e) => received = e;

            var mockFolder = new Mock<IStorageFolder>();
            _vm.AddWallpaperFolder(mockFolder.Object);

            Assert.AreSame(mockFolder.Object, received);
        }

        [TestMethod]
        public void AddWallpaperFolder_WhenNoSubscriber_DoesNotThrow() {
            var mockFolder = new Mock<IStorageFolder>();

            _vm.AddWallpaperFolder(mockFolder.Object);
        }

        [TestMethod]
        public void AddWallpaperFolder_SenderIsViewModel() {
            object? senderReceived = null;
            _vm.OnRequestAddFolder += (s, _) => senderReceived = s;

            var mockFolder = new Mock<IStorageFolder>();
            _vm.AddWallpaperFolder(mockFolder.Object);

            Assert.AreSame(_vm, senderReceived);
        }

        // ── 多订阅者 ────────────────────────────────────────────────────

        [TestMethod]
        public void AddWallpaperFiles_WhenMultipleSubscribers_AllReceiveEvent() {
            int callCount = 0;
            _vm.OnRequestAddFile += (_, _) => callCount++;
            _vm.OnRequestAddFile += (_, _) => callCount++;

            _vm.AddWallpaperFiles(new List<IStorageItem>().AsReadOnly());

            Assert.AreEqual(2, callCount);
        }

        [TestMethod]
        public void AddWallpaperFolder_WhenMultipleSubscribers_AllReceiveEvent() {
            int callCount = 0;
            _vm.OnRequestAddFolder += (_, _) => callCount++;
            _vm.OnRequestAddFolder += (_, _) => callCount++;

            _vm.AddWallpaperFolder(new Mock<IStorageFolder>().Object);

            Assert.AreEqual(2, callCount);
        }

        // ── FileBrowseActionAsync ──────────────────────────────────────

        [TestMethod]
        public async Task FileBrowseActionAsync_WhenPickerReturnsNull_DoesNotRaiseEvent() {
            var picker = new Mock<IStoragePicker>();
            picker.Setup(p => p.PickFilesAsync(It.IsAny<IntPtr>(), It.IsAny<string[]>(), It.IsAny<bool>()))
                  .ReturnsAsync((IStorageItem[]?)null);
            var vm = new AddToLibViewModel(picker.Object);

            bool raised = false;
            vm.OnRequestAddFile += (_, _) => raised = true;

            await InvokeFileBrowseAsync(vm);

            Assert.IsFalse(raised);
        }

        [TestMethod]
        public async Task FileBrowseActionAsync_WhenPickerReturnsEmpty_DoesNotRaiseEvent() {
            var picker = new Mock<IStoragePicker>();
            picker.Setup(p => p.PickFilesAsync(It.IsAny<IntPtr>(), It.IsAny<string[]>(), It.IsAny<bool>()))
                  .ReturnsAsync(Array.Empty<IStorageItem>());
            var vm = new AddToLibViewModel(picker.Object);

            bool raised = false;
            vm.OnRequestAddFile += (_, _) => raised = true;

            await InvokeFileBrowseAsync(vm);

            Assert.IsFalse(raised);
        }

        [TestMethod]
        public async Task FileBrowseActionAsync_WhenPickerReturnsFiles_RaisesOnRequestAddFileEvent() {
            var mockItem = new Mock<IStorageItem>();
            var picker = new Mock<IStoragePicker>();
            picker.Setup(p => p.PickFilesAsync(It.IsAny<IntPtr>(), It.IsAny<string[]>(), It.IsAny<bool>()))
                  .ReturnsAsync(new[] { mockItem.Object });
            var vm = new AddToLibViewModel(picker.Object);

            IReadOnlyList<IStorageItem>? received = null;
            vm.OnRequestAddFile += (_, e) => received = e;

            await InvokeFileBrowseAsync(vm);

            Assert.IsNotNull(received);
            Assert.AreEqual(1, received!.Count);
        }

        // ── FolderBrowseActionAsync ────────────────────────────────────

        [TestMethod]
        public async Task FolderBrowseActionAsync_WhenPickerReturnsNull_DoesNotRaiseEvent() {
            var picker = new Mock<IStoragePicker>();
            picker.Setup(p => p.PickFolderAsync(It.IsAny<IntPtr>()))
                  .ReturnsAsync((IStorageFolder?)null);
            var vm = new AddToLibViewModel(picker.Object);

            bool raised = false;
            vm.OnRequestAddFolder += (_, _) => raised = true;

            await InvokeFolderBrowseAsync(vm);

            Assert.IsFalse(raised);
        }

        [TestMethod]
        public async Task FolderBrowseActionAsync_WhenPickerReturnsFolder_RaisesOnRequestAddFolderEvent() {
            var mockFolder = new Mock<IStorageFolder>();
            var picker = new Mock<IStoragePicker>();
            picker.Setup(p => p.PickFolderAsync(It.IsAny<IntPtr>()))
                  .ReturnsAsync(mockFolder.Object);
            var vm = new AddToLibViewModel(picker.Object);

            IStorageFolder? received = null;
            vm.OnRequestAddFolder += (_, e) => received = e;

            await InvokeFolderBrowseAsync(vm);

            Assert.AreSame(mockFolder.Object, received);
        }

        // ── 私有方法反射调用辅助 ───────────────────────────────────────

        private static Task InvokeFileBrowseAsync(AddToLibViewModel vm) {
            var method = typeof(AddToLibViewModel)
                .GetMethod("FileBrowseActionAsync",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance)!;
            return (Task)method.Invoke(vm, null)!;
        }

        private static Task InvokeFolderBrowseAsync(AddToLibViewModel vm) {
            var method = typeof(AddToLibViewModel)
                .GetMethod("FolderBrowseActionAsync",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance)!;
            return (Task)method.Invoke(vm, null)!;
        }

        // ── FileBrowseActionAsync 扩展名硬编码验证 ────────────────────

        [TestMethod]
        public async Task FileBrowseActionAsync_ExtensionsMatchExpectedExactly() {
            string[]? capturedExtensions = null;
            _storagePicker
                .Setup(p => p.PickFilesAsync(It.IsAny<IntPtr>(), It.IsAny<string[]>(), It.IsAny<bool>()))
                .Callback<IntPtr, string[], bool>((_, exts, _) => capturedExtensions = exts)
                .ReturnsAsync((IStorageItem[]?)null);

            await InvokeFileBrowseAsync(_vm);

            // 只要 FileTypeToExtension 里任何一个后缀被增删改，测试立即失败
            string[] expected = [
                // FImage
                ".jpg", ".jpeg", ".bmp", ".png", ".svg", ".webp",
                // FGif
                ".gif", ".apng",
                // FVideo
                ".mp4", ".webm",
            ];

            Assert.IsNotNull(capturedExtensions);
            CollectionAssert.AreEquivalent(expected, capturedExtensions,
                $"Picker received: [{string.Join(", ", capturedExtensions!)}]\n" +
                $"Expected:        [{string.Join(", ", expected)}]");
        }

        [TestMethod]
        public async Task FileBrowseActionAsync_PassesMultiSelectTrueToPickerAsync() {
            bool? capturedMultiSelect = null;
            _storagePicker
                .Setup(p => p.PickFilesAsync(It.IsAny<IntPtr>(), It.IsAny<string[]>(), It.IsAny<bool>()))
                .Callback<IntPtr, string[], bool>((_, _, multi) => capturedMultiSelect = multi)
                .ReturnsAsync((IStorageItem[]?)null);

            await InvokeFileBrowseAsync(_vm);

            Assert.IsTrue(capturedMultiSelect, "multiSelect should be true.");
        }
    }
}
