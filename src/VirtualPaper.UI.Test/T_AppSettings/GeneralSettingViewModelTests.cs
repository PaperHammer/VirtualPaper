using Grpc.Core;
using Moq;
using VirtualPaper.AppSettingsPanel.ViewModels;
using VirtualPaper.AppSettingsPanel.Utils;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Localization;
using VirtualPaper.Common.Utils.Storage;
using VirtualPaper.Common.Utils.ThreadContext;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.Models.AppUpdate;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Models.Events;
using VirtualPaper.UI.Test.Utils;

namespace VirtualPaper.UI.Test.T_AppSettings {
    [TestClass]
    public class GeneralSettingViewModelTests {
        private Mock<IAppUpdaterClient> _appUpdater = null!;
        private Mock<IUserSettingsClient> _userSettingsClient = null!;
        private Mock<IWallpaperControlClient> _wpControlClient = null!;
        private Mock<ISettings> _settings = null!;
        private Mock<ICommandsClient> _commandsClient = null!;
        private GeneralSettingViewModel _vm = null!;

        [TestInitialize]
        public void Setup() {
            CrossThreadInvoker.Initialize(new TUiSynchronizationContext());

            _appUpdater = new Mock<IAppUpdaterClient>();
            _userSettingsClient = new Mock<IUserSettingsClient>();
            _wpControlClient = new Mock<IWallpaperControlClient>();
            _settings = new Mock<ISettings>();
            _commandsClient = new Mock<ICommandsClient>();

            _settings.SetupProperty(s => s.IsAutoStart, false);
            _settings.SetupProperty(s => s.SystemBackdrop, AppSystemBackdrop.Default);
            _settings.SetupProperty(s => s.NavigationAnimationsEnabled, true);
            _settings.SetupProperty(s => s.Language, "en-US");
            _settings.SetupProperty(s => s.WallpaperDir, @"C:\Wallpapers");
            _settings.SetupProperty(s => s.BuiltInWallpapersDir, @"C:\BuiltInWallpapers");

            _userSettingsClient.Setup(u => u.Settings).Returns(_settings.Object);
            _userSettingsClient
                .Setup(u => u.SaveAsync<ISettings>())
                .Returns(Task.CompletedTask);

            _vm = new GeneralSettingViewModel(
                _appUpdater.Object,
                _userSettingsClient.Object,
                _wpControlClient.Object,
                _commandsClient.Object);
        }

        [TestCleanup]
        public void Cleanup() {
            _vm.Dispose();
        }

        // ── MenuUpdate ───────────────────────────────────────────────

        [TestMethod]
        public void MenuUpdate_WhenUptodate_SetsUptoNewest() {
            var releaseInfo = new ReleaseInfo {
                Version = new Version(1, 0),
                CheckedTime = DateTime.Now,
                InstallerUri = new Uri("https://example.com/update"),
                InstallerShaUri = new Uri("https://example.com/update.sha"),
            };
            _appUpdater.Raise(a => a.UpdateChecked += null,
                new AppUpdaterEventArgs(
                    AppUpdateStatus.Uptodate,
                    releaseInfo));

            Assert.AreEqual(VersionState.UptoNewest, _vm.CurrentVersionState);
        }

        [TestMethod]
        public void MenuUpdate_WhenAvailable_SetsVersionAndFindNew() {
            var version = new Version(2, 0);
            var releaseInfo = new ReleaseInfo {
                Version = version,
                CheckedTime = DateTime.Now,
                InstallerUri = new Uri("https://example.com/update"),
                InstallerShaUri = new Uri("https://example.com/update.sha"),
            };
            _appUpdater.Raise(a => a.UpdateChecked += null,
                new AppUpdaterEventArgs(
                    AppUpdateStatus.Available,
                    releaseInfo));


            Assert.AreEqual(VersionState.FindNew, _vm.CurrentVersionState);
            Assert.AreEqual("v2.0", _vm.Version);
        }

