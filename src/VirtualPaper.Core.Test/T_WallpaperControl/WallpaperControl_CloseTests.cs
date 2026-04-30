using Moq;
using VirtualPaper.Core.Test.Infrastructure;
using VirtualPaper.Cores;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Cores.WpControl;
using VirtualPaper.Factories.Interfaces;
using VirtualPaper.Models.Cores;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;
using MockFactory = VirtualPaper.Core.Test.Infrastructure.MockFactory;

namespace VirtualPaper.Core.Test.T_WallpaperControl {
    [TestClass]
    [TestCategory("Backend")]
    public class WallpaperControl_CloseTests {
        private WallpaperControl _sut = null!;
        private Mock<IMonitorManager> _monitorMgr = null!;
        private Mock<IWallpaperFactory> _factory = null!;
        private Mock<IUserSettingsService> _settings = null!;

        [TestInitialize]
        public void Setup() {
            _settings = MockFactory.CreateUserSettings();
            _monitorMgr = MockFactory.CreateMonitorManager(monitorCount: 2);
            _factory = new Mock<IWallpaperFactory>();
            var desktop = MockFactory.CreateDesktopService();
            var jobService = new Mock<IJobService>();

            _sut = new WallpaperControl(
                _settings.Object, _monitorMgr.Object,
                _factory.Object, desktop.Object, jobService.Object);
        }

        [TestMethod]
        [Description("CloseAllWallpapers should close all running wallpaper instances")]
        public async Task CloseAllWallpapers_ShouldCloseAllInstances() {
            // Arrange: set up 2 wallpaper instances
            var monitor1 = _monitorMgr.Object.Monitors[0];
            var monitor2 = _monitorMgr.Object.Monitors[1];
            var player1 = await AddRunningWallpaper(monitor1);
            Assert.HasCount(1, _sut.Wallpapers, "After adding 1st wallpaper");

            var player2 = await AddRunningWallpaper(monitor2);
            Assert.HasCount(2, _sut.Wallpapers, "After adding 2nd wallpaper");

            // Act
            _sut.CloseAllWallpapers();

            // Assert
            player1.Verify(p => p.Close(), Times.Once, "Wallpaper 1 should be closed");
            player2.Verify(p => p.Close(), Times.Once, "Wallpaper 2 should be closed");
            Assert.HasCount(0, _sut.Wallpapers, "Wallpaper list should be empty after closing all");
        }

        [TestMethod]
        [Description("CloseAllWallpapers should clear thumbnail paths for all monitors")]
        public void CloseAllWallpapers_ShouldClearThumbnailPaths() {
            // Arrange
            var monitors = _monitorMgr.Object.Monitors.ToList();

            // Act
            _sut.CloseAllWallpapers();

            // Assert
            foreach (var monitor in monitors) {
                Assert.AreEqual(string.Empty, monitor.ThumbnailPath);
            }
        }

        [TestMethod]
        [Description("CloseWallpaper should return safely without throwing when passed null")]
        public void CloseWallpaper_WithNullMonitor_ShouldNotThrow() {
            // Act & Assert
            try {
                _sut.CloseWallpaper(null);
            }
            catch (Exception ex) {
                Assert.Fail($"No exception should be thrown, but got: {ex.GetType().Name}: {ex.Message}");
            }
        }

        [TestMethod]
        [Description("CloseWallpaper should only close the wallpaper on the specified monitor without affecting others")]
        public async Task CloseWallpaper_ShouldOnlyCloseTargetMonitor() {
            // Arrange
            var monitor1 = _monitorMgr.Object.Monitors[0];
            var monitor2 = _monitorMgr.Object.Monitors[1];
            var player1 = await AddRunningWallpaper(monitor1);
            var player2 = await AddRunningWallpaper(monitor2);

            // Act: close monitor1 only
            _sut.CloseWallpaper(monitor1);

            // Assert
            player1.Verify(p => p.Close(), Times.Once, "Target monitor wallpaper should be closed");
            player2.Verify(p => p.Close(), Times.Never, "Other monitor wallpaper should not be closed");
            Assert.HasCount(1, _sut.Wallpapers);
        }

        // Helper: simulate adding a running wallpaper
        private async Task<Mock<IWpPlayer>> AddRunningWallpaper(IMonitor monitor) {
            var data = TestDataBuilder.CreateValidPlayerData().Object;
            var player = TestDataBuilder.CreateWpPlayer(data, monitor);
            _factory.Setup(f => f.CreatePlayer(It.IsAny<IWpPlayerData>(), monitor))
                    .Returns(player.Object);
            await _sut.SetWallpaperAsync(data, monitor);
            return player;
        }
    }
}
