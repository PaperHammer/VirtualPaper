using Moq;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Storage.Adapter;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Grpc.Client;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UI.Test.Utils;
using VirtualPaper.WpSettingsPanel.Utils;
using VirtualPaper.WpSettingsPanel.ViewModels;

namespace VirtualPaper.UI.Test.T_WpSettings {
    [TestClass]
    public class LibraryContentsViewModelTests {
        private Mock<IUserSettingsClient> _userSettingsClient = null!;
        private Mock<IWallpaperControlClient> _wpControlClient = null!;
        private Mock<WallpaperIndexService> _wallpaperIndexService = null!;
        private Mock<IMonitorManagerClient> _monitorManagerClient = null!;
        private Mock<ISettings> _settings = null!;
        private LibraryContentsViewModel _vm = null!;
        private Mock<IStoragePicker> _storagePicker = null!;
        private Mock<IMonitor> _primaryMonitor = null!;

        [TestInitialize]
        public void Setup() {
            CrossThreadInvoker.Initialize(new T_UiSynchronizationContext());
            _userSettingsClient = new Mock<IUserSettingsClient>();
            _wpControlClient = new Mock<IWallpaperControlClient>();
            _wallpaperIndexService = new Mock<WallpaperIndexService>();
            _settings = new Mock<ISettings>();
            
            _settings.SetupProperty(s => s.WallpaperDir, @"C:\Wallpapers");
            _userSettingsClient.Setup(u => u.Settings).Returns(_settings.Object);
            
            SetupWpSettings();

            _vm = new LibraryContentsViewModel(
                _userSettingsClient.Object,
                _wpControlClient.Object,
                CreateVm(),
                _wallpaperIndexService.Object);
        }

        private void SetupWpSettings() {
            _monitorManagerClient = new Mock<IMonitorManagerClient>();
            _primaryMonitor = new Mock<IMonitor>();
            _storagePicker = new Mock<IStoragePicker>();

            _settings.SetupProperty(s => s.WallpaperArrangement, WallpaperArrangement.Per);
            _userSettingsClient.Setup(u => u.Settings).Returns(_settings.Object);

            _primaryMonitor.Setup(m => m.CloneWithPrimaryInfo()).Returns(_primaryMonitor.Object);
            _primaryMonitor.SetupProperty(m => m.Content);
            _primaryMonitor.SetupProperty(m => m.SystemIndex, 0);
            _monitorManagerClient.Setup(m => m.PrimaryMonitor).Returns(_primaryMonitor.Object);
        }

        private WpSettingsViewModel CreateVm(IEnumerable<IMonitor>? monitors = null) {
            var list = monitors ?? new[] { _primaryMonitor.Object };
            _monitorManagerClient.Setup(m => m.Monitors).Returns(list.ToList().AsReadOnly());
            return new WpSettingsViewModel(
                _monitorManagerClient.Object,
                _wpControlClient.Object,
                _userSettingsClient.Object,
                _storagePicker.Object);
        }

        // ── FilterByTitle ─────────────────────────────────────────────

        [TestMethod]
        public void FilterByTitle_WhenKeywordMatches_ShowsMatchingItems() {
            var match = MakeWpData("uid-1", "Nature Wallpaper");
            var noMatch = MakeWpData("uid-2", "City Night");
            PopulateLibrary(match, noMatch);

            _vm.FilterByTitle("Nature");

            Assert.HasCount(1, _vm.LibraryWallpapers);
            Assert.AreSame(match, _vm.LibraryWallpapers[0]);
        }

        [TestMethod]
        public void FilterByTitle_WhenKeywordEmpty_ShowsAllItems() {
            var a = MakeWpData("uid-1", "Alpha");
            var b = MakeWpData("uid-2", "Beta");
            PopulateLibrary(a, b);

            _vm.FilterByTitle(string.Empty);

            Assert.HasCount(2, _vm.LibraryWallpapers);
        }

        [TestMethod]
        public void FilterByTitle_IsCaseInsensitive() {
            var item = MakeWpData("uid-1", "Sunset");
            PopulateLibrary(item);

            _vm.FilterByTitle("sunset");

            Assert.HasCount(1, _vm.LibraryWallpapers);
        }

