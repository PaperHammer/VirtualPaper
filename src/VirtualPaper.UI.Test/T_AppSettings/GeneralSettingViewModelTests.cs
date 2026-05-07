using Grpc.Core;
using Moq;
using VirtualPaper.AppSettingsPanel.ViewModels;
using VirtualPaper.Common;
using VirtualPaper.Common.Events;
using VirtualPaper.Common.Utils.Localization;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.UI.Test.Utils;

namespace VirtualPaper.UI.Test.T_AppSettings {
    [TestClass]
    public class GeneralSettingViewModelTests {
        private Mock<IAppUpdaterClient> _appUpdater = null!;
        private Mock<IUserSettingsClient> _userSettingsClient = null!;
        private Mock<IWallpaperControlClient> _wpControlClient = null!;
        private Mock<ISettings> _settings = null!;
        private GeneralSettingViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            CrossThreadInvoker.Initialize(new T_UiSynchronizationContext());

            _appUpdater = new Mock<IAppUpdaterClient>();
            _userSettingsClient = new Mock<IUserSettingsClient>();
            _wpControlClient = new Mock<IWallpaperControlClient>();
            _settings = new Mock<ISettings>();

            _settings.SetupProperty(s => s.IsAutoStart, false);
            _settings.SetupProperty(s => s.SystemBackdrop, AppSystemBackdrop.Default);
            _settings.SetupProperty(s => s.Language, "en-US");
            _settings.SetupProperty(s => s.WallpaperDir, @"C:\Wallpapers");

            _userSettingsClient.Setup(u => u.Settings).Returns(_settings.Object);
            _userSettingsClient
                .Setup(u => u.SaveAsync<ISettings>())
                .Returns(Task.CompletedTask);

            _vm = new GeneralSettingViewModel(
                _appUpdater.Object,
                _userSettingsClient.Object,
                _wpControlClient.Object);
        }

        [TestCleanup]
        public void Cleanup() {
            _vm.Dispose();
        }

        // ── MenuUpdate ───────────────────────────────────────────────

        [TestMethod]
        public void MenuUpdate_WhenUptodate_SetsUptoNewest() {
            _appUpdater.Raise(a => a.UpdateChecked += null,
                new AppUpdaterEventArgs(
                    AppUpdateStatus.Uptodate,
                    new Version(1, 0),
                    DateTime.Now,
                    new Uri("https://example.com/update"),
                    new Uri("https://example.com/update.sha"),
                    string.Empty));

            Assert.AreEqual(VersionState.UptoNewest, _vm.CurrentVersionState);
        }

        [TestMethod]
        public void MenuUpdate_WhenAvailable_SetsVersionAndFindNew() {
            var version = new Version(2, 0);
            _appUpdater.Raise(a => a.UpdateChecked += null,
                new AppUpdaterEventArgs(
                    AppUpdateStatus.Available,
                    version,
                    DateTime.Now,
                    new Uri("https://example.com/update"),
                    new Uri("https://example.com/update.sha"),
                    string.Empty));

            Assert.AreEqual(VersionState.FindNew, _vm.CurrentVersionState);
            Assert.AreEqual("v2.0", _vm.Version);
        }

        [TestMethod]
        [DataRow(AppUpdateStatus.Invalid)]
        [DataRow(AppUpdateStatus.Error)]
        public void MenuUpdate_WhenInvalidOrError_SetsUpdateErr(AppUpdateStatus status) {
            _appUpdater.Raise(a => a.UpdateChecked += null,
                new AppUpdaterEventArgs(
                    status,
                    new Version(1, 0),
                    DateTime.Now,
                    new Uri("https://example.com/update"),
                    new Uri("https://example.com/update.sha"),
                    string.Empty));

            Assert.AreEqual(VersionState.UpdateErr, _vm.CurrentVersionState);
        }

        // ── IsAutoStart setter ───────────────────────────────────────

        [TestMethod]
        public void IsAutoStart_WhenValueUnchanged_DoesNotCallSave() {
            _settings.Object.IsAutoStart = true;
            _vm.IsAutoStart = true;

            _userSettingsClient.Verify(u => u.SaveAsync<ISettings>(), Times.Never);
        }

        [TestMethod]
        public void IsAutoStart_WhenValueChanged_CallsSave() {
            _settings.Object.IsAutoStart = false;
            _vm.IsAutoStart = true;

            _userSettingsClient.Verify(u => u.SaveAsync<ISettings>(), Times.Once);
        }

