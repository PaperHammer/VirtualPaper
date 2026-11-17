using System.ComponentModel;
using Moq;
using VirtualPaper.Common;
using VirtualPaper.Core.Test.Infrastructure;
using VirtualPaper.Cores;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Cores.WpControl;
using VirtualPaper.Factories.Interfaces;
using VirtualPaper.Models.Cores.Interfaces;
using VirtualPaper.Services.Interfaces;
using VirtualPaper.Utils.Interfcaes;
using MockFactory = VirtualPaper.Core.Test.Infrastructure.MockFactory;

namespace VirtualPaper.Core.Test.T_WallpaperControl;

[TestClass]
[TestCategory("Backend")]
[DoNotParallelize]
public class WallpaperControl_FailureRecoveryTests {
    [TestCleanup]
    public void Cleanup() {
        foreach (var player in _players)
            player.Setup(item => item.Close()).Callback(() => player.Object.Closing?.Invoke(player.Object, EventArgs.Empty));
        _sut?.Dispose();
        TestDataBuilder.CleanupTempFiles(_files);
    }

    [TestMethod]
    [DataRow(WallpaperArrangement.Per, "show-false")]
    [DataRow(WallpaperArrangement.Per, "show-throws")]
    [DataRow(WallpaperArrangement.Per, "attach-false")]
    [DataRow(WallpaperArrangement.Per, "attach-throws")]
    [DataRow(WallpaperArrangement.Expand, "show-false")]
    [DataRow(WallpaperArrangement.Expand, "show-throws")]
    [DataRow(WallpaperArrangement.Expand, "attach-false")]
    [DataRow(WallpaperArrangement.Expand, "attach-throws")]
    [DataRow(WallpaperArrangement.Duplicate, "show-false")]
    [DataRow(WallpaperArrangement.Duplicate, "show-throws")]
    [DataRow(WallpaperArrangement.Duplicate, "attach-false")]
    [DataRow(WallpaperArrangement.Duplicate, "attach-throws")]
    public async Task FailedStartup_ClosesCandidateAndAllowsNextAttempt(WallpaperArrangement arrangement, string phase) {
        Initialize(arrangement);
        var monitor = _monitors.Object.PrimaryMonitor;
        var data = NewData("failed");
        var failed = NewPlayer(data, monitor);
        var recovered = NewPlayer(NewData("recovered"), monitor);
        _factory.SetupSequence(factory => factory.CreatePlayer(It.IsAny<IWpPlayerData>(), monitor, false))
            .Returns(failed.Object).Returns(recovered.Object);
        if (phase == "show-false") failed.Setup(player => player.ShowAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        if (phase == "show-throws") failed.Setup(player => player.ShowAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("Startup failed"));
        if (phase == "attach-false") _desktop.Setup(service => service.TrySetParentWorkerW(It.IsAny<nint>(), It.IsAny<nint>())).Returns(false);
        if (phase == "attach-throws") _desktop.Setup(service => service.TrySetParentWorkerW(It.IsAny<nint>(), It.IsAny<nint>())).Throws(new IOException("Desktop changed"));

        Assert.IsFalse((await _sut.SetWallpaperAsync(data, monitor)).IsFinished);
        failed.Verify(player => player.Close(), Times.Once);
        Assert.IsEmpty(_sut.Wallpapers);
        Assert.IsEmpty(_savedMonitorIds);
        Assert.IsNull(failed.Object.Closing);
        _jobs.Verify(jobs => jobs.AddProcess(It.IsAny<int>(), PluginName.PlayerWeb), Times.Never);

        _desktop.Setup(service => service.TrySetParentWorkerW(It.IsAny<nint>(), It.IsAny<nint>())).Returns(true);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.IsTrue((await _sut.SetWallpaperAsync(recovered.Object.Data, monitor, token: timeout.Token)).IsFinished);
        Assert.AreSame(recovered.Object, _sut.Wallpapers.Single());
        CollectionAssert.AreEqual(new[] { monitor.DeviceId }, _savedMonitorIds.ToArray());
        _jobs.Verify(jobs => jobs.AddProcess(It.IsAny<int>(), PluginName.PlayerWeb), Times.Once);
    }

    [TestMethod]
    [DataRow(WallpaperArrangement.Per)]
    [DataRow(WallpaperArrangement.Expand)]
    [DataRow(WallpaperArrangement.Duplicate)]
    public async Task MissingPlayerExecutable_ReportsPluginErrorAndReleasesStartupGate(WallpaperArrangement arrangement) {
        Initialize(arrangement);
        var monitor = _monitors.Object.PrimaryMonitor;
        var data = NewData("wallpaper");
        var recovered = NewPlayer(data, monitor);
        _factory.SetupSequence(factory => factory.CreatePlayer(data, monitor, false))
            .Throws(new Win32Exception(2)).Returns(recovered.Object);
        Exception? error = null;
        _sut.WallpaperError += (_, exception) => error = exception;

        Assert.IsFalse((await _sut.SetWallpaperAsync(data, monitor)).IsFinished);
        Assert.IsInstanceOfType<Errors.WallpaperPluginNotFoundException>(error);
        Assert.IsEmpty(_sut.Wallpapers);
        Assert.IsEmpty(_savedMonitorIds);
        _jobs.Verify(jobs => jobs.AddProcess(It.IsAny<int>(), PluginName.PlayerWeb), Times.Never);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.IsTrue((await _sut.SetWallpaperAsync(data, monitor, token: timeout.Token)).IsFinished);
        Assert.AreSame(recovered.Object, _sut.Wallpapers.Single());
    }

    [TestMethod]
    [DataRow(WallpaperArrangement.Per)]
    [DataRow(WallpaperArrangement.Expand)]
    [DataRow(WallpaperArrangement.Duplicate)]
    public async Task CancelledStartup_WhenCleanupAlsoFails_PreservesCancellationAndAllowsRetry(WallpaperArrangement arrangement) {
        Initialize(arrangement);
        var monitor = _monitors.Object.PrimaryMonitor;
        var data = NewData("cancelled");
        var cancelled = NewPlayer(data, monitor);
        var recovered = NewPlayer(NewData("recovered"), monitor);
        _factory.SetupSequence(factory => factory.CreatePlayer(It.IsAny<IWpPlayerData>(), monitor, false))
            .Returns(cancelled.Object).Returns(recovered.Object);
        using var cancellation = new CancellationTokenSource();
        cancelled.Setup(player => player.ShowAsync(It.IsAny<CancellationToken>())).Returns<CancellationToken>(token => {
            cancellation.Cancel();
            return Task.FromCanceled<bool>(token);
        });
        cancelled.Setup(player => player.Close()).Throws(new IOException("Cleanup failed"));
        Exception? error = null;
        _sut.WallpaperError += (_, exception) => error = exception;

        Assert.IsFalse((await _sut.SetWallpaperAsync(data, monitor, token: cancellation.Token)).IsFinished);
        Assert.IsInstanceOfType<OperationCanceledException>(error);
        cancelled.Verify(player => player.Close(), Times.Once);
        Assert.IsEmpty(_sut.Wallpapers);
        Assert.IsEmpty(_savedMonitorIds);
        _jobs.Verify(jobs => jobs.AddProcess(It.IsAny<int>(), PluginName.PlayerWeb), Times.Never);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.IsTrue((await _sut.SetWallpaperAsync(recovered.Object.Data, monitor, token: timeout.Token)).IsFinished);
        Assert.AreSame(recovered.Object, _sut.Wallpapers.Single());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CloseFailure_StillCleansRegistrationAndOtherTargets_AndRepeatedCloseIsHarmless(bool closeAll) {
        Initialize(WallpaperArrangement.Per, 2);
        var first = await AddPlayer(_monitors.Object.Monitors[0], "first");
        var second = await AddPlayer(_monitors.Object.Monitors[1], "second");
        first.Setup(player => player.Close()).Throws(new IOException("Close handler failed"));
        int changed = 0;
        _sut.WallpaperChanged += (_, _) => changed++;

        void Close() {
            if (closeAll) _sut.CloseAllWallpapers(); else _sut.CloseWallpaper(first.Object.Monitor);
        }
        Close();
        Close();

        first.Verify(player => player.Close(), Times.Once);
        second.Verify(player => player.Close(), closeAll ? Times.Once : Times.Never);
        Assert.AreEqual(1, changed);
        Assert.IsNull(first.Object.Closing);
        CollectionAssert.AreEqual(closeAll ? Array.Empty<string>() : new[] { second.Object.Monitor!.DeviceId }, _savedMonitorIds.ToArray());
        CollectionAssert.AreEqual(closeAll ? Array.Empty<IWpPlayer>() : new[] { second.Object }, _sut.Wallpapers.ToArray());
        _jobs.Verify(jobs => jobs.StopPlugin(It.IsAny<int>()), Times.Exactly(closeAll ? 2 : 1));
    }

    [TestMethod]
    public async Task DelayedOldClosingNotification_DoesNotRemoveOrStopReplacement() {
        Initialize(WallpaperArrangement.Per);
        var monitor = _monitors.Object.PrimaryMonitor;
        var old = await AddPlayer(monitor, "same-wallpaper");
        var delayed = old.Object.Closing!;
        _sut.CloseWallpaper(monitor);
        var replacement = await AddPlayer(monitor, "same-wallpaper");
        // Real players compare wallpaper identity, which is weaker than instance identity.
        replacement.Setup(player => player.Equals(old.Object)).Returns(true);
        int stops = _jobs.Invocations.Count(call => call.Method.Name == nameof(IJobService.StopPlugin));

        delayed(old.Object, EventArgs.Empty);
        delayed(old.Object, EventArgs.Empty);

        Assert.AreSame(replacement.Object, _sut.Wallpapers.Single());
        Assert.IsNotNull(replacement.Object.Closing);
        replacement.Verify(player => player.Close(), Times.Never);
        Assert.AreEqual(stops, _jobs.Invocations.Count(call => call.Method.Name == nameof(IJobService.StopPlugin)));
        CollectionAssert.AreEqual(new[] { monitor.DeviceId }, _savedMonitorIds.ToArray());
    }

    [TestMethod]
    public async Task ClosePlayer_CapturesProcessBeforeCloseAndStopsItAfterward() {
        Initialize(WallpaperArrangement.Per);
        var player = await AddPlayer(_monitors.Object.PrimaryMonitor, "wallpaper");
        var process = player.Object.Proc;
        bool closed = false;
        bool closedWhenStopped = false;
        player.SetupGet(item => item.Proc).Returns(() => closed ? throw new ObjectDisposedException("Process") : process);
        player.Setup(item => item.Close()).Callback(() => closed = true);
        _jobs.Setup(jobs => jobs.StopPlugin(process.Id)).Callback(() => closedWhenStopped = closed);

        _sut.CloseAllWallpapers();
        _sut.CloseAllWallpapers();

        player.Verify(item => item.Close(), Times.Once);
        _jobs.Verify(jobs => jobs.StopPlugin(process.Id), Times.Once);
        Assert.IsTrue(closedWhenStopped);
        Assert.IsNull(player.Object.Closing);
        Assert.IsEmpty(_sut.Wallpapers);
        Assert.IsEmpty(_savedMonitorIds);
    }

    [TestMethod]
    public async Task RepeatedPlayerClosingNotification_CleansOnlyThatInstanceOnce() {
        Initialize(WallpaperArrangement.Per, 2);
        var first = await AddPlayer(_monitors.Object.Monitors[0], "first");
        var second = await AddPlayer(_monitors.Object.Monitors[1], "second");
        var closing = first.Object.Closing!;

        closing(first.Object, EventArgs.Empty);
        closing(first.Object, EventArgs.Empty);

        Assert.AreSame(second.Object, _sut.Wallpapers.Single());
        Assert.IsNull(first.Object.Closing);
        Assert.IsNotNull(second.Object.Closing);
        first.Verify(player => player.Close(), Times.Never);
        second.Verify(player => player.Close(), Times.Never);
        _jobs.Verify(jobs => jobs.StopPlugin(It.IsAny<int>()), Times.Once);
    }

    [TestMethod]
    public async Task CloseAfterMonitorRemoval_CleansItsPlayerWithoutTouchingRemainingMonitor() {
        Initialize(WallpaperArrangement.Per, 2);
        var removed = _monitors.Object.Monitors[0];
        var first = await AddPlayer(removed, "removed");
        var second = await AddPlayer(_monitors.Object.Monitors[1], "remaining");
        _monitors.Object.Monitors.Remove(removed);

        _sut.CloseWallpaper(removed);
        _sut.CloseWallpaper(removed);

        first.Verify(player => player.Close(), Times.Once);
        second.Verify(player => player.Close(), Times.Never);
        Assert.AreSame(second.Object, _sut.Wallpapers.Single());
        CollectionAssert.AreEqual(new[] { second.Object.Monitor!.DeviceId }, _savedMonitorIds.ToArray());
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public async Task DuplicatePartialFailure_PersistsOnlyStartedPlayersAndUpdatesTheirOwnThumbnails(int failedIndex) {
        Initialize(WallpaperArrangement.Duplicate, 3);
        var data = NewData("duplicate");
        var players = _monitors.Object.Monitors.Select(monitor => NewPlayer(data, monitor)).ToArray();
        for (int i = 0; i < players.Length; i++) {
            var player = players[i];
            _factory.Setup(factory => factory.CreatePlayer(data, player.Object.Monitor!, false)).Returns(player.Object);
        }
        players[failedIndex].Setup(player => player.ShowAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        Assert.IsFalse((await _sut.SetWallpaperAsync(data, _monitors.Object.PrimaryMonitor)).IsFinished);

        var started = players.Where((_, index) => index != failedIndex).Select(player => player.Object).ToArray();
        CollectionAssert.AreEqual(started, _sut.Wallpapers.ToArray());
        CollectionAssert.AreEqual(started.Select(player => player.Monitor!.DeviceId).ToArray(), _savedMonitorIds.ToArray());
        for (int i = 0; i < players.Length; i++) {
            players[i].Verify(player => player.Close(), i == failedIndex ? Times.Once : Times.Never);
            Assert.AreEqual(i == failedIndex ? "" : data.ThumbnailPath, _monitors.Object.Monitors[i].ThumbnailPath);
        }
    }

    [TestMethod]
    public async Task DuplicateStartupException_PersistsEarlierSuccessWithoutRegisteringFailedPlayer() {
        Initialize(WallpaperArrangement.Duplicate, 3);
        var data = NewData("duplicate");
        var players = _monitors.Object.Monitors.Select(monitor => NewPlayer(data, monitor)).ToArray();
        foreach (var player in players)
            _factory.Setup(factory => factory.CreatePlayer(data, player.Object.Monitor!, false)).Returns(player.Object);
        players[1].Setup(player => player.ShowAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Win32Exception(2));
        Exception? error = null;
        _sut.WallpaperError += (_, exception) => error = exception;

        Assert.IsFalse((await _sut.SetWallpaperAsync(data, _monitors.Object.PrimaryMonitor)).IsFinished);

        Assert.IsInstanceOfType<Errors.WallpaperPluginNotFoundException>(error);
        Assert.AreSame(players[0].Object, _sut.Wallpapers.Single());
        CollectionAssert.AreEqual(new[] { players[0].Object.Monitor!.DeviceId }, _savedMonitorIds.ToArray());
        Assert.AreEqual(data.ThumbnailPath, _monitors.Object.Monitors[0].ThumbnailPath);
        Assert.AreEqual("", _monitors.Object.Monitors[1].ThumbnailPath);
        Assert.AreEqual("", _monitors.Object.Monitors[2].ThumbnailPath);
        players[0].Verify(player => player.Close(), Times.Never);
        players[1].Verify(player => player.Close(), Times.Once);
        players[2].Verify(player => player.ShowAsync(It.IsAny<CancellationToken>()), Times.Never);
        _jobs.Verify(jobs => jobs.AddProcess(It.IsAny<int>(), PluginName.PlayerWeb), Times.Once);
    }

    private void Initialize(WallpaperArrangement arrangement, int monitors = 1) {
        _arrangement = arrangement;
        var settings = MockFactory.CreateUserSettings(arrangement);
        settings.Setup(service => service.Save<List<IWallpaperLayout>>()).Callback(() => {
            _savedMonitorIds.Clear();
            _savedMonitorIds.AddRange(settings.Object.WallpaperLayouts.Select(layout => layout.MonitorDeviceId));
        });
        _monitors = MockFactory.CreateMonitorManager(monitors);
        _monitors.Setup(manager => manager.UpdateTargetMonitorThu(It.IsAny<int>(), It.IsAny<string>()))
            .Callback<int, string>((index, path) => _monitors.Object.Monitors[index].ThumbnailPath = path);
        _desktop = MockFactory.CreateDesktopService();
        _sut = new WallpaperControl(settings.Object, _monitors.Object, _factory.Object, _desktop.Object, _jobs.Object);
    }

    private IWpPlayerData NewData(string id) {
        var data = TestDataBuilder.CreateValidPlayerData(_files, wpId: id);
        data.SetupProperty(item => item.Arrangement, _arrangement);
        data.SetupGet(item => item.WpEffectFilePathUsing).Returns(data.Object.FilePath);
        data.SetupGet(item => item.ThumbnailPath).Returns(id + ".png");
        return data.Object;
    }

    private Mock<IWpPlayer> NewPlayer(IWpPlayerData data, IMonitor monitor) {
        var player = TestDataBuilder.CreateWpPlayer(data, monitor);
        _players.Add(player);
        return player;
    }

    private async Task<Mock<IWpPlayer>> AddPlayer(IMonitor monitor, string id) {
        var data = NewData(id);
        var player = NewPlayer(data, monitor);
        _factory.Setup(factory => factory.CreatePlayer(data, monitor, false)).Returns(player.Object);
        Assert.IsTrue((await _sut.SetWallpaperAsync(data, monitor)).IsFinished);
        return player;
    }

    private WallpaperControl _sut = null!;
    private WallpaperArrangement _arrangement;
    private Mock<IMonitorManager> _monitors = null!;
    private Mock<INativeService> _desktop = null!;
    private readonly Mock<IWallpaperFactory> _factory = new();
    private readonly Mock<IJobService> _jobs = new();
    private readonly List<Mock<IWpPlayer>> _players = [];
    private readonly List<string> _files = [];
    private readonly List<string> _savedMonitorIds = [];
}
