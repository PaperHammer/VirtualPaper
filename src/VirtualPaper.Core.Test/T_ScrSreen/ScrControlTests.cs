using System.Diagnostics;
using System.Text.Json;
using Moq;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Cores.ScreenSaver;
using VirtualPaper.Cores.WpControl;
using VirtualPaper.Models.Cores;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Utils.Interfcaes;
using MockFactory = VirtualPaper.Core.Test.Infrastructure.MockFactory;

namespace VirtualPaper.Core.Test.T_ScrSreen {
    [TestClass]
    [TestCategory("Backend")]
    public class ScrControlTests {
        private ScrControl _sut = null!;
        private Mock<IUserSettingsService> _settings = null!;
        private Mock<IWallpaperControl> _wpControl = null!;
        private Mock<IRawInputMsg> _msgWindow = null!;
        private Mock<IDispatcherTimer> _timer = null!;
        private Mock<INativeService> _native = null!;
        private Mock<IProcessLauncher> _launcher = null!;
        private Mock<IJobService> _jobService = null!;

        private EventHandler? _capturedTick;

        [TestInitialize]
        public void Setup() {
            _settings = MockFactory.CreateUserSettings();
            _wpControl = new Mock<IWallpaperControl>();
            _msgWindow = new Mock<IRawInputMsg>();
            _native = new Mock<INativeService>();
            _launcher = new Mock<IProcessLauncher>();
            _jobService = new Mock<IJobService>();

            _timer = new Mock<IDispatcherTimer>();
            _timer
                .SetupAdd(t => t.Tick += It.IsAny<EventHandler>())
                .Callback<EventHandler>(h => _capturedTick += h);
            _timer
                .SetupRemove(t => t.Tick -= It.IsAny<EventHandler>())
                .Callback<EventHandler>(h => _capturedTick -= h);

            // 默认：主壁纸存在
            _wpControl
                .Setup(w => w.GetPrimaryWpFilePathRType())
                .Returns(("C:\\wp\\valid.jpg", RuntimeType.RImage));

            // 默认：系统通知状态正常
            var normalState = Native.QUERY_USER_NOTIFICATION_STATE.QUNS_ACCEPTS_NOTIFICATIONS;
            _native
                .Setup(n => n.SHQueryUserNotificationState(out normalState))
                .Returns(0);

            // 默认：前台进程不在白名单
            _native.Setup(n => n.GetForegroundWindow()).Returns(new nint(1));
            _native.Setup(n => n.GetWindowThreadProcessId(It.IsAny<nint>(), out It.Ref<int>.IsAny))
                   .Returns(0);
            _native.Setup(n => n.GetProcessNameById(It.IsAny<int>())).Returns("explorer");

            // 默认：进程未退出
            _launcher.Setup(l => l.HasExited).Returns(false);
            
            _settings.Setup(s => s.Settings).Returns(new Settings() {
                IsScreenSaverOn = true,
            });

            _sut = BuildSut();
        }

        private ScrControl BuildSut() => new(
            _settings.Object, _wpControl.Object, _msgWindow.Object,
            _timer.Object, _native.Object, _launcher.Object, _jobService.Object);

        private void FireTick() =>
            _capturedTick?.Invoke(_timer.Object, EventArgs.Empty);

        /// <summary>
        /// 模拟屏保进程通过 OutputDataReceived 发出 WpLoaded 信号
        /// </summary>
        private void SimulateWpLoaded() {
            var msg = new VirtualPaperMessageWallpaperLoaded() { Success = true };
            var json = JsonSerializer.Serialize(msg, IpcMessageContext.Default.IpcMessage);
            _launcher.Raise(
                l => l.OutputDataReceived += null,
                new ProcessOutputEventArgs(json));
        }

        /// <summary>
        /// 模拟屏保进程退出
        /// </summary>
        private void SimulateProcessExited() {
            _launcher.Setup(l => l.HasExited).Returns(true);
            _launcher.Raise(l => l.Exited += null, EventArgs.Empty);
        }

        // ----------------------------------------------------------------
        // Start / Stop 生命周期
        // ----------------------------------------------------------------