        // ── SeletedSystemBackdropIndx setter ─────────────────────────

        [TestMethod]
        public void SeletedSystemBackdropIndx_WhenValueUnchanged_DoesNotCallSave() {
            _settings.Object.SystemBackdrop = AppSystemBackdrop.Mica; // index = 1
            _vm.SeletedSystemBackdropIndx = 1;

            _userSettingsClient.Verify(u => u.SaveAsync<ISettings>(), Times.Never);
        }

        [TestMethod]
        public void SeletedSystemBackdropIndx_WhenValueChanged_CallsSave() {
            _settings.Object.SystemBackdrop = AppSystemBackdrop.Default; // index = 0
            _vm.SeletedSystemBackdropIndx = 1;

            _userSettingsClient.Verify(u => u.SaveAsync<ISettings>(), Times.Once);
        }

        // ── SelectedLanguage setter ───────────────────────────────────

        [TestMethod]
        public void SelectedLanguage_WhenLanguageCodeMatches_DoesNotUpdate() {
            _settings.Object.Language = "en-US";
            var lang = new LanguagesModel("English", new[] { "en-US" });
            _vm.SelectedLanguage = lang;

            _userSettingsClient.Verify(u => u.SaveAsync<ISettings>(), Times.Never);
        }

        [TestMethod]
        public void SelectedLanguage_WhenLanguageCodeDiffers_UpdatesAndSaves() {
            _settings.Object.Language = "en-US";
            var lang = new LanguagesModel("Chinese", new[] { "zh-CN" });
            _vm.SelectedLanguage = lang;

            Assert.AreEqual("zh-CN", _settings.Object.Language);
            _userSettingsClient.Verify(u => u.SaveAsync<ISettings>(), Times.Once);
        }

        // ── WallpaperDirectoryChangeAsync ────────────────────────────

