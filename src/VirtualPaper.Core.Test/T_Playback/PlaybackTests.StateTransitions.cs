using System.Reflection;
using Moq;
using VirtualPaper.Common;
using VirtualPaper.Cores;
using VirtualPaper.Cores.PlaybackControl;
using VirtualPaper.Models.Cores.Interfaces;

namespace VirtualPaper.Core.Test.T_Playback;

public partial class PlaybackTests {
    [TestMethod]
    [DataRow(AppWpRunRulesEnum.KeepRun, false)]
    [DataRow(AppWpRunRulesEnum.KeepRun, true)]
    [DataRow(AppWpRunRulesEnum.Silence, false)]
    [DataRow(AppWpRunRulesEnum.Silence, true)]
    [DataRow(AppWpRunRulesEnum.Pause, false)]
    [DataRow(AppWpRunRulesEnum.Pause, true)]
    public void StateTransition_ChangesOnlyTheRequestedMonitor_OrAllPlayers(AppWpRunRulesEnum rule, bool targeted) {
        var monitor = new VirtualPaper.Models.Cores.Monitor { DeviceId = "target" };
        _mockWallpaper1.Setup(player => player.Monitor).Returns(new VirtualPaper.Models.Cores.Monitor { DeviceId = "target" });
        _mockWallpaper2.Setup(player => player.Monitor).Returns(new VirtualPaper.Models.Cores.Monitor { DeviceId = "other" });
        // Keep a player without a monitor: global changes include it; targeted ones do not.
        var detached = new Mock<IWpPlayer>();
        _wallpapers.Add(detached.Object);
        bool[] paused = [true, true, true];
        bool[] muted = [false, false, false];
        Mock<IWpPlayer>[] players = [_mockWallpaper1, _mockWallpaper2, detached];
        for (int i = 0; i < players.Length; i++) {
            int index = i;
            players[i].Setup(player => player.Play()).Callback(() => paused[index] = false);
            players[i].Setup(player => player.Pause()).Callback(() => paused[index] = true);
            players[i].Setup(player => player.SetMute(It.IsAny<bool>())).Callback<bool>(value => muted[index] = value);
        }
        using var playback = CreatePlayback();
        ChangeState(playback, rule, targeted ? monitor : null);

        for (int i = 0; i < players.Length; i++) {
            bool affected = !targeted || i == 0;
            Assert.AreEqual(!affected || rule == AppWpRunRulesEnum.Pause, paused[i], $"Player {i}: playback state");
            Assert.AreEqual(affected && rule == AppWpRunRulesEnum.Silence, muted[i], $"Player {i}: mute state");
            players[i].Verify(player => player.Pause(),
                affected && rule == AppWpRunRulesEnum.Pause ? Times.Once : Times.Never);
            players[i].Verify(player => player.Play(),
                affected && rule != AppWpRunRulesEnum.Pause ? Times.Once : Times.Never);
            players[i].Verify(player => player.SetMute(It.IsAny<bool>()),
                affected && rule != AppWpRunRulesEnum.Pause ? Times.Once : Times.Never);
        }
    }

    [TestMethod]
    public void MonitorState_ResumeUnmutesPreviouslySilencedPlayer_WithoutResumingOtherMonitor() {
        var monitor = new VirtualPaper.Models.Cores.Monitor { DeviceId = "target" };
        _mockWallpaper1.Setup(player => player.Monitor).Returns(monitor);
        _mockWallpaper2.Setup(player => player.Monitor).Returns(new VirtualPaper.Models.Cores.Monitor { DeviceId = "other" });
        bool muted = false, paused = false;
        _mockWallpaper1.Setup(player => player.SetMute(It.IsAny<bool>())).Callback<bool>(value => muted = value);
        _mockWallpaper1.Setup(player => player.Pause()).Callback(() => paused = true);
        _mockWallpaper1.Setup(player => player.Play()).Callback(() => paused = false);
        using var playback = CreatePlayback();
        ChangeState(playback, AppWpRunRulesEnum.Silence, monitor);
        Assert.IsTrue(muted);
        ChangeState(playback, AppWpRunRulesEnum.Pause, monitor);
        Assert.IsTrue(paused);
        Assert.IsTrue(muted, "Pausing must preserve the existing audio state.");
        ChangeState(playback, AppWpRunRulesEnum.KeepRun, monitor);
        Assert.IsFalse(paused);
        Assert.IsFalse(muted);
        _mockWallpaper2.Verify(player => player.Play(), Times.Never);
        _mockWallpaper2.Verify(player => player.Pause(), Times.Never);
        _mockWallpaper2.Verify(player => player.SetMute(It.IsAny<bool>()), Times.Never);
    }

    private static void ChangeState(Playback playback, AppWpRunRulesEnum rule, IMonitor? monitor) =>
        typeof(Playback).GetMethod("ChangeWpState", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(playback, [rule, monitor]);
}