        [TestMethod]
        [Description("Start should configure timer interval from settings and start it")]
        public void Start_ShouldConfigureAndStartTimer() {
            _settings.Object.Settings.WaitingTime = 5;

            _sut.Start();

            _timer.VerifySet(t => t.Interval = TimeSpan.FromMinutes(5), Times.Once);
            _timer.Verify(t => t.Start(), Times.Once);
        }

        [TestMethod]
        [Description("Start should not start the timer if it is already timing")]
        public void Start_WhenAlreadyTiming_ShouldNotStartAgain() {
            _sut.Start();
            _sut.Start();

            _timer.Verify(t => t.Start(), Times.Once,
                "Timer should only be started once");
        }

        [TestMethod]
        [Description("Start should not start the timer if screensaver is already running")]
        public void Start_WhenAlreadyRunning_ShouldNotStartTimer() {
            _sut.Start();
            FireTick();
            SimulateWpLoaded(); // IsRunning = true

            _sut.Start();

            _timer.Verify(t => t.Start(), Times.Once);
        }

        [TestMethod]
        [Description("Stop should stop the timer")]
        public void Stop_ShouldStopTimer() {
            _sut.Start();

            _sut.Stop();

            _timer.Verify(t => t.Stop(), Times.AtLeastOnce);
        }

        // ----------------------------------------------------------------
        // Timer Tick 触发逻辑
        // ----------------------------------------------------------------

        [TestMethod]
        [Description("Tick should stop the timer before evaluating launch conditions")]
        public void Tick_ShouldStopTimerFirst() {
            _sut.Start();

            FireTick();

            _timer.Verify(t => t.Stop(), Times.AtLeastOnce);
        }

        [TestMethod]
        [Description("Tick should not launch screensaver when primary wallpaper is null")]
        public void Tick_WhenNoPrimaryWallpaper_ShouldNotLaunch() {
            _wpControl
                .Setup(w => w.GetPrimaryWpFilePathRType())
                .Returns((null, RuntimeType.RUnknown));
            _settings.Object.Settings.IsScreenSaverOn = true;
            _sut.Start();

            FireTick();

            _launcher.Verify(l => l.Launch(It.IsAny<ProcessStartInfo>()), Times.Never);
        }

        [TestMethod]
        [Description("Tick should not launch when system notification state is busy")]
        public void Tick_WhenSystemIsBusy_ShouldNotLaunch() {
            var busyState = Native.QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY;
            _native
                .Setup(n => n.SHQueryUserNotificationState(out busyState))
                .Returns(0);
            _settings.Object.Settings.IsScreenSaverOn = true;
            _sut.Start();

            FireTick();

            _launcher.Verify(l => l.Launch(It.IsAny<ProcessStartInfo>()), Times.Never);
        }

        [TestMethod]
        [Description("Tick should not launch when foreground process is in the whitelist")]
        public void Tick_WhenForegroundProcInWhitelist_ShouldNotLaunch() {
            _native.Setup(n => n.GetProcessNameById(It.IsAny<int>())).Returns("chrome");
            _sut.AddToWhiteList("chrome");
            _settings.Object.Settings.IsScreenSaverOn = true;
            _sut.Start();

            FireTick();

            _launcher.Verify(l => l.Launch(It.IsAny<ProcessStartInfo>()), Times.Never);
        }

        [TestMethod]
        [Description("Tick should launch screensaver when all conditions are met")]
        public void Tick_WhenAllConditionsMet_ShouldLaunchProcess() {
            _sut.Start();

            FireTick();

            _launcher.Verify(l => l.Launch(It.IsAny<ProcessStartInfo>()), Times.Once);
        }

        [TestMethod]
        [Description("Tick should call BeginOutputReadLine after launch to start reading stdout")]
        public void Tick_AfterLaunch_ShouldBeginOutputReadLine() {
            _sut.Start();

            FireTick();

            _launcher.Verify(l => l.BeginOutputReadLine(), Times.Once,
                "BeginOutputReadLine must be called so OutputDataReceived events fire");
        }