        [TestMethod]
        [DataRow(AppUpdateStatus.Invalid)]
        [DataRow(AppUpdateStatus.Error)]
        public void MenuUpdate_WhenInvalidOrError_SetsUpdateErr(AppUpdateStatus status) {
            var releaseInfo = new ReleaseInfo {
                Version = new Version(1, 0),
                CheckedTime = DateTime.Now,
                InstallerUri = new Uri("https://example.com/update"),
                InstallerShaUri = new Uri("https://example.com/update.sha"),
            };
            _appUpdater.Raise(a => a.UpdateChecked += null,
                new AppUpdaterEventArgs(
                    status,
                    releaseInfo));

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

        [TestMethod]
        public void NavigationAnimations_WhenChanged_SavesAndNotifiesOnlyOnce() {
            int notifications = 0;
            _vm.PropertyChanged += (_, args) => {
                if (args.PropertyName == nameof(_vm.NavigationAnimationsEnabled)) notifications++;
            };

            Assert.IsTrue(_vm.NavigationAnimationsEnabled);
            _vm.NavigationAnimationsEnabled = false;
            _vm.NavigationAnimationsEnabled = false;

            Assert.IsFalse(_settings.Object.NavigationAnimationsEnabled);
            Assert.AreEqual(1, notifications);
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
        public async Task SelectedLanguage_WhenLanguageCodeDiffers_UpdatesAndSaves() {
            _settings.Object.Language = "en-US";
            var lang = new LanguagesModel("Chinese", new[] { "zh-CN" });
            await _vm.ChangeLanguageAsync(lang);

            Assert.AreEqual("zh-CN", _settings.Object.Language);
            _userSettingsClient.Verify(u => u.SaveAsync<ISettings>(), Times.Once);
            Assert.IsTrue(_vm.IsLanguageChangeEnabled);
        }

        [TestMethod]
        public async Task ChangeLanguage_WhenSaveFails_RestoresSelectionAndPreference() {
            var previous = _vm.SelectedLanguage;
            _userSettingsClient.Setup(u => u.SaveAsync<ISettings>()).ThrowsAsync(new IOException("Settings save failed"));

            await _vm.ChangeLanguageAsync(new LanguagesModel("Chinese", ["zh-CN"]));

            Assert.AreEqual("en-US", _settings.Object.Language);
            Assert.AreSame(previous, _vm.SelectedLanguage);
            Assert.IsTrue(_vm.IsLanguageChangeEnabled);
        }

        // ── WallpaperDirectoryChangeAsync ────────────────────────────

        [TestMethod]
        public async Task WallpaperDirectoryChangeAsync_WhenSucceeds_UpdatesWallpaperDir() {
            var sourceLibrary = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var srcRoot = Path.Combine(sourceLibrary, "wallpapers");
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
                _settings.Object.BuiltInWallpapersDir = Constants.CommonPaths.GetBuiltInWallpapersDir(srcRoot);

                _wpControlClient
                    .Setup(w => w.ChangeWallpaperLayoutFolderPathAsync(
                        It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(Task.CompletedTask);
                _wpControlClient
                    .Setup(w => w.RestartAllWallpapersAsync())
                    .ReturnsAsync(new Grpc_RestartWallpaperResponse { IsFinished = true });

                // ── 执行 ────────────────────────────────────────────────
                await _vm.WallpaperDirectoryChangeAsync(destRoot);

                // ── 断言 ────────────────────────────────────────────────
                Assert.AreEqual(expectedWallpaperDir, _vm.WallpaperDir);   // ← 修正点
                Assert.IsFalse(_vm.WallpaperDirectoryChangeOngoing);
            }
            finally {
                if (Directory.Exists(sourceLibrary)) Directory.Delete(sourceLibrary, true);
                if (Directory.Exists(destRoot)) Directory.Delete(destRoot, true);
            }
        }

        [TestMethod]
        public async Task WallpaperDirectoryChangeAsync_WhenRpcCancelled_OngoingRestored() {
            _wpControlClient
                .Setup(w => w.RestartAllWallpapersAsync())
                .ThrowsAsync(new RpcException(new Status(StatusCode.Cancelled, "")));

            await _vm.WallpaperDirectoryChangeAsync(@"D:\NewPath");

            Assert.IsFalse(_vm.WallpaperDirectoryChangeOngoing);
        }

        [TestMethod]
        public async Task WallpaperDirectoryChangeAsync_WhenExceptionThrown_OngoingRestored() {
            _wpControlClient
                .Setup(w => w.RestartAllWallpapersAsync())
                .ThrowsAsync(new Exception("unexpected"));

            await _vm.WallpaperDirectoryChangeAsync(@"D:\NewPath");

            Assert.IsFalse(_vm.WallpaperDirectoryChangeOngoing);
        }

        // ── GetWpBasicDataByInstallFoldersAsync ───────────────────────

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WallpaperDirectoryChange_MovesBothLibraries_AndKeepsSourcesOnFailure(bool failRestart) {
            string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.LibraryMove", Guid.NewGuid().ToString("N"));
            string sourceUser = Path.Combine(root, "source", "wallpapers");
            string sourceBuiltIn = Path.Combine(root, "separate built-ins");
            string destination = Path.Combine(root, "destination");
            string targetUser = Path.Combine(destination, "wallpapers");
            string targetBuiltIn = Constants.CommonPaths.GetBuiltInWallpapersDir(targetUser);
            try {
                CreateMovableWallpaper(sourceUser, "user");
                CreateMovableWallpaper(sourceBuiltIn, "built-in");
                _settings.Object.WallpaperDir = sourceUser;
                _settings.Object.BuiltInWallpapersDir = sourceBuiltIn;
                _wpControlClient.Setup(w => w.ChangeWallpaperLayoutFolderPathAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(Task.CompletedTask);
                _wpControlClient.Setup(w => w.RestartAllWallpapersAsync())
                    .ReturnsAsync(new Grpc_RestartWallpaperResponse { IsFinished = !failRestart });

                await _vm.WallpaperDirectoryChangeAsync(destination);

                Assert.AreEqual(failRestart ? sourceUser : targetUser, _settings.Object.WallpaperDir);
                Assert.AreEqual(failRestart ? sourceBuiltIn : targetBuiltIn, _settings.Object.BuiltInWallpapersDir);
                Assert.AreEqual(failRestart, Directory.Exists(sourceUser));
                Assert.AreEqual(failRestart, Directory.Exists(sourceBuiltIn));
                foreach (var (directory, name) in new[] { (targetUser, "user"), (targetBuiltIn, "built-in") }) {
                    string folder = Path.Combine(directory, name);
                    var basic = WpBasicData.Load(Path.Combine(folder, Constants.Field.WpBasicDataFileName));
                    Assert.AreEqual(Path.Combine(folder, "media.png"), basic.FilePath);
                    CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(basic.FilePath));
                    var runtime = WpRuntimeData.Load(Path.Combine(folder, "display", "RImage", Constants.Field.WpRuntimeDataFileName));
                    Assert.AreEqual(folder, runtime.FolderPath);
                    Assert.AreEqual(Path.Combine(folder, "effect.json"), runtime.WpEffectFilePathUsing);
                    Assert.AreEqual("{\"Intensity\":73}", File.ReadAllText(runtime.WpEffectFilePathUsing));
                }
                _userSettingsClient.Verify(s => s.SaveAsync<ISettings>(), Times.Exactly(failRestart ? 2 : 1));
                _wpControlClient.Verify(w => w.ChangeWallpaperLayoutFolderPathAsync(sourceUser, targetUser), Times.Once);
                _wpControlClient.Verify(w => w.ChangeWallpaperLayoutFolderPathAsync(sourceBuiltIn, targetBuiltIn), Times.Once);
                if (failRestart) {
                    _wpControlClient.Verify(w => w.ChangeWallpaperLayoutFolderPathAsync(targetUser, sourceUser), Times.Once);
                    _wpControlClient.Verify(w => w.ChangeWallpaperLayoutFolderPathAsync(targetBuiltIn, sourceBuiltIn), Times.Once);
                    Assert.AreEqual("{\"Intensity\":73}", File.ReadAllText(Path.Combine(sourceBuiltIn, "built-in", "effect.json")));
                }
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WallpaperDirectoryChange_RejectsOccupiedOrNestedDestinations(bool nested) {
            string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.LibraryMove", Guid.NewGuid().ToString("N"));
            string source = Path.Combine(root, "source", "wallpapers");
            string destination = nested ? Path.Combine(source, "nested") : Path.Combine(root, "destination");
            try {
                CreateMovableWallpaper(source, "user");
                _settings.Object.WallpaperDir = source;
                _settings.Object.BuiltInWallpapersDir = Constants.CommonPaths.GetBuiltInWallpapersDir(source);
                string marker = Path.Combine(destination, "wallpapers", "existing.txt");
                if (!nested) {
                    Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
                    File.WriteAllText(marker, "keep this file");
                }
                await _vm.WallpaperDirectoryChangeAsync(destination);
                Assert.AreEqual(source, _settings.Object.WallpaperDir);
                Assert.IsTrue(File.Exists(Path.Combine(source, "user", "media.png")));
                if (!nested) Assert.AreEqual("keep this file", File.ReadAllText(marker));
                _userSettingsClient.Verify(s => s.SaveAsync<ISettings>(), Times.Never);
                _wpControlClient.Verify(w => w.ChangeWallpaperLayoutFolderPathAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WallpaperDirectoryChange_WhenSecondLibraryPreparationFails_KeepsSourcesAndOriginalSettings(bool invalidMetadata) {
            string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.LibraryMove", Guid.NewGuid().ToString("N"));
            string sourceUser = Path.Combine(root, "source", "wallpapers");
            string sourceBuiltIn = Path.Combine(root, "separate built-ins");
            string destination = Path.Combine(root, "destination");
            string builtInMedia = Path.Combine(sourceBuiltIn, "built-in", "media.png");
            try {
                CreateMovableWallpaper(sourceUser, "user");
                CreateMovableWallpaper(sourceBuiltIn, "built-in");
                _settings.Object.WallpaperDir = sourceUser;
                _settings.Object.BuiltInWallpapersDir = sourceBuiltIn;
                if (invalidMetadata)
                    File.WriteAllText(Path.Combine(sourceBuiltIn, "built-in", "display", "RImage", Constants.Field.WpRuntimeDataFileName), "{invalid");
                using (var locked = invalidMetadata ? null : new FileStream(builtInMedia, FileMode.Open, FileAccess.Read, FileShare.None)) {
                    await _vm.WallpaperDirectoryChangeAsync(destination);
                }

                Assert.AreEqual(sourceUser, _settings.Object.WallpaperDir);
                Assert.AreEqual(sourceBuiltIn, _settings.Object.BuiltInWallpapersDir);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(builtInMedia));
                Assert.IsTrue(File.Exists(Path.Combine(sourceUser, "user", "media.png")));
                // The first library was already copied, but preparation failure must
                // leave both original libraries in place and never switch any paths.
                Assert.IsTrue(File.Exists(Path.Combine(destination, "wallpapers", "user", "media.png")));
                _userSettingsClient.Verify(s => s.SaveAsync<ISettings>(), Times.Never);
                _wpControlClient.Verify(w => w.ChangeWallpaperLayoutFolderPathAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
                _wpControlClient.Verify(w => w.RestartAllWallpapersAsync(), Times.Never);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
        }

        [TestMethod]
        [DataRow("complete")]
        [DataRow("truncated-file")]
        [DataRow("missing-file")]
        [DataRow("missing-directory")]
        [DataRow("extra-file")]
        public void WallpaperDirectorySnapshot_ChecksEntriesAndFileSizes(string scenario) {
            string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.LibraryMove", Guid.NewGuid().ToString("N"));
            string source = Path.Combine(root, "source");
            string target = Path.Combine(root, "target");
            try {
                Directory.CreateDirectory(Path.Combine(source, "empty"));
                Directory.CreateDirectory(Path.Combine(source, "nested"));
                File.WriteAllBytes(Path.Combine(source, "media.bin"), [1, 2, 3]);
                File.WriteAllBytes(Path.Combine(source, "nested", "other.bin"), new byte[65536]);
                File.WriteAllBytes(Path.Combine(source, "zero.bin"), []);
                var snapshot = WallpaperDirectorySnapshot.Capture(source);
                VirtualPaper.Common.Utils.Files.FileUtil.CopyDirectory(source, target, true);
                switch (scenario) {
                    case "truncated-file": File.WriteAllBytes(Path.Combine(target, "media.bin"), [1]); break;
                    case "missing-file": File.Delete(Path.Combine(target, "media.bin")); break;
                    case "missing-directory": Directory.Delete(Path.Combine(target, "empty")); break;
                    case "extra-file": File.WriteAllText(Path.Combine(target, "extra.txt"), "extra"); break;
                }
                if (scenario == "complete") snapshot.VerifyCopy(target);
                else Assert.Throws<IOException>(() => snapshot.VerifyCopy(target));
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(source, "media.bin")));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
        }

        [TestMethod]
        [DataRow("added")]
        [DataRow("modified")]
        [DataRow("removed")]
        public async Task WallpaperDirectoryChange_WhenSourceChangesAfterCopy_RetainsThatSource(string change) {
            string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.LibraryMove", Guid.NewGuid().ToString("N"));
            string sourceUser = Path.Combine(root, "source", "wallpapers");
            string sourceBuiltIn = Path.Combine(root, "separate built-ins");
            string destination = Path.Combine(root, "destination");
            string media = Path.Combine(sourceUser, "user", "media.png");
            string lateFile = Path.Combine(sourceUser, "late-file.txt");
            try {
                CreateMovableWallpaper(sourceUser, "user");
                CreateMovableWallpaper(sourceBuiltIn, "built-in");
                _settings.Object.WallpaperDir = sourceUser;
                _settings.Object.BuiltInWallpapersDir = sourceBuiltIn;
                _wpControlClient.Setup(w => w.ChangeWallpaperLayoutFolderPathAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(Task.CompletedTask);
                _wpControlClient.Setup(w => w.RestartAllWallpapersAsync()).Callback(() => {
                    switch (change) {
                        case "added": File.WriteAllText(lateFile, "keep new data"); break;
                        case "modified":
                            var previousTime = File.GetLastWriteTimeUtc(media);
                            File.WriteAllBytes(media, [3, 2, 1]);
                            File.SetLastWriteTimeUtc(media, previousTime.AddSeconds(5));
                            break;
                        case "removed": File.Delete(media); break;
                    }
                }).ReturnsAsync(new Grpc_RestartWallpaperResponse { IsFinished = true });

                await _vm.WallpaperDirectoryChangeAsync(destination);

                Assert.AreEqual(Path.Combine(destination, "wallpapers"), _settings.Object.WallpaperDir);
                Assert.AreEqual(Path.Combine(destination, "BuiltInWallpapers"), _settings.Object.BuiltInWallpapersDir);
                Assert.IsTrue(Directory.Exists(sourceUser));
                Assert.IsFalse(Directory.Exists(sourceBuiltIn));
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(destination, "wallpapers", "user", "media.png")));
                if (change == "added") Assert.AreEqual("keep new data", File.ReadAllText(lateFile));
                if (change == "modified") CollectionAssert.AreEqual(new byte[] { 3, 2, 1 }, File.ReadAllBytes(media));
                _userSettingsClient.Verify(s => s.SaveAsync<ISettings>(), Times.Once);
                _wpControlClient.Verify(w => w.RestartAllWallpapersAsync(), Times.Once);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
        }

        [TestMethod]
        public async Task WallpaperDirectoryChange_DoesNotDeleteSourceCreatedAfterCopyPhase() {
            string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.LibraryMove", Guid.NewGuid().ToString("N"));
            string sourceUser = Path.Combine(root, "source", "wallpapers");
            string sourceBuiltIn = Path.Combine(root, "late-built-ins");
            string destination = Path.Combine(root, "destination");
            try {
                CreateMovableWallpaper(sourceUser, "user");
                _settings.Object.WallpaperDir = sourceUser;
                _settings.Object.BuiltInWallpapersDir = sourceBuiltIn;
                _wpControlClient.Setup(w => w.ChangeWallpaperLayoutFolderPathAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(Task.CompletedTask);
                _wpControlClient.Setup(w => w.RestartAllWallpapersAsync()).Callback(() => {
                    Directory.CreateDirectory(sourceBuiltIn);
                    File.WriteAllText(Path.Combine(sourceBuiltIn, "new.txt"), "keep new data");
                }).ReturnsAsync(new Grpc_RestartWallpaperResponse { IsFinished = true });

                await _vm.WallpaperDirectoryChangeAsync(destination);

                Assert.AreEqual("keep new data", File.ReadAllText(Path.Combine(sourceBuiltIn, "new.txt")));
                Assert.IsFalse(Directory.Exists(sourceUser));
                Assert.AreEqual(Path.Combine(destination, "wallpapers"), _settings.Object.WallpaperDir);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
        }

        [TestMethod]
        public async Task WallpaperDirectoryChange_WhenSecondLayoutUpdateFails_RestoresBothPathsAndKeepsSources() {
            string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.LibraryMove", Guid.NewGuid().ToString("N"));
            string sourceUser = Path.Combine(root, "source", "wallpapers");
            string sourceBuiltIn = Path.Combine(root, "separate built-ins");
            string destination = Path.Combine(root, "destination");
            string targetUser = Path.Combine(destination, "wallpapers");
            string targetBuiltIn = Path.Combine(destination, "BuiltInWallpapers");
            try {
                CreateMovableWallpaper(sourceUser, "user");
                CreateMovableWallpaper(sourceBuiltIn, "built-in");
                _settings.Object.WallpaperDir = sourceUser;
                _settings.Object.BuiltInWallpapersDir = sourceBuiltIn;
                _wpControlClient.Setup(w => w.ChangeWallpaperLayoutFolderPathAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(Task.CompletedTask);
                _wpControlClient.Setup(w => w.ChangeWallpaperLayoutFolderPathAsync(sourceBuiltIn, targetBuiltIn))
                    .ThrowsAsync(new IOException("Layout update failed."));
                _wpControlClient.Setup(w => w.RestartAllWallpapersAsync())
                    .ReturnsAsync(new Grpc_RestartWallpaperResponse { IsFinished = true });
                _userSettingsClient.Setup(s => s.SaveAsync<ISettings>()).Returns(() => {
                    Assert.AreEqual(sourceUser, _settings.Object.WallpaperDir);
                    Assert.AreEqual(sourceBuiltIn, _settings.Object.BuiltInWallpapersDir);
                    return Task.CompletedTask;
                });

                await _vm.WallpaperDirectoryChangeAsync(destination);

                Assert.AreEqual(sourceUser, _settings.Object.WallpaperDir);
                Assert.AreEqual(sourceBuiltIn, _settings.Object.BuiltInWallpapersDir);
                Assert.IsTrue(File.Exists(Path.Combine(sourceUser, "user", "media.png")));
                Assert.IsTrue(File.Exists(Path.Combine(sourceBuiltIn, "built-in", "media.png")));
                _userSettingsClient.Verify(s => s.SaveAsync<ISettings>(), Times.Once);
                _wpControlClient.Verify(w => w.ChangeWallpaperLayoutFolderPathAsync(targetUser, sourceUser), Times.Once);
                _wpControlClient.Verify(w => w.ChangeWallpaperLayoutFolderPathAsync(targetBuiltIn, sourceBuiltIn), Times.Never);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
        }

        [TestMethod]
        [DataRow("source", "source/nested", true)]
        [DataRow("source/nested", "source", false)]
        [DataRow("source", "SOURCE", true)]
        [DataRow("source", "source-backup", false)]
        [DataRow("source", "destination", false)]
        public void ValidateMigrationDestination_RejectsOnlyDestinationsInsideSource(string source, string target, bool overlaps) {
            string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.LibraryMove", Guid.NewGuid().ToString("N"));
            string destination = Path.Combine(root, target);
            string[] sources = [Path.Combine(root, source)];
            if (overlaps) Assert.Throws<IOException>(() => GeneralSettingViewModel.ValidateMigrationDestination(destination, sources));
            else GeneralSettingViewModel.ValidateMigrationDestination(destination, sources);
        }

        [TestMethod]
        public void ValidateMigrationDestination_ChecksEverySourceLibrary() {
            string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.LibraryMove", Guid.NewGuid().ToString("N"));
            string[] sources = [Path.Combine(root, "source"), Path.Combine(root, "other-source")];
            Assert.Throws<IOException>(() => GeneralSettingViewModel.ValidateMigrationDestination(Path.Combine(sources[1], "nested"), sources));
        }

        [TestMethod]
        public async Task WallpaperDirectoryChange_InvalidPath_DoesNotEscapeOrChangeSettings() {
            string previous = _settings.Object.WallpaperDir;
            await _vm.WallpaperDirectoryChangeAsync("invalid\0path");
            Assert.AreEqual(previous, _settings.Object.WallpaperDir);
            Assert.IsFalse(_vm.WallpaperDirectoryChangeOngoing);
            _userSettingsClient.Verify(s => s.SaveAsync<ISettings>(), Times.Never);
            _wpControlClient.Verify(w => w.ChangeWallpaperLayoutFolderPathAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WallpaperDirectoryChange_AllowsUnrelatedRootContentsAndSourceParent(bool destinationIsSourceParent) {
            string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.LibraryMove", Guid.NewGuid().ToString("N"));
            string sourceUser = Path.Combine(root, "previous", "wallpapers");
            string sourceBuiltIn = Path.Combine(root, "previous-built-ins");
            string destination = destinationIsSourceParent ? root : Path.Combine(root, "destination");
            try {
                CreateMovableWallpaper(sourceUser, "user");
                CreateMovableWallpaper(sourceBuiltIn, "built-in");
                Directory.CreateDirectory(destination);
                string unrelated = Path.Combine(destination, "readme.txt");
                File.WriteAllText(unrelated, "keep unrelated data");
                _settings.Object.WallpaperDir = sourceUser;
                _settings.Object.BuiltInWallpapersDir = sourceBuiltIn;
                _wpControlClient.Setup(w => w.ChangeWallpaperLayoutFolderPathAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(Task.CompletedTask);
                _wpControlClient.Setup(w => w.RestartAllWallpapersAsync())
                    .ReturnsAsync(new Grpc_RestartWallpaperResponse { IsFinished = true });

                await _vm.WallpaperDirectoryChangeAsync(destination);

                Assert.AreEqual("keep unrelated data", File.ReadAllText(unrelated));
                Assert.AreEqual(Path.Combine(destination, "wallpapers"), _settings.Object.WallpaperDir);
                Assert.AreEqual(Path.Combine(destination, "BuiltInWallpapers"), _settings.Object.BuiltInWallpapersDir);
                Assert.IsTrue(File.Exists(Path.Combine(destination, "wallpapers", "user", "media.png")));
                Assert.IsTrue(File.Exists(Path.Combine(destination, "BuiltInWallpapers", "built-in", "media.png")));
                Assert.IsFalse(Directory.Exists(sourceUser));
                Assert.IsFalse(Directory.Exists(sourceBuiltIn));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
        }

        [TestMethod]
        public async Task WallpaperDirectoryChange_ReselectCurrentRoot_DoesNothing() {
            string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.LibraryMove", Guid.NewGuid().ToString("N"));
            try {
                string user = Path.Combine(root, "wallpapers");
                string builtIn = Path.Combine(root, "BuiltInWallpapers");
                CreateMovableWallpaper(user, "user");
                CreateMovableWallpaper(builtIn, "built-in");
                _settings.Object.WallpaperDir = user;
                _settings.Object.BuiltInWallpapersDir = builtIn;
                await _vm.WallpaperDirectoryChangeAsync(root);
                Assert.IsTrue(File.Exists(Path.Combine(user, "user", "media.png")));
                Assert.IsTrue(File.Exists(Path.Combine(builtIn, "built-in", "media.png")));
                Assert.IsFalse(_vm.WallpaperDirectoryChangeOngoing);
                _userSettingsClient.Verify(s => s.SaveAsync<ISettings>(), Times.Never);
                _wpControlClient.Verify(w => w.ChangeWallpaperLayoutFolderPathAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
        }

        [TestMethod]
        public async Task WallpaperDirectoryChange_RejectsTargetContainingAnotherSource() {
            string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.LibraryMove", Guid.NewGuid().ToString("N"));
            string sourceUser = Path.Combine(root, "source", "wallpapers");
            string destination = Path.Combine(root, "destination");
            string sourceBuiltIn = Path.Combine(destination, "wallpapers", "existing-library");
            try {
                CreateMovableWallpaper(sourceUser, "user");
                CreateMovableWallpaper(sourceBuiltIn, "built-in");
                _settings.Object.WallpaperDir = sourceUser;
                _settings.Object.BuiltInWallpapersDir = sourceBuiltIn;
                await _vm.WallpaperDirectoryChangeAsync(destination);
                Assert.AreEqual(sourceUser, _settings.Object.WallpaperDir);
                Assert.AreEqual(sourceBuiltIn, _settings.Object.BuiltInWallpapersDir);
                Assert.IsTrue(File.Exists(Path.Combine(sourceUser, "user", "media.png")));
                Assert.IsTrue(File.Exists(Path.Combine(sourceBuiltIn, "built-in", "media.png")));
                _userSettingsClient.Verify(s => s.SaveAsync<ISettings>(), Times.Never);
                _wpControlClient.Verify(w => w.ChangeWallpaperLayoutFolderPathAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
        }

        [TestMethod]
        public async Task WallpaperDirectoryChange_RejectsFileAtLibraryTargetWithoutOverwritingIt() {
            string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.LibraryMove", Guid.NewGuid().ToString("N"));
            string sourceUser = Path.Combine(root, "source", "wallpapers");
            string destination = Path.Combine(root, "destination");
            try {
                CreateMovableWallpaper(sourceUser, "user");
                Directory.CreateDirectory(destination);
                string existing = Path.Combine(destination, "wallpapers");
                File.WriteAllText(existing, "keep existing file");
                _settings.Object.WallpaperDir = sourceUser;
                _settings.Object.BuiltInWallpapersDir = Path.Combine(root, "absent-built-ins");
                await _vm.WallpaperDirectoryChangeAsync(destination);
                Assert.AreEqual("keep existing file", File.ReadAllText(existing));
                Assert.IsTrue(File.Exists(Path.Combine(sourceUser, "user", "media.png")));
                _userSettingsClient.Verify(s => s.SaveAsync<ISettings>(), Times.Never);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
        }

        [TestMethod]
        [DataRow(-1)]
        [DataRow(0)]
        [DataRow(1)]
        public void WallpaperDirectorySnapshot_ValidatesLongestCompletePath(int difference) {
            string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.LibraryMove", Guid.NewGuid().ToString("N"));
            const string relative = "images/cover.png";
            try {
                Directory.CreateDirectory(Path.Combine(root, "images"));
                File.WriteAllText(Path.Combine(root, relative), "image");
                var snapshot = WallpaperDirectorySnapshot.Capture(root);
                int targetLength = WallpaperDirectorySnapshot.MaxMigrationPathLength - relative.Length - 1 + difference;
                string destination = @"C:\" + new string('d', targetLength - 3);
                if (difference > 0) Assert.Throws<PathTooLongException>(() => snapshot.ValidateDestinationPathLength(destination));
                else snapshot.ValidateDestinationPathLength(destination);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
        }

        [TestMethod]
        public async Task WallpaperDirectoryChange_RejectsDeepPathBeforeCopyingFirstLibrary() {
            string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.LibraryMove", Guid.NewGuid().ToString("N"));
            string sourceUser = Path.Combine(root, "source", "wallpapers");
            string sourceBuiltIn = Path.Combine(root, "built-ins");
            string destination = Path.Combine(root, new string('d', 100));
            try {
                CreateMovableWallpaper(sourceUser, "user");
                CreateMovableWallpaper(sourceBuiltIn, "built-in");
                string deep = Path.Combine(sourceBuiltIn, "built-in", "nested");
                Directory.CreateDirectory(deep);
                string media = Path.Combine(deep, new string('m', 80) + ".bin");
                File.WriteAllText(media, "keep original data");
                _settings.Object.WallpaperDir = sourceUser;
                _settings.Object.BuiltInWallpapersDir = sourceBuiltIn;
                await _vm.WallpaperDirectoryChangeAsync(destination);
                Assert.IsFalse(Directory.Exists(destination));
                Assert.AreEqual("keep original data", File.ReadAllText(media));
                Assert.AreEqual(sourceUser, _settings.Object.WallpaperDir);
                _userSettingsClient.Verify(s => s.SaveAsync<ISettings>(), Times.Never);
                _wpControlClient.Verify(w => w.ChangeWallpaperLayoutFolderPathAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
        }

        private static void CreateMovableWallpaper(string directory, string name) {
            string folder = Path.Combine(directory, name);
            Directory.CreateDirectory(folder);
            string media = Path.Combine(folder, "media.png");
            string effect = Path.Combine(folder, "effect.json");
            File.WriteAllBytes(media, [1, 2, 3]);
            File.WriteAllText(effect, "{\"Intensity\":73}");
            var basic = new WpBasicData {
                WallpaperUid = Guid.NewGuid().ToString("N"), FType = FileType.FImage,
                FolderName = name, FolderPath = folder, FilePath = media, ThumbnailPath = media,
                AppInfo = new ApplicationInfo { AppVersion = "1.0.0", FileVersion = "18" }
            };
            JsonSaver.Save(Path.Combine(folder, Constants.Field.WpBasicDataFileName), basic, WpBasicDataContext.Default);
            var runtime = new WpRuntimeData {
                FolderPath = folder, MonitorContent = "display", RType = RuntimeType.RImage,
                WpEffectFilePathTemplate = effect, WpEffectFilePathUsing = effect, WpEffectFilePathTemporary = effect,
                AppInfo = new ApplicationInfo { AppVersion = "1.0.0", FileVersion = "18" }
            };
            JsonSaver.Save(Path.Combine(folder, "display", "RImage", Constants.Field.WpRuntimeDataFileName), runtime, WpRuntimeDataContext.Default);
        }

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
            };
            await JsonSaver.SaveAsync(
                Path.Combine(subDir.FullName, Constants.Field.WpBasicDataFileName),
                basicData,
                WpBasicDataContext.Default);

            var results = new List<WpLibData>();
            await foreach (var item in GeneralSettingViewModel.GetWpBasicDataByInstallFoldersAsync([tempDir])) {
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
            await foreach (var item in GeneralSettingViewModel.GetWpBasicDataByInstallFoldersAsync([tempDir])) {
                results.Add(item);
            }

            Assert.HasCount(0, results);

            Directory.Delete(tempDir, true);
        }
    }
}
