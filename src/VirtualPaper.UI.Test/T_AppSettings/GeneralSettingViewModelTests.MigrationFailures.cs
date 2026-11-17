using Moq;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.UI.Test.T_AppSettings;

public partial class GeneralSettingViewModelTests {
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WallpaperDirectoryChange_AfterCommitFailure_KeepsNewPathsAndContinuesCleanup(bool notificationFails) {
        string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.LibraryMove", Guid.NewGuid().ToString("N"));
        string sourceUser = Path.Combine(root, "source", "wallpapers");
        string sourceBuiltIn = Path.Combine(root, "source", "BuiltInWallpapers");
        string destination = Path.Combine(root, "destination");
        string targetUser = Path.Combine(destination, "wallpapers");
        string targetBuiltIn = Path.Combine(destination, "BuiltInWallpapers");
        string media = Path.Combine(sourceUser, "user", "media.png");
        FileStream? locked = null;
        int notifications = 0;
        try {
            CreateMovableWallpaper(sourceUser, "user");
            CreateMovableWallpaper(sourceBuiltIn, "built-in");
            _settings.Object.WallpaperDir = sourceUser;
            _settings.Object.BuiltInWallpapersDir = sourceBuiltIn;
            _wpControlClient.Setup(client => client.ChangeWallpaperLayoutFolderPathAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _wpControlClient.Setup(client => client.RestartAllWallpapersAsync())
                .ReturnsAsync(new Grpc_RestartWallpaperResponse { IsFinished = true });
            _vm.WallpaperInstallDirChanged += (_, _) => {
                notifications++;
                if (notificationFails) throw new IOException("A subscriber failed after commit");
                // Deny deletion only after copying, saving and restoring have succeeded.
                locked = new FileStream(media, FileMode.Open, FileAccess.Read, FileShare.Read);
            };

            await _vm.WallpaperDirectoryChangeAsync(destination);

            Assert.AreEqual(targetUser, _settings.Object.WallpaperDir);
            Assert.AreEqual(targetBuiltIn, _settings.Object.BuiltInWallpapersDir);
            Assert.AreEqual(targetUser, _vm.WallpaperDir);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(targetUser, "user", "media.png")));
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(targetBuiltIn, "built-in", "media.png")));
            Assert.AreEqual(!notificationFails, File.Exists(media));
            Assert.IsFalse(Directory.Exists(sourceBuiltIn), "One cleanup failure must not block the other library's cleanup.");
            Assert.AreEqual(1, notifications);
            Assert.IsFalse(_vm.WallpaperDirectoryChangeOngoing);
            Assert.IsTrue(_vm.IsWallpaperDirectoryChangeEnable);
            Assert.AreEqual(string.Empty, _vm.WallpaperDirectoryChangeStatus);
            _userSettingsClient.Verify(client => client.SaveAsync<ISettings>(), Times.Once);
            _wpControlClient.Verify(client => client.RestartAllWallpapersAsync(), Times.Once);
            _wpControlClient.Verify(client => client.ChangeWallpaperLayoutFolderPathAsync(targetUser, sourceUser), Times.Never);
            _wpControlClient.Verify(client => client.ChangeWallpaperLayoutFolderPathAsync(targetBuiltIn, sourceBuiltIn), Times.Never);
        }
        finally {
            locked?.Dispose();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task WallpaperDirectoryChange_ConcurrentRequest_DoesNotStartASecondMigration() {
        string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.LibraryMove", Guid.NewGuid().ToString("N"));
        string sourceUser = Path.Combine(root, "source", "wallpapers");
        string sourceBuiltIn = Path.Combine(root, "source", "BuiltInWallpapers");
        string destination = Path.Combine(root, "destination");
        string ignoredDestination = Path.Combine(root, "ignored");
        string targetUser = Path.Combine(destination, "wallpapers");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? migration = null;
        try {
            CreateMovableWallpaper(sourceUser, "user");
            CreateMovableWallpaper(sourceBuiltIn, "built-in");
            _settings.Object.WallpaperDir = sourceUser;
            _settings.Object.BuiltInWallpapersDir = sourceBuiltIn;
            _wpControlClient.Setup(client => client.ChangeWallpaperLayoutFolderPathAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _wpControlClient.Setup(client => client.ChangeWallpaperLayoutFolderPathAsync(sourceUser, targetUser)).Returns(() => {
                entered.TrySetResult();
                return resume.Task;
            });
            _wpControlClient.Setup(client => client.RestartAllWallpapersAsync())
                .ReturnsAsync(new Grpc_RestartWallpaperResponse { IsFinished = true });

            migration = _vm.WallpaperDirectoryChangeAsync(destination);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(_vm.WallpaperDirectoryChangeOngoing);
            Assert.IsFalse(_vm.IsWallpaperDirectoryChangeEnable);
            await _vm.WallpaperDirectoryChangeAsync(ignoredDestination);

            Assert.IsFalse(migration.IsCompleted);
            Assert.IsFalse(Directory.Exists(ignoredDestination));
            Assert.AreEqual(sourceUser, _settings.Object.WallpaperDir);
            _userSettingsClient.Verify(client => client.SaveAsync<ISettings>(), Times.Never);
            resume.TrySetResult();
            await migration.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.AreEqual(targetUser, _settings.Object.WallpaperDir);
            Assert.AreEqual(Path.Combine(destination, "BuiltInWallpapers"), _settings.Object.BuiltInWallpapersDir);
            Assert.IsFalse(Directory.Exists(sourceUser));
            Assert.IsFalse(Directory.Exists(sourceBuiltIn));
            Assert.IsFalse(Directory.Exists(ignoredDestination));
            Assert.IsFalse(_vm.WallpaperDirectoryChangeOngoing);
            Assert.IsTrue(_vm.IsWallpaperDirectoryChangeEnable);
            _userSettingsClient.Verify(client => client.SaveAsync<ISettings>(), Times.Once);
            _wpControlClient.Verify(client => client.ChangeWallpaperLayoutFolderPathAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
            _wpControlClient.Verify(client => client.RestartAllWallpapersAsync(), Times.Once);
        }
        finally {
            resume.TrySetResult();
            if (migration != null) await migration.WaitAsync(TimeSpan.FromSeconds(5));
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