        [TestMethod]
        [Description("Tick should pass correct file path and rtype as process arguments")]
        public void Tick_ShouldPassCorrectArgsToProcess() {
            _wpControl
                .Setup(w => w.GetPrimaryWpFilePathRType())
                .Returns(("C:\\wp\\test.mp4", RuntimeType.RVideo));

            ProcessStartInfo? capturedInfo = null;
            _launcher
                .Setup(l => l.Launch(It.IsAny<ProcessStartInfo>()))
                .Callback<ProcessStartInfo>(info => capturedInfo = info);

            _sut.Start();
            FireTick();

            Assert.IsNotNull(capturedInfo);
            Assert.Contains("C:\\wp\\test.mp4", capturedInfo.Arguments);
            Assert.Contains(RuntimeType.RVideo.ToString(), capturedInfo.Arguments);
        }

        [TestMethod]
        [Description("Launcher should be called with a valid .exe path")]
        public void Tick_ShouldLaunchCorrectExecutable() {
            ProcessStartInfo? captured = null;
            _launcher
                .Setup(l => l.Launch(It.IsAny<ProcessStartInfo>()))
                .Callback<ProcessStartInfo>(psi => captured = psi);
            _sut.Start();

            FireTick();

            Assert.IsNotNull(captured);
            Assert.EndsWith(".exe", captured.FileName,
                "Launcher should point to a valid executable");
        }

        // ----------------------------------------------------------------
        // Launch 失败处理
        // ----------------------------------------------------------------

        [TestMethod]
        [Description("When launcher throws, should not propagate exception and should restart timer")]
        public void Tick_WhenLaunchThrows_ShouldNotCrashAndRestartTimer() {
            _launcher
                .Setup(l => l.Launch(It.IsAny<ProcessStartInfo>()))
                .Throws(new InvalidOperationException("Process start failed"));
            _sut.Start();

            try {
                FireTick();
            }
            catch {
                Assert.Fail("Exception should be handled internally, not propagated to caller");
            }

            // 计时器应被重新启动，恢复等待状态
            _timer.Verify(t => t.Start(), Times.AtLeast(2),
                "Timer should be restarted after launch failure");
        }

        [TestMethod]
        [Description("When launcher throws, IsRunning should remain false")]
        public void Tick_WhenLaunchThrows_IsRunningShouldRemainFalse() {
            _launcher
                .Setup(l => l.Launch(It.IsAny<ProcessStartInfo>()))
                .Throws(new InvalidOperationException("Process start failed"));
            _sut.Start();

            try { FireTick(); } catch { /* ignored */ }

            Assert.IsFalse(_sut.IsRunning);
        }

        // ----------------------------------------------------------------
        // OutputDataReceived → WpLoaded → 状态变化
        // ----------------------------------------------------------------

        [TestMethod]
        [Description("After WpLoaded signal received, IsRunning should become true")]
        public void OutputDataReceived_WpLoaded_IsRunningShouldBecomeTrue() {
            _sut.Start();
            
            Assert.IsNotNull(_capturedTick, "Tick未被注册");

            FireTick();

            _launcher.Verify(l => l.Launch(It.IsAny<ProcessStartInfo>()), Times.Once, "Launch未被调用，Tick内部提前return");

            SimulateWpLoaded();

            Assert.IsTrue(_sut.IsRunning,
                "IsRunning should be true after process signals WpLoaded");
        }

        [TestMethod]
        [Description("After WpLoaded, timer should not restart (screensaver is active)")]
        public void OutputDataReceived_WpLoaded_TimerShouldNotRestart() {
            _sut.Start();
            FireTick();
            int startCountAfterTick = _timer.Invocations
                .Count(i => i.Method.Name == nameof(IDispatcherTimer.Start));

            SimulateWpLoaded();

            int startCountAfterLoaded = _timer.Invocations
                .Count(i => i.Method.Name == nameof(IDispatcherTimer.Start));

            Assert.AreEqual(startCountAfterTick, startCountAfterLoaded,
                "Timer should not restart while screensaver is actively running");
        }

        [TestMethod]
        [Description("OutputDataReceived with null data should not throw")]
        public void OutputDataReceived_NullData_ShouldNotThrow() {
            _sut.Start();
            FireTick();

            try {
                _launcher.Raise(
                    l => l.OutputDataReceived += null,
                    new ProcessOutputEventArgs(null));
            }
            catch {
                Assert.Fail("Null OutputData should be handled gracefully");
            }
        }