        [TestMethod]
        public async Task WallpaperDirectoryChangeAsync_WhenSucceeds_UpdatesWallpaperDir() {
            var srcRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var destRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            // 预期的最终 WallpaperDir
            var expectedWallpaperDir = Path.Combine(destRoot, Constants.FolderName.WpStoreFolderName);

            try {
                // ── 构造源目录结构 ──────────────────────────────────────
                var subDir = Directory.CreateDirectory(Path.Combine(srcRoot, "wp001"));
                var thumbnailPath = Path.Combine(subDir.FullName, "thumbnail.png");
                var filePath = Path.Combine(subDir.FullName, "wallpaper.mp4");
                await File.WriteAllBytesAsync(thumbnailPath, [0xFF, 0xD8]);
                await File.WriteAllBytesAsync(filePath, [0x00]);

                var basicData = new WpBasicData { /* ... */ FolderName = "wp001", FolderPath = subDir.FullName };
                await JsonSaver.SaveAsync(
                    Path.Combine(subDir.FullName, Constants.Field.WpBasicDataFileName),
                    basicData, WpBasicDataContext.Default);

                // 目标根目录需要存在（让 CopyDirectory 能创建子目录）
                Directory.CreateDirectory(destRoot);

                // ── Setup ───────────────────────────────────────────────
                _settings.Object.WallpaperDir = srcRoot;

                _wpControlClient
                    .Setup(w => w.ChangeWallpaperLayoutFolderPathAsync(
                        It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(Task.CompletedTask);
                _wpControlClient
                    .Setup(w => w.RestartAllWallpapersAsync())
                    .ReturnsAsync(new Grpc_RestartWallpaperResponse { IsFinished = true });

                // ── 执行 ────────────────────────────────────────────────
                await InvokeWallpaperDirectoryChangeAsync(_vm, destRoot);

                // ── 断言 ────────────────────────────────────────────────
                Assert.AreEqual(expectedWallpaperDir, _vm.WallpaperDir);   // ← 修正点
                Assert.IsFalse(_vm.WallpaperDirectoryChangeOngoing);
            }
            finally {
                if (Directory.Exists(srcRoot)) Directory.Delete(srcRoot, true);
                if (Directory.Exists(destRoot)) Directory.Delete(destRoot, true);
            }
        }

        [TestMethod]
        public async Task WallpaperDirectoryChangeAsync_WhenRpcCancelled_OngoingRestored() {
            _wpControlClient
                .Setup(w => w.RestartAllWallpapersAsync())
                .ThrowsAsync(new RpcException(new Status(StatusCode.Cancelled, "")));

            await InvokeWallpaperDirectoryChangeAsync(_vm, @"D:\NewPath");

            Assert.IsFalse(_vm.WallpaperDirectoryChangeOngoing);
        }

        [TestMethod]
        public async Task WallpaperDirectoryChangeAsync_WhenExceptionThrown_OngoingRestored() {
            _wpControlClient
                .Setup(w => w.RestartAllWallpapersAsync())
                .ThrowsAsync(new Exception("unexpected"));

            await InvokeWallpaperDirectoryChangeAsync(_vm, @"D:\NewPath");

            Assert.IsFalse(_vm.WallpaperDirectoryChangeOngoing);
        }

        // ── GetWpBasicDataByInstallFoldersAsync ───────────────────────

        [TestMethod]
        public async Task GetWpBasicData_WhenFolderHasValidData_YieldsItems() {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var subDir = Directory.CreateDirectory(Path.Combine(tempDir, "wp001"));

            // 创建缩略图占位文件（ThumbnailPath 需指向真实存在的文件）
            var thumbnailPath = Path.Combine(subDir.FullName, "thumbnail.png");
            await File.WriteAllBytesAsync(thumbnailPath, [0xFF, 0xD8]); // 最小占位内容

            // 创建壁纸主文件占位
            var filePath = Path.Combine(subDir.FullName, "wallpaper.mp4");
            await File.WriteAllBytesAsync(filePath, [0x00]);

            var basicData = new WpBasicData {// ── IsAvailable() 要求的四个字段 ──────────────────────
                WallpaperUid = Guid.NewGuid().ToString(),
                FType = FileType.FVideo,
                ThumbnailPath = thumbnailPath,
                AppInfo = new ApplicationInfo { AppVersion = "1.0.0" },

                // ── 路径相关 ──────────────────────────────────────────
                FolderName = "wp001",
                FolderPath = subDir.FullName,
                FilePath = filePath,

                // ── 可选描述字段 ──────────────────────────────────────
                Title = "Test Wallpaper",
                Status = WallpaperStatus.Normal,
            };
            await JsonSaver.SaveAsync(
                Path.Combine(subDir.FullName, Constants.Field.WpBasicDataFileName),
                basicData,
                WpBasicDataContext.Default);

            var results = new List<WpLibData>();
            await foreach (var item in InvokeGetWpBasicData([tempDir])) {
                results.Add(item);
            }

            Assert.HasCount(1, results);

            Directory.Delete(tempDir, true);
        }

        [TestMethod]
        public async Task GetWpBasicData_WhenFolderEmpty_YieldsNothing() {
            var tempDir = Directory.CreateDirectory(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;

            var results = new List<WpLibData>();
            await foreach (var item in InvokeGetWpBasicData([tempDir])) {
                results.Add(item);
            }

            Assert.HasCount(0, results);

            Directory.Delete(tempDir, true);
        }

        // ── 辅助方法 ─────────────────────────────────────────────────

        /// <summary>
        /// 通过反射调用 internal WallpaperDirectoryChangeAsync
        /// （推荐改为 internal + InternalsVisibleTo 替代反射）
        /// </summary>
        private static async Task InvokeWallpaperDirectoryChangeAsync(
            GeneralSettingViewModel vm, string newPath) {
            var method = typeof(GeneralSettingViewModel)
                .GetMethod("WallpaperDirectoryChangeAsync",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

            Assert.IsNotNull(method, "WallpaperDirectoryChangeAsync 方法未找到，请确认方法名或改为 internal");

            var task = (Task)method.Invoke(vm, [newPath])!;
            await task;
        }

        private static async IAsyncEnumerable<WpLibData> InvokeGetWpBasicData(
            IEnumerable<string> folders) {
            var method = typeof(GeneralSettingViewModel)
                .GetMethod("GetWpBasicDataByInstallFoldersAsync",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Static);

            Assert.IsNotNull(method, "GetWpBasicDataByInstallFoldersAsync 方法未找到");

            var result = (IAsyncEnumerable<WpLibData>)method.Invoke(null, [folders.ToList()])!;
            await foreach (var item in result) {
                yield return item;
            }
        }
    }
}