        [TestMethod]
        public void FilterByTitle_WhenNoMatch_LibraryWallpapersIsEmpty() {
            var item = MakeWpData("uid-1", "Forest");
            PopulateLibrary(item);

            _vm.FilterByTitle("Ocean");

            Assert.HasCount(0, _vm.LibraryWallpapers);
        }

        [TestMethod]
        public void FilterByTitle_WhenCalledTwice_SecondFilterOverridesFirst() {
            var a = MakeWpData("uid-1", "Mountain");
            var b = MakeWpData("uid-2", "Desert");
            PopulateLibrary(a, b);

            _vm.FilterByTitle("Mountain");
            _vm.FilterByTitle("Desert");

            Assert.HasCount(1, _vm.LibraryWallpapers);
            Assert.AreSame(b, _vm.LibraryWallpapers[0]);
        }

        // ── FilterKeyword ─────────────────────────────────────────────

        [TestMethod]
        public void FilterKeyword_DefaultValue_IsLibraryTitle() {
            Assert.AreEqual(FilterKey.LibraryTitle, _vm.FilterKeyword);
        }

        // ── ApplyFilter ───────────────────────────────────────────────

        [TestMethod]
        public void ApplyFilter_DelegatesToFilterByTitle() {
            var item = MakeWpData("uid-1", "Cherry Blossom");
            PopulateLibrary(item);

            _vm.ApplyFilter("Cherry");

            Assert.HasCount(1, _vm.LibraryWallpapers);
        }

        // ── HandleDelete ──────────────────────────────────────────────

        [TestMethod]
        public void HandleDelete_RemovesFromLibraryWallpapers() {
            var item = MakeWpData("uid-1", "Galaxy");
            PopulateLibrary(item);

            InvokeHandleDelete(item);

            Assert.HasCount(0, _vm.LibraryWallpapers);
        }

        [TestMethod]
        public void HandleDelete_CallsWallpaperIndexServiceRemove() {
            var item = MakeWpData("uid-1", "Galaxy");
            PopulateLibrary(item);

            InvokeHandleDelete(item);

            _wallpaperIndexService.Verify(s => s.Remove(item), Times.Once);
        }

        [TestMethod]
        public void HandleDelete_WhenItemNotExists_DoesNotThrow() {
            var item = MakeWpData("uid-99", "Ghost");

            // 不 populate，直接删除
            InvokeHandleDelete(item);

            _wallpaperIndexService.Verify(s => s.Remove(item), Times.Once);
        }

        // ── UpdateLib (TryGetValue 命中) ──────────────────────────────

        [TestMethod]
        public void UpdateLib_WhenIndexExists_ReplacesItemAtIndex() {
            var original = MakeWpData("uid-1", "Old Title");
            PopulateLibrary(original);

            _wallpaperIndexService
                .Setup(s => s.TryGetValue("uid-1", out It.Ref<int>.IsAny))
                .Returns((string _, ref int idx) => { idx = 0; return true; });

            var updated = MakeWpData("uid-1", "New Title");
            InvokeUpdateLib(updated);

            Assert.AreEqual("New Title", _vm.LibraryWallpapers[0].Title);
            _wallpaperIndexService.Verify(s => s.Update(updated), Times.Once);
        }

        [TestMethod]
        public void UpdateLib_WhenIndexNotExists_InsertsAtFront() {
            _wallpaperIndexService
                .Setup(s => s.TryGetValue(It.IsAny<string>(), out It.Ref<int>.IsAny))
                .Returns((string _, ref int idx) => { idx = -1; return false; });

            var newItem = MakeWpData("uid-new", "Brand New");
            InvokeUpdateLib(newItem);

            Assert.HasCount(1, _vm.LibraryWallpapers);
            Assert.AreSame(newItem, _vm.LibraryWallpapers[0]);
            _wallpaperIndexService.Verify(s => s.Update(newItem), Times.Once);
        }

        // ── IsFileInPreview ───────────────────────────────────────────

        [TestMethod]
        public void IsFileInPreview_WhenNotInPreview_ReturnsFalse() {
            var data = MakeWpData("uid-1", "Test");

            var result = InvokeIsFileInPreview(data);

            Assert.IsFalse(result);
        }

