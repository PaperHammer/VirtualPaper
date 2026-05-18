using Moq;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Hardware;
using VirtualPaper.Cores;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Cores.PlaybackControl;
using VirtualPaper.Cores.ScreenSaver;
using VirtualPaper.Cores.WpControl;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Utils.Interfcaes;

namespace VirtualPaper.Core.Test.T_Playback {
    [TestClass]
    public class PlaybackTests {
        private Mock<IUserSettingsService> _mockUserSettings = null!;
        private Mock<IWallpaperControl> _mockWpControl = null!;
        private Mock<IScrControl> _mockScrControl = null!;
        private Mock<IMonitorManager> _mockMonitorManager = null!;
        private Mock<IPowerService> _mockPowerService = null!;
        private Mock<ISettings> _mockSettings = null!;
        private Mock<IWpPlayer> _mockWallpaper1 = null!;
        private Mock<IWpPlayer> _mockWallpaper2 = null!;
        private List<IWpPlayer> _wallpapers = null!;

        [TestInitialize]
        public void TestInitialize() {
            _mockSettings = new Mock<ISettings>();
            _mockSettings.Setup(s => s.ProcessTimerInterval).Returns(500);
            _mockSettings.Setup(s => s.RemoteDesktop).Returns(AppWpRunRulesEnum.KeepRun);
            _mockSettings.Setup(s => s.BatteryPoweredn).Returns(AppWpRunRulesEnum.KeepRun);
            _mockSettings.Setup(s => s.PowerSaving).Returns(AppWpRunRulesEnum.KeepRun);

            _mockUserSettings = new Mock<IUserSettingsService>();
            _mockUserSettings.Setup(u => u.Settings).Returns(_mockSettings.Object);
            _mockUserSettings.Setup(u => u.AppRules).Returns(new List<IApplicationRules>());

            _mockWpControl = new Mock<IWallpaperControl>();
            _mockScrControl = new Mock<IScrControl>();
            _mockMonitorManager = new Mock<IMonitorManager>();
            _mockPowerService = new Mock<IPowerService>();

            _mockPowerService.Setup(p => p.GetACPowerStatus())
                .Returns(PowerUtil.ACLineStatus.Online);
            _mockPowerService.Setup(p => p.GetBatterySaverStatus())
                .Returns(PowerUtil.SystemStatusFlag.Off);

            _mockScrControl.Setup(s => s.IsRunning).Returns(false);

            _mockWallpaper1 = new Mock<IWpPlayer>();
            _mockWallpaper2 = new Mock<IWpPlayer>();
            _wallpapers = new List<IWpPlayer> { _mockWallpaper1.Object, _mockWallpaper2.Object };
            _mockWpControl.Setup(w => w.Wallpapers).Returns(_wallpapers.AsReadOnly());
        }

        private Playback CreatePlayback() {
            return new Playback(
                _mockUserSettings.Object,
                _mockWpControl.Object,
                _mockScrControl.Object,
                _mockMonitorManager.Object,
                _mockPowerService.Object);
        }

        // -------------------------------------------------------
        // WallpaperPlaybackMode setter
        // -------------------------------------------------------

        [TestMethod]
        public void WallpaperPlaybackMode_WhenSet_ShouldFirePlaybackModeChangedEvent() {
            var playback = CreatePlayback();
            PlaybackMode? received = null;
            playback.PlaybackModeChanged += (_, mode) => received = mode;

            playback.WallpaperPlaybackMode = PlaybackMode.Paused;

            Assert.AreEqual(PlaybackMode.Paused, received);
        }

        [TestMethod]
        public void WallpaperPlaybackMode_SetSameValue_ShouldStillFireEvent() {
            var playback = CreatePlayback();
            int callCount = 0;
            playback.PlaybackModeChanged += (_, _) => callCount++;

            playback.WallpaperPlaybackMode = PlaybackMode.Play;

            Assert.AreEqual(1, callCount);
        }

        // -------------------------------------------------------
        // RunPlayback - 壁纸列表为空
        // -------------------------------------------------------

