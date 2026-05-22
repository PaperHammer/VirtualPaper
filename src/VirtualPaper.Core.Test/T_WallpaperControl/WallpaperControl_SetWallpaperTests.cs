using Moq;
using NLog.Config;
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
    [DoNotParallelize]
    public class WallpaperControl_SetWallpaperTests {
        private WallpaperControl _sut = null!;
        private Mock<IMonitorManager> _monitorMgr = null!;
        private Mock<IWallpaperFactory> _factory = null!;
        private Mock<IUserSettingsService> _settings = null!;
        private readonly List<string> _tempFiles = [];

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

        [TestCleanup]
        public void Cleanup() {
            _sut.CloseAllWallpapers();
            TestDataBuilder.CleanupTempFiles(_tempFiles);
        }

        [TestMethod]
        [Description("SetWallpaperAsync should return failure immediately when monitor is null")]
        public async Task SetWallpaperAsync_WithNullMonitor_ShouldReturnFailure() {
            // Arrange
            var data = TestDataBuilder.CreateValidPlayerData(_tempFiles).Object;

            // Act
            var result = await _sut.SetWallpaperAsync(data, monitor: null);

            // Assert
            Assert.IsFalse(result.IsFinished, "Should return failure when monitor is null");
            _factory.Verify(
                f => f.CreatePlayer(It.IsAny<IWpPlayerData>(), It.IsAny<IMonitor>(), false),
                Times.Never, "Player should not be created");
        }

        [TestMethod]
        [Description("Should fire WallpaperError event and return failure when the file does not exist")]
        public async Task SetWallpaperAsync_WhenFileMissing_ShouldFireErrorEvent() {
            // Arrange
            var data = TestDataBuilder.CreateValidPlayerData(_tempFiles).Object;
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
            var data = TestDataBuilder.CreateValidPlayerData(_tempFiles,
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
            var data1 = TestDataBuilder.CreateValidPlayerData(_tempFiles, wpId: "wp_001").Object;
            var data2 = TestDataBuilder.CreateValidPlayerData(_tempFiles, wpId: "wp_002").Object;

            var player = TestDataBuilder.CreateWpPlayer(data1, monitor);
            _factory.Setup(f => f.CreatePlayer(It.IsAny<IWpPlayerData>(), monitor, false))
                    .Returns(player.Object);

            // Set the first wallpaper
            var r1 = await _sut.SetWallpaperAsync(data1, monitor);
            Assert.IsTrue(r1.IsFinished, "first step failed: data1 was not set successfully");

            // Act: set a second wallpaper on the same monitor
            var r2 = await _sut.SetWallpaperAsync(data2, monitor);
            Assert.IsTrue(r2.IsFinished, "second step failed: data2 was not set successfully");

            // Assert
            player.Verify(p => p.Update(data2), Times.Once, "Should call Update to refresh the wallpaper");
            _factory.Verify(
                f => f.CreatePlayer(It.IsAny<IWpPlayerData>(), monitor, false),
                Times.Once, "Player should not be recreated");
        }

        [TestMethod]
        [Description("WallpaperChanged event should be fired after a wallpaper is set successfully")]
        public async Task SetWallpaperAsync_OnSuccess_ShouldFireChangedEvent() {
            // Arrange
            var monitor = _monitorMgr.Object.PrimaryMonitor;
            var data = TestDataBuilder.CreateValidPlayerData(_tempFiles).Object;
            bool changed = false;
            _sut.WallpaperChanged += (_, _) => changed = true;

            var player = TestDataBuilder.CreateWpPlayer(data, monitor);
            _factory.Setup(f => f.CreatePlayer(data, monitor, false)).Returns(player.Object);

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
            var desktop = MockFactory.CreateDesktopService();
            var jobService = new Mock<IJobService>();
            _sut = new WallpaperControl(
                _settings.Object, _monitorMgr.Object,
                _factory.Object, desktop.Object, jobService.Object);

            var data = TestDataBuilder.CreateValidPlayerData(_tempFiles).Object;
            foreach (var m in _monitorMgr.Object.Monitors) {
                var p = TestDataBuilder.CreateWpPlayer(data, m);
                _factory.Setup(f => f.CreatePlayer(It.IsAny<IWpPlayerData>(), m, false))
                        .Returns(p.Object);
            }

            // Act
            await _sut.SetWallpaperAsync(data, _monitorMgr.Object.PrimaryMonitor);

            // Assert
            _factory.Verify(
                f => f.CreatePlayer(It.IsAny<IWpPlayerData>(), It.IsAny<IMonitor>(), false),
                Times.Exactly(3), "Duplicate mode should create a Player for each monitor");
        }

        // -------------------------------------------------------
        // RType guard: same monitor, different RType → new player
        // -------------------------------------------------------

        [TestMethod]
        [Description("In Per mode, setting a wallpaper with a different RType on the same monitor should create a new player, not call Update")]
        public async Task SetWallpaperAsync_PerMode_SameMonitor_DifferentRType_ShouldCreateNewPlayer() {
            // Arrange
            var monitor = _monitorMgr.Object.PrimaryMonitor;
            var data1 = TestDataBuilder.CreateValidPlayerData(_tempFiles, rtype: RuntimeType.RImage, wpId: "wp_img").Object;
            var data2 = TestDataBuilder.CreateValidPlayerData(_tempFiles, rtype: RuntimeType.RVideo, wpId: "wp_vid").Object;

            var player1 = TestDataBuilder.CreateWpPlayer(data1, monitor);
            var player2 = TestDataBuilder.CreateWpPlayer(data2, monitor);

            _factory.Setup(f => f.CreatePlayer(It.Is<IWpPlayerData>(d => d.RType == RuntimeType.RImage), monitor, false))
                    .Returns(player1.Object);
            _factory.Setup(f => f.CreatePlayer(It.Is<IWpPlayerData>(d => d.RType == RuntimeType.RVideo), monitor, false))
                    .Returns(player2.Object);

            // Set Image wallpaper first
            var r1 = await _sut.SetWallpaperAsync(data1, monitor);
            Assert.IsTrue(r1.IsFinished, "First set (RImage) should succeed");

            // Act: set Video wallpaper on same monitor
            var r2 = await _sut.SetWallpaperAsync(data2, monitor);
            Assert.IsTrue(r2.IsFinished, "Second set (RVideo) should succeed");

            // Assert: player1 should NOT have Update called (different RType → not reused)
            player1.Verify(p => p.Update(It.IsAny<IWpPlayerData>()), Times.Never,
                "Update should not be called on a different-RType player");
            // A new player (player2) should be created for the new RType
            _factory.Verify(
                f => f.CreatePlayer(It.Is<IWpPlayerData>(d => d.RType == RuntimeType.RVideo), monitor, false),
                Times.Once, "A new player should be created for the different RType");
        }

        [TestMethod]
        [Description("In Per mode, setting a wallpaper with the same RType on the same monitor should call Update, not create a new player")]
        public async Task SetWallpaperAsync_PerMode_SameMonitor_SameRType_ShouldUpdate_NotCreateNew() {
            // Arrange
            var monitor = _monitorMgr.Object.PrimaryMonitor;
            var data1 = TestDataBuilder.CreateValidPlayerData(_tempFiles, rtype: RuntimeType.RImage, wpId: "wp_001").Object;
            var data2 = TestDataBuilder.CreateValidPlayerData(_tempFiles, rtype: RuntimeType.RImage, wpId: "wp_002").Object;

            var player = TestDataBuilder.CreateWpPlayer(data1, monitor);
            _factory.Setup(f => f.CreatePlayer(It.IsAny<IWpPlayerData>(), monitor, false))
                    .Returns(player.Object);

            var r1 = await _sut.SetWallpaperAsync(data1, monitor);
            Assert.IsTrue(r1.IsFinished, "First set should succeed");

            // Act
            var r2 = await _sut.SetWallpaperAsync(data2, monitor);
            Assert.IsTrue(r2.IsFinished, "Second set (same RType) should succeed");

            // Assert
            player.Verify(p => p.Update(data2), Times.Once, "Update should be called for same-RType replacement");
            _factory.Verify(
                f => f.CreatePlayer(It.IsAny<IWpPlayerData>(), monitor, false),
                Times.Once, "Player should not be recreated for same RType");
        }

        [TestMethod]
        [Description("In Expand mode, setting a wallpaper with a different RType should not reuse the existing player")]
        public async Task SetWallpaperAsync_ExpandMode_SameMonitor_DifferentRType_ShouldCreateNewPlayer() {
            // Arrange
            _settings = MockFactory.CreateUserSettings(WallpaperArrangement.Expand);
            _monitorMgr = MockFactory.CreateMonitorManager(1);
            var desktop = MockFactory.CreateDesktopService();
            var jobService = new Mock<IJobService>();
            _sut = new WallpaperControl(
                _settings.Object, _monitorMgr.Object,
                _factory.Object, desktop.Object, jobService.Object);

            var monitor = _monitorMgr.Object.PrimaryMonitor;
            var dataImg = TestDataBuilder.CreateValidPlayerData(_tempFiles, rtype: RuntimeType.RImage, wpId: "wp_img").Object;
            var dataVid = TestDataBuilder.CreateValidPlayerData(_tempFiles, rtype: RuntimeType.RVideo, wpId: "wp_vid").Object;

            var playerImg = TestDataBuilder.CreateWpPlayer(dataImg, monitor);
            var playerVid = TestDataBuilder.CreateWpPlayer(dataVid, monitor);

            _factory.Setup(f => f.CreatePlayer(It.Is<IWpPlayerData>(d => d.RType == RuntimeType.RImage), monitor, false))
                    .Returns(playerImg.Object);
            _factory.Setup(f => f.CreatePlayer(It.Is<IWpPlayerData>(d => d.RType == RuntimeType.RVideo), monitor, false))
                    .Returns(playerVid.Object);

            await _sut.SetWallpaperAsync(dataImg, monitor);

            // Act
            await _sut.SetWallpaperAsync(dataVid, monitor);

            // Assert
            playerImg.Verify(p => p.Update(It.IsAny<IWpPlayerData>()), Times.Never,
                "Different RType: Update should NOT be called on the old player");
            _factory.Verify(
                f => f.CreatePlayer(It.Is<IWpPlayerData>(d => d.RType == RuntimeType.RVideo), monitor, false),
                Times.Once, "A new Expand player should be created for the different RType");
        }

        // -------------------------------------------------------
        // isFromPreview behaviour
        // -------------------------------------------------------

        [TestMethod]
        [Description("SetWallpaperAsync with isFromPreview=true should succeed (runtime data loaded from temp path)")]
        public async Task SetWallpaperAsync_IsFromPreview_True_ShouldSucceed() {
            // Arrange
            var monitor = _monitorMgr.Object.PrimaryMonitor;
            // Build data with all effect paths pre-populated so isFromPreview branch can proceed
            var data = TestDataBuilder.CreateValidPlayerDataWithEffectPaths(_tempFiles).Object;
            bool changed = false;
            _sut.WallpaperChanged += (_, _) => changed = true;

            var player = TestDataBuilder.CreateWpPlayer(data, monitor);
            _factory.Setup(f => f.CreatePlayer(It.IsAny<IWpPlayerData>(), monitor, false))
                    .Returns(player.Object);

            // Act
            var result = await _sut.SetWallpaperAsync(data, monitor, isFromPreview: true);

            // Assert
            Assert.IsTrue(result.IsFinished, "SetWallpaperAsync with isFromPreview=true should finish successfully");
            Assert.IsTrue(changed, "WallpaperChanged event should fire even when isFromPreview=true");
        }

        [TestMethod]
        [Description("SetWallpaperAsync with isFromPreview=false and no effect path should succeed via CreateRuntimeData path")]
        public async Task SetWallpaperAsync_IsFromPreview_False_EmptyEffectPath_ShouldSucceed() {
            // Arrange
            var monitor = _monitorMgr.Object.PrimaryMonitor;
            var data = TestDataBuilder.CreateValidPlayerData(_tempFiles).Object;

            var player = TestDataBuilder.CreateWpPlayer(data, monitor);
            _factory.Setup(f => f.CreatePlayer(It.IsAny<IWpPlayerData>(), monitor, false))
                    .Returns(player.Object);

            // Act
            var result = await _sut.SetWallpaperAsync(data, monitor, isFromPreview: false);

            // Assert
            Assert.IsTrue(result.IsFinished);
        }

        // -------------------------------------------------------
        // Monitor 不存在路径
        // -------------------------------------------------------

        [TestMethod]
        [Description("SetWallpaperAsync should fire WallpaperError when the monitor is not found in manager")]
        public async Task SetWallpaperAsync_WhenMonitorNotFound_ShouldFireErrorAndReturnFailure() {
            // Arrange: make MonitorExists return false
            _monitorMgr.Setup(m => m.MonitorExists(It.IsAny<IMonitor>())).Returns(false);
            var monitor = _monitorMgr.Object.PrimaryMonitor;
            var data = TestDataBuilder.CreateValidPlayerData(_tempFiles).Object;
            Exception? capturedError = null;
            _sut.WallpaperError += (_, e) => capturedError = e;

            // Act
            var result = await _sut.SetWallpaperAsync(data, monitor);

            // Assert
            Assert.IsFalse(result.IsFinished);
            Assert.IsNotNull(capturedError, "WallpaperError should be fired when monitor is not found");
            _factory.Verify(
                f => f.CreatePlayer(It.IsAny<IWpPlayerData>(), It.IsAny<IMonitor>(), false),
                Times.Never, "No player should be created when monitor is missing");
        }

        [TestMethod]
        [Description("SetWallpaperAsync should fire BOTH WallpaperError and WallpaperChanged when the file is missing")]
        public async Task SetWallpaperAsync_WhenFileMissing_ShouldFireBothErrorAndChangedEvents() {
            // Arrange
            var data = TestDataBuilder.CreateValidPlayerData(_tempFiles).Object;
            Mock.Get(data).Setup(d => d.FilePath).Returns("C:\\not_exist.jpg");
            bool changed = false;
            Exception? capturedError = null;
            _sut.WallpaperChanged += (_, _) => changed = true;
            _sut.WallpaperError += (_, e) => capturedError = e;

            // Act
            await _sut.SetWallpaperAsync(data, _monitorMgr.Object.PrimaryMonitor);

            // Assert
            Assert.IsNotNull(capturedError, "WallpaperError must be fired on file missing");
            Assert.IsTrue(changed,
                "WallpaperChanged must ALSO be fired on file missing (source-code behaviour to clear UI state)");
        }

        // -------------------------------------------------------
        // RType guard: 旧 Player 孤立 Bug — 验证旧 Player 被 Close 且列表数量
        // -------------------------------------------------------

        [TestMethod]
        [Description("Per mode: after switching to a different RType on the same monitor, the OLD player must be closed and wallpaper count must stay at 1")]
        public async Task SetWallpaperAsync_PerMode_DifferentRType_OldPlayerMustBeClosedAndCountStaysOne() {
            // Arrange
            var monitor = _monitorMgr.Object.PrimaryMonitor;
            var dataImg = TestDataBuilder.CreateValidPlayerData(_tempFiles, rtype: RuntimeType.RImage, wpId: "wp_img").Object;
            var dataVid = TestDataBuilder.CreateValidPlayerData(_tempFiles, rtype: RuntimeType.RVideo, wpId: "wp_vid").Object;

            var playerImg = TestDataBuilder.CreateWpPlayer(dataImg, monitor);
            var playerVid = TestDataBuilder.CreateWpPlayer(dataVid, monitor);

            _factory.Setup(f => f.CreatePlayer(It.Is<IWpPlayerData>(d => d.RType == RuntimeType.RImage), monitor, false))
                    .Returns(playerImg.Object);
            _factory.Setup(f => f.CreatePlayer(It.Is<IWpPlayerData>(d => d.RType == RuntimeType.RVideo), monitor, false))
                    .Returns(playerVid.Object);

            await _sut.SetWallpaperAsync(dataImg, monitor);
            Assert.HasCount(1, _sut.Wallpapers, "Setup: should have 1 wallpaper after first set");

            // Act
            await _sut.SetWallpaperAsync(dataVid, monitor);

            // Assert: old player should be closed, count must remain 1
            playerImg.Verify(p => p.Close(), Times.Once,
                "Old RImage player MUST be closed when switching to a different RType");
            Assert.HasCount(1, _sut.Wallpapers,
                "After switching RType, wallpaper count must still be 1 — not orphan-leaked");
        }

        [TestMethod]
        [Description("Expand mode: after switching to a different RType, the OLD player must be closed and count stays at 1")]
        public async Task SetWallpaperAsync_ExpandMode_DifferentRType_OldPlayerMustBeClosedAndCountStaysOne() {
            // Arrange
            _settings = MockFactory.CreateUserSettings(WallpaperArrangement.Expand);
            _monitorMgr = MockFactory.CreateMonitorManager(1);
            var desktop = MockFactory.CreateDesktopService();
            var jobService = new Mock<IJobService>();
            _sut = new WallpaperControl(
                _settings.Object, _monitorMgr.Object,
                _factory.Object, desktop.Object, jobService.Object);

            var monitor = _monitorMgr.Object.PrimaryMonitor;
            var dataImg = TestDataBuilder.CreateValidPlayerData(_tempFiles, rtype: RuntimeType.RImage, wpId: "wp_img").Object;
            var dataVid = TestDataBuilder.CreateValidPlayerData(_tempFiles, rtype: RuntimeType.RVideo, wpId: "wp_vid").Object;

            var playerImg = TestDataBuilder.CreateWpPlayer(dataImg, monitor);
            var playerVid = TestDataBuilder.CreateWpPlayer(dataVid, monitor);

            _factory.Setup(f => f.CreatePlayer(It.Is<IWpPlayerData>(d => d.RType == RuntimeType.RImage), monitor, false))
                    .Returns(playerImg.Object);
            _factory.Setup(f => f.CreatePlayer(It.Is<IWpPlayerData>(d => d.RType == RuntimeType.RVideo), monitor, false))
                    .Returns(playerVid.Object);

            await _sut.SetWallpaperAsync(dataImg, monitor);
            Assert.HasCount(1, _sut.Wallpapers, "Setup: should have 1 wallpaper after first set");

            // Act
            await _sut.SetWallpaperAsync(dataVid, monitor);

            // Assert
            playerImg.Verify(p => p.Close(), Times.Once,
                "Old Expand player MUST be closed when switching to a different RType");
            Assert.HasCount(1, _sut.Wallpapers,
                "Wallpaper count must remain 1 after Expand mode RType switch");
        }
    }
}