        [TestMethod]
        [Description("OutputDataReceived with unrecognized data should not affect IsRunning")]
        public void OutputDataReceived_UnrecognizedData_IsRunningShouldRemainFalse() {
            _sut.Start();
            FireTick();

            _launcher.Raise(
                l => l.OutputDataReceived += null,
                new ProcessOutputEventArgs("some_random_unrecognized_output"));

            Assert.IsFalse(_sut.IsRunning);
        }

        // ----------------------------------------------------------------
        // 进程退出处理
        // ----------------------------------------------------------------

        [TestMethod]
        [Description("When process exits normally, IsRunning should become false")]
        public void ProcessExited_Normal_IsRunningShouldBecomeFalse() {
            _sut.Start();
            FireTick();
            SimulateWpLoaded(); // IsRunning = true

            SimulateProcessExited();

            Assert.IsFalse(_sut.IsRunning,
                "IsRunning should be false after process exits");
        }

        [TestMethod]
        [Description("When process exits normally, timer should restart to allow re-trigger")]
        public void ProcessExited_Normal_TimerShouldRestart() {
            _sut.Start();
            FireTick();
            SimulateWpLoaded();

            SimulateProcessExited();

            _timer.Verify(t => t.Start(), Times.AtLeast(2),
                "Timer should restart after process exits to allow next trigger");
        }

        [TestMethod]
        [Description("When process exits unexpectedly (before WpLoaded), state should still reset")]
        public void ProcessExited_BeforeWpLoaded_ShouldResetState() {
            _sut.Start();
            FireTick();
            // 不调用 SimulateWpLoaded，直接退出

            SimulateProcessExited();

            Assert.IsFalse(_sut.IsRunning);
            _timer.Verify(t => t.Start(), Times.AtLeast(2));
        }

        [TestMethod]
        [Description("When process exits, it should be disposed")]
        public void ProcessExited_ShouldDisposeProcess() {
            _sut.Start();
            FireTick();
            SimulateWpLoaded();

            SimulateProcessExited();

            _launcher.Verify(l => l.Dispose(), Times.Once,
                "Process should be disposed after exit");
        }

        // ----------------------------------------------------------------
        // 白名单管理
        // ----------------------------------------------------------------

        [TestMethod]
        [Description("AddToWhiteList should block launch when that process is in foreground")]
        public void AddToWhiteList_ShouldBlockLaunch() {
            _native.Setup(n => n.GetProcessNameById(It.IsAny<int>())).Returns("notepad");

            _sut.AddToWhiteList("notepad");
            _sut.Start();
            FireTick();

            _launcher.Verify(l => l.Launch(It.IsAny<ProcessStartInfo>()), Times.Never);
        }

        [TestMethod]
        [Description("RemoveFromWhiteList should allow launch after removal")]
        public void RemoveFromWhiteList_ShouldAllowLaunchAfterRemoval() {
            _native.Setup(n => n.GetProcessNameById(It.IsAny<int>())).Returns("notepad");
            _sut.AddToWhiteList("notepad");

            _sut.RemoveFromWhiteList("notepad");
            _sut.Start();
            FireTick();

            _launcher.Verify(l => l.Launch(It.IsAny<ProcessStartInfo>()), Times.Once);
        }

        [TestMethod]
        [Description("AddToWhiteList should be case-insensitive")]
        public void AddToWhiteList_CaseInsensitive_ShouldBlockLaunch() {
            _native.Setup(n => n.GetProcessNameById(It.IsAny<int>())).Returns("Notepad");

            _sut.AddToWhiteList("notepad"); // 小写加入
            _sut.Start();
            FireTick();

            _launcher.Verify(l => l.Launch(It.IsAny<ProcessStartInfo>()), Times.Never,
                "Whitelist check should be case-insensitive");
        }

        [TestMethod]
        [Description("Adding duplicate entries to whitelist should not cause issues")]
        public void AddToWhiteList_Duplicate_ShouldNotThrow() {
            try {
                _sut.AddToWhiteList("notepad");
                _sut.AddToWhiteList("notepad");
            }
            catch {
                Assert.Fail("Adding duplicate whitelist entry should not throw");
            }
        }
    }
}