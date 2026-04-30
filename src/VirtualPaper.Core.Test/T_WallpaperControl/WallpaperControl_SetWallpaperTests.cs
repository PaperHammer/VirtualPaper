using Moq;
using VirtualPaper.Common;
using VirtualPaper.Core.Test.Infrastructure;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Cores.WpControl;
using VirtualPaper.Factories.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;
using static VirtualPaper.Common.Errors;
using MockFactory = VirtualPaper.Core.Test.Infrastructure.MockFactory;

namespace VirtualPaper.Core.Test.T_WallpaperControl {
    [TestClass]
    [TestCategory("Backend")]
    public class WallpaperControl_SetWallpaperTests {
        private WallpaperControl _sut = null!;
        private Mock<IMonitorManager> _monitorMgr = null!;
        private Mock<IWallpaperFactory> _factory = null!;
        private Mock<IUserSettingsService> _settings = null!;

        [TestInitialize]
        public void Setup() {
            _settings = MockFactory.CreateUserSettings(WallpaperArrangement.Per);
            _monitorMgr = MockFactory.CreateMonitorManager(1);
            _factory = new Mock<IWallpaperFactory>();
            var desktop = MockFactory.CreateDesktopService();
            var jobService = new Mock<IJobService>();

            _sut = new WallpaperControl(
                _settings.Object, _monitorMgr.Object,
                _factory.Object, desktop.Object, jobService.Object);
        }

        [TestMethod]
        [Description("SetWallpaperAsync should return failure immediately when monitor is null")]
        public async Task SetWallpaperAsync_WithNullMonitor_ShouldReturnFailure() {
            // Arrange
            var data = TestDataBuilder.CreateValidPlayerData().Object;

            // Act
            var result = await _sut.SetWallpaperAsync(data, monitor: null);

            // Assert
            Assert.IsFalse(result.IsFinished, "Should return failure when monitor is null");
            _factory.Verify(
                f => f.CreatePlayer(It.IsAny<IWpPlayerData>(), It.IsAny<IMonitor>()),
                Times.Never, "Player should not be created");
        }

        [TestMethod]
        [Description("Should fire WallpaperError event and return failure when the file does not exist")]
        public async Task SetWallpaperAsync_WhenFileMissing_ShouldFireErrorEvent() {
            // Arrange
            var data = TestDataBuilder.CreateValidPlayerData().Object;
            Mock.Get(data).Setup(d => d.FilePath).Returns("C:\\not_exist.jpg");
            Exception? capturedError = null;
            _sut.WallpaperError += (s, e) => capturedError = e;

            // Act
            var result = await _sut.SetWallpaperAsync(data, _monitorMgr.Object.PrimaryMonitor);

            // Assert
            Assert.IsFalse(result.IsFinished);
            Assert.IsNotNull(capturedError);
            Assert.IsInstanceOfType(capturedError, typeof(WallpaperNotFoundException));
        }

        [TestMethod]
        [Description("Should throw an exception and fire WallpaperError when RType is Unknown")]
        public async Task SetWallpaperAsync_WithUnknownRType_ShouldFireError() {
            // Arrange
            var data = TestDataBuilder.CreateValidPlayerData(
                rtype: RuntimeType.RUnknown).Object;
            Exception? capturedError = null;
            _sut.WallpaperError += (_, e) => capturedError = e;

            // Act
            var result = await _sut.SetWallpaperAsync(data, _monitorMgr.Object.PrimaryMonitor);

            // Assert
            Assert.IsFalse(result.IsFinished);
            Assert.IsNotNull(capturedError, "Error event should be fired");
        }

        [TestMethod]
        [Description("In Per mode, setting a wallpaper on the same monitor again should call Update instead of recreating")]
        public async Task SetWallpaperAsync_PerMode_SameMontior_ShouldUpdate_NotRecreate() {
            // Arrange
            var monitor = _monitorMgr.Object.PrimaryMonitor;
            var data1 = TestDataBuilder.CreateValidPlayerData(wpId: "wp_001").Object;
            var data2 = TestDataBuilder.CreateValidPlayerData(wpId: "wp_002").Object;

            var player = TestDataBuilder.CreateWpPlayer(data1, monitor);
            _factory.Setup(f => f.CreatePlayer(It.IsAny<IWpPlayerData>(), monitor))
                    .Returns(player.Object);

            // Set the first wallpaper
            await _sut.SetWallpaperAsync(data1, monitor);

            // Act: set a second wallpaper on the same monitor
            await _sut.SetWallpaperAsync(data2, monitor);

            // Assert
            player.Verify(p => p.Update(data2), Times.Once, "Should call Update to refresh the wallpaper");
            _factory.Verify(
                f => f.CreatePlayer(It.IsAny<IWpPlayerData>(), monitor),
                Times.Once, "Player should not be recreated");
        }

        [TestMethod]
        [Description("WallpaperChanged event should be fired after a wallpaper is set successfully")]
        public async Task SetWallpaperAsync_OnSuccess_ShouldFireChangedEvent() {
            // Arrange
            var monitor = _monitorMgr.Object.PrimaryMonitor;
            var data = TestDataBuilder.CreateValidPlayerData().Object;
            bool changed = false;
            _sut.WallpaperChanged += (_, _) => changed = true;

            var player = TestDataBuilder.CreateWpPlayer(data, monitor);
            _factory.Setup(f => f.CreatePlayer(data, monitor)).Returns(player.Object);

            // Act
            var result = await _sut.SetWallpaperAsync(data, monitor);

            // Assert
            Assert.IsTrue(result.IsFinished);
            Assert.IsTrue(changed, "WallpaperChanged event should be fired on success");
        }

        [TestMethod]
        [Description("In Duplicate mode, setting a wallpaper should create one instance per monitor")]
        public async Task SetWallpaperAsync_DuplicateMode_ShouldCreatePlayerPerMonitor() {
            // Arrange: 3 monitors
            _settings = MockFactory.CreateUserSettings(WallpaperArrangement.Duplicate);
            _monitorMgr = MockFactory.CreateMonitorManager(3);
            RebuildSut();

            var data = TestDataBuilder.CreateValidPlayerData().Object;
            foreach (var m in _monitorMgr.Object.Monitors) {
                var p = TestDataBuilder.CreateWpPlayer(data, m);
                _factory.Setup(f => f.CreatePlayer(It.IsAny<IWpPlayerData>(), m))
                        .Returns(p.Object);
            }

            // Act
            await _sut.SetWallpaperAsync(data, _monitorMgr.Object.PrimaryMonitor);

            // Assert
            _factory.Verify(
                f => f.CreatePlayer(It.IsAny<IWpPlayerData>(), It.IsAny<IMonitor>()),
                Times.Exactly(3), "Duplicate mode should create a Player for each monitor");
        }

        private void RebuildSut() {
            var desktop = MockFactory.CreateDesktopService();
            var jobService = new Mock<IJobService>();
            _sut = new WallpaperControl(
                _settings.Object, _monitorMgr.Object,
                _factory.Object, desktop.Object, jobService.Object);
        }
    }
}
