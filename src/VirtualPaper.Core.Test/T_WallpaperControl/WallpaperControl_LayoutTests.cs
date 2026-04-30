using Moq;
using VirtualPaper.Common;
using VirtualPaper.Core.Test.Infrastructure;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Cores.WpControl;
using VirtualPaper.Factories.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;
using MockFactory = VirtualPaper.Core.Test.Infrastructure.MockFactory;

namespace VirtualPaper.Core.Test.T_WallpaperControl {
    [TestClass]
    [TestCategory("Backend")]
    public class WallpaperControl_LayoutTests {
        private WallpaperControl _sut = null!;
        private Mock<IUserSettingsService> _settings = null!;
        private Mock<IMonitorManager> _monitorMgr = null!;
        private Mock<IJobService> _jobService = null!;
        private Mock<IWallpaperFactory> _factory = null!;
        private List<IWallpaperLayout> _capturedLayouts = [];

        [TestInitialize]
        public void Setup() {
            _capturedLayouts = [];
            _settings = MockFactory.CreateUserSettings();
            _jobService = new Mock<IJobService>();
            _settings.Setup(s => s.WallpaperLayouts).Returns(_capturedLayouts);
            _monitorMgr = MockFactory.CreateMonitorManager(2);
            _factory = new Mock<IWallpaperFactory>();

            _sut = new WallpaperControl(
                _settings.Object, _monitorMgr.Object,
                _factory.Object, MockFactory.CreateDesktopService().Object, _jobService.Object);
        }

        [TestMethod]
        [Description("Layout information should be saved correctly after a wallpaper is set successfully")]
        public async Task WallpaperChanged_ShouldSaveLayoutWithCorrectMonitorId() {
            // Arrange
            var monitor = _monitorMgr.Object.PrimaryMonitor;
            var data = TestDataBuilder.CreateValidPlayerData().Object;
            var player = TestDataBuilder.CreateWpPlayer(data, monitor);
            _factory.Setup(f => f.CreatePlayer(data, monitor)).Returns(player.Object);

            // Act
            await _sut.SetWallpaperAsync(data, monitor);

            // Assert
            _settings.Verify(s => s.Save<List<IWallpaperLayout>>(), Times.AtLeastOnce);
            Assert.IsTrue(_capturedLayouts.Any(l => l.MonitorDeviceId == monitor.DeviceId),
                "Layout for the corresponding monitor should be saved");
        }

        [TestMethod]
        [Description("The saved layout list should be empty after all wallpapers are closed")]
        public async Task CloseAllWallpapers_ShouldSaveEmptyLayout() {
            // Arrange: add a wallpaper first
            var monitor = _monitorMgr.Object.PrimaryMonitor;
            var data = TestDataBuilder.CreateValidPlayerData().Object;
            _factory.Setup(f => f.CreatePlayer(data, monitor))
                    .Returns(TestDataBuilder.CreateWpPlayer(data, monitor).Object);
            await _sut.SetWallpaperAsync(data, monitor);

            // Act
            _sut.CloseAllWallpapers();

            // Assert
            Assert.HasCount(0, _capturedLayouts, "Layout should be empty after closing all wallpapers");
        }

        [TestMethod]
        [Description("In Expand mode, the MonitorContent of the layout should be 'Expand'")]
        public async Task SaveLayout_ExpandMode_ShouldUseExpandAsMonitorContent() {
            // Arrange
            _settings = MockFactory.CreateUserSettings(WallpaperArrangement.Expand);
            _settings.Setup(s => s.WallpaperLayouts).Returns(_capturedLayouts);
            var desktop = MockFactory.CreateDesktopService();
            _sut = new WallpaperControl(
                _settings.Object, _monitorMgr.Object, _factory.Object, desktop.Object, _jobService.Object);

            var monitor = _monitorMgr.Object.PrimaryMonitor;
            var data = TestDataBuilder.CreateValidPlayerData().Object;
            _factory.Setup(f => f.CreatePlayer(data, monitor))
                    .Returns(TestDataBuilder.CreateWpPlayer(data, monitor).Object);

            // Act
            await _sut.SetWallpaperAsync(data, monitor);

            // Assert
            Assert.IsTrue(
                _capturedLayouts.All(l => l.MonitorContent == "Expand"),
                "In Expand mode, MonitorContent of all layouts should be 'Expand'");
        }
    }
}