        [TestMethod]
        public void RunPlayback_WhenNoWallpapers_ShouldNotChangeAnyState() {
            _mockWpControl.Setup(w => w.Wallpapers).Returns(new List<IWpPlayer>().AsReadOnly());
            var playback = CreatePlayback();

            playback.InvokeRunPlayback();

            _mockWallpaper1.Verify(w => w.Pause(), Times.Never);
            _mockWallpaper1.Verify(w => w.Play(), Times.Never);
        }

        // -------------------------------------------------------
        // RunPlayback - 屏保运行
        // -------------------------------------------------------

        [TestMethod]
        public void RunPlayback_WhenScrControlIsRunning_ShouldPauseAllWallpapers() {
            _mockScrControl.Setup(s => s.IsRunning).Returns(true);
            var playback = CreatePlayback();

            playback.InvokeRunPlayback();

            _mockWallpaper1.Verify(w => w.Pause(), Times.Once);
            _mockWallpaper2.Verify(w => w.Pause(), Times.Once);
        }

        // -------------------------------------------------------
        // RunPlayback - WallpaperPlaybackMode
        // -------------------------------------------------------

        [TestMethod]
        public void RunPlayback_WhenPlaybackModePaused_ShouldPauseAllWallpapers() {
            var playback = CreatePlayback();
            playback.WallpaperPlaybackMode = PlaybackMode.Paused;

            playback.InvokeRunPlayback();

            _mockWallpaper1.Verify(w => w.Pause(), Times.Once);
            _mockWallpaper2.Verify(w => w.Pause(), Times.Once);
        }

        [TestMethod]
        public void RunPlayback_WhenPlaybackModeSilence_ShouldPlayAndMuteAllWallpapers() {
            var playback = CreatePlayback();
            playback.WallpaperPlaybackMode = PlaybackMode.Silence;

            playback.InvokeRunPlayback();

            _mockWallpaper1.Verify(w => w.Play(), Times.Once);
            _mockWallpaper1.Verify(w => w.SetMute(true), Times.Once);
            _mockWallpaper2.Verify(w => w.Play(), Times.Once);
            _mockWallpaper2.Verify(w => w.SetMute(true), Times.Once);
        }

        // -------------------------------------------------------
        // RunPlayback - 锁屏
        // -------------------------------------------------------

        [TestMethod]
        public void RunPlayback_WhenLockScreen_ShouldPauseAllWallpapers() {
            var playback = CreatePlayback();
            playback.SimulateLockScreen(true);

            playback.InvokeRunPlayback();

            _mockWallpaper1.Verify(w => w.Pause(), Times.Once);
            _mockWallpaper2.Verify(w => w.Pause(), Times.Once);
        }

        // -------------------------------------------------------
        // RunPlayback - 远程桌面
        // -------------------------------------------------------

        [TestMethod]
        public void RunPlayback_WhenRemoteSession_AndSettingIsPause_ShouldPauseAllWallpapers() {
            _mockSettings.Setup(s => s.RemoteDesktop).Returns(AppWpRunRulesEnum.Pause);
            var playback = CreatePlayback();
            playback.SimulateRemoteSession(true);

            playback.InvokeRunPlayback();

            _mockWallpaper1.Verify(w => w.Pause(), Times.Once);
            _mockWallpaper2.Verify(w => w.Pause(), Times.Once);
        }

        [TestMethod]
        public void RunPlayback_WhenRemoteSession_AndSettingIsSilence_ShouldSilenceAllWallpapers() {
            _mockSettings.Setup(s => s.RemoteDesktop).Returns(AppWpRunRulesEnum.Silence);
            var playback = CreatePlayback();
            playback.SimulateRemoteSession(true);

            playback.InvokeRunPlayback();

            _mockWallpaper1.Verify(w => w.Play(), Times.Once);
            _mockWallpaper1.Verify(w => w.SetMute(true), Times.Once);
            _mockWallpaper2.Verify(w => w.Play(), Times.Once);
            _mockWallpaper2.Verify(w => w.SetMute(true), Times.Once);
        }

        [TestMethod]
        public void RunPlayback_WhenNotRemoteSession_AndSettingIsPause_ShouldNotPause() {
            _mockSettings.Setup(s => s.RemoteDesktop).Returns(AppWpRunRulesEnum.Pause);
            var playback = CreatePlayback();
            playback.SimulateRemoteSession(false);

            playback.InvokeRunPlayback();

            _mockWallpaper1.Verify(w => w.Pause(), Times.Never);
        }