        // ── IsFileInUseAsync ──────────────────────────────────────────

        [TestMethod]
        public async Task IsFileInUseAsync_WhenLayoutContainsFolderPath_ReturnsTrue() {
            var data = MakeWpData("uid-1", "Test", folderPath: @"C:\Wallpapers\uid-1");
            var layout = MakeWallpaperLayout(@"C:\Wallpapers\uid-1");

            _userSettingsClient.Setup(u => u.LoadAsync<List<IWallpaperLayout>>())
                .Returns(Task.CompletedTask);
            _userSettingsClient.Setup(u => u.WallpaperLayouts)
                .Returns(new List<IWallpaperLayout> { layout });

            var result = await InvokeIsFileInUseAsync(data);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task IsFileInUseAsync_WhenLayoutDoesNotContainFolderPath_ReturnsFalse() {
            var data = MakeWpData("uid-1", "Test", folderPath: @"C:\Wallpapers\uid-1");
            var layout = MakeWallpaperLayout(@"C:\Wallpapers\other");

            _userSettingsClient.Setup(u => u.LoadAsync<List<IWallpaperLayout>>())
                .Returns(Task.CompletedTask);
            _userSettingsClient.Setup(u => u.WallpaperLayouts)
                .Returns(new List<IWallpaperLayout> { layout });

            var result = await InvokeIsFileInUseAsync(data);

            Assert.IsFalse(result);
        }

        // ── LibLoadingStatus property ─────────────────────────────────

        [TestMethod]
        public void LibLoadingStatus_WhenSet_RaisesPropertyChanged() {
            bool raised = false;
            _vm.PropertyChanged += (_, e) => {
                if (e.PropertyName == nameof(_vm.LibLoadingStatus)) raised = true;
            };

            _vm.LibLoadingStatus = LoadingStatus.Changing;

            Assert.IsTrue(raised);
        }

        // ── 辅助方法 ──────────────────────────────────────────────────

        /// <summary>将数据同时写入 LibraryWallpapers 和内部 _libraryWallpapers</summary>
        private void PopulateLibrary(params IWpBasicData[] items) {
            // 通过反射访问私有字段 _libraryWallpapers
            var privateList = (List<IWpBasicData>)typeof(LibraryContentsViewModel)
                .GetField("_libraryWallpapers",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(_vm)!;

            foreach (var item in items) {
                _vm.LibraryWallpapers.Add(item);
                privateList.Add(item);
            }
        }

        private void InvokeHandleDelete(IWpBasicData data) {
            typeof(LibraryContentsViewModel)
                .GetMethod("HandleDelete",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(_vm, new object[] { data });
        }

        private void InvokeUpdateLib(IWpBasicData data) {
            typeof(LibraryContentsViewModel)
                .GetMethod("UpdateLib",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(_vm, new object[] { data });
        }

        private bool InvokeIsFileInPreview(IWpBasicData data) {
            return (bool)typeof(LibraryContentsViewModel)
                .GetMethod("IsFileInPreview",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(_vm, new object[] { data })!;
        }

        private async Task<bool> InvokeIsFileInUseAsync(IWpBasicData data) {
            var task = (Task<bool>)typeof(LibraryContentsViewModel)
                .GetMethod("IsFileInUseAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(_vm, new object[] { data })!;
            return await task;
        }

        private static IWpBasicData MakeWpData(
            string uid,
            string title,
            string folderPath = @"C:\Wallpapers\default",
            FileType ftype = FileType.FImage) {
            var mock = new Mock<IWpBasicData>();
            mock.Setup(d => d.WallpaperUid).Returns(uid);
            mock.Setup(d => d.Title).Returns(title);
            mock.Setup(d => d.FolderPath).Returns(folderPath);
            mock.Setup(d => d.FType).Returns(ftype);
            mock.Setup(d => d.IsAvailable()).Returns(true);
            return mock.Object;
        }

        private static IWallpaperLayout MakeWallpaperLayout(string folderPath) {
            var mock = new Mock<IWallpaperLayout>();
            mock.Setup(l => l.FolderPath).Returns(folderPath);
            return mock.Object;
        }
    }
}