        // -------------------------------------------------------
        // RunPlayback - 电池供电
        // -------------------------------------------------------

        [TestMethod]
        public void RunPlayback_WhenBatteryOffline_AndSettingIsPause_ShouldPauseAllWallpapers() {
            _mockPowerService.Setup(p => p.GetACPowerStatus())
                .Returns(PowerUtil.ACLineStatus.Offline);
            _mockSettings.Setup(s => s.BatteryPoweredn).Returns(AppWpRunRulesEnum.Pause);
            var playback = CreatePlayback();

            playback.InvokeRunPlayback();

            _mockWallpaper1.Verify(w => w.Pause(), Times.Once);
            _mockWallpaper2.Verify(w => w.Pause(), Times.Once);
        }

        [TestMethod]
        public void RunPlayback_WhenBatteryOffline_AndSettingIsSilence_ShouldSilenceAllWallpapers() {
            _mockPowerService.Setup(p => p.GetACPowerStatus())
                .Returns(PowerUtil.ACLineStatus.Offline);
            _mockSettings.Setup(s => s.BatteryPoweredn).Returns(AppWpRunRulesEnum.Silence);
            var playback = CreatePlayback();

            playback.InvokeRunPlayback();

            _mockWallpaper1.Verify(w => w.Play(), Times.Once);
            _mockWallpaper1.Verify(w => w.SetMute(true), Times.Once);
            _mockWallpaper2.Verify(w => w.Play(), Times.Once);
            _mockWallpaper2.Verify(w => w.SetMute(true), Times.Once);
        }

        [TestMethod]
        public void RunPlayback_WhenBatteryOnline_AndSettingIsPause_ShouldNotPause() {
            _mockPowerService.Setup(p => p.GetACPowerStatus())
                .Returns(PowerUtil.ACLineStatus.Online);
            _mockSettings.Setup(s => s.BatteryPoweredn).Returns(AppWpRunRulesEnum.Pause);
            var playback = CreatePlayback();

            playback.InvokeRunPlayback();

            _mockWallpaper1.Verify(w => w.Pause(), Times.Never);
        }

        // -------------------------------------------------------
        // RunPlayback - 省电模式
        // -------------------------------------------------------

        [TestMethod]
        public void RunPlayback_WhenPowerSavingOn_AndSettingIsPause_ShouldPauseAllWallpapers() {
            _mockPowerService.Setup(p => p.GetBatterySaverStatus())
                .Returns(PowerUtil.SystemStatusFlag.On);
            _mockSettings.Setup(s => s.PowerSaving).Returns(AppWpRunRulesEnum.Pause);
            var playback = CreatePlayback();

            playback.InvokeRunPlayback();

            _mockWallpaper1.Verify(w => w.Pause(), Times.Once);
            _mockWallpaper2.Verify(w => w.Pause(), Times.Once);
        }

        [TestMethod]
        public void RunPlayback_WhenPowerSavingOn_AndSettingIsSilence_ShouldSilenceAllWallpapers() {
            _mockPowerService.Setup(p => p.GetBatterySaverStatus())
                .Returns(PowerUtil.SystemStatusFlag.On);
            _mockSettings.Setup(s => s.PowerSaving).Returns(AppWpRunRulesEnum.Silence);
            var playback = CreatePlayback();

            playback.InvokeRunPlayback();

            _mockWallpaper1.Verify(w => w.Play(), Times.Once);
            _mockWallpaper1.Verify(w => w.SetMute(true), Times.Once);
            _mockWallpaper2.Verify(w => w.Play(), Times.Once);
            _mockWallpaper2.Verify(w => w.SetMute(true), Times.Once);
        }

        [TestMethod]
        public void RunPlayback_WhenPowerSavingOff_AndSettingIsPause_ShouldNotPause() {
            _mockPowerService.Setup(p => p.GetBatterySaverStatus())
                .Returns(PowerUtil.SystemStatusFlag.Off);
            _mockSettings.Setup(s => s.PowerSaving).Returns(AppWpRunRulesEnum.Pause);
            var playback = CreatePlayback();

            playback.InvokeRunPlayback();

            _mockWallpaper1.Verify(w => w.Pause(), Times.Never);
        }
    }
}
