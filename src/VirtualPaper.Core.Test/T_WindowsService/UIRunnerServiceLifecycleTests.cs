using System.Diagnostics;
using System.Reflection;
using Moq;
using VirtualPaper.Services;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Core.Test.T_WindowsService;

[TestClass, DoNotParallelize]
public class UIRunnerServiceLifecycleTests {
    [TestMethod]
    public void ExitCallback_DoesNotWaitForLifecycleLock() {
        var jobs = new Mock<IJobService>(MockBehavior.Strict);
        using var service = new UIRunnerService(jobs.Object);
        using var process = new Process();
        var gate = typeof(UIRunnerService).GetField("_processLock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        var callback = typeof(UIRunnerService).GetMethod("Proc_UI_Exited", BindingFlags.Instance | BindingFlags.NonPublic)!;
        using var started = new ManualResetEventSlim();
        Task invocation;
        bool returnedWhileLocked;
        lock (gate) {
            invocation = Task.Run(() => {
                started.Set();
                callback.Invoke(service, [process, EventArgs.Empty]);
            });
            Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(5)), "Exit callback worker did not start");
            returnedWhileLocked = invocation.Wait(TimeSpan.FromSeconds(1));
        }
        Assert.IsTrue(invocation.Wait(TimeSpan.FromSeconds(2)), "Exit callback did not drain after releasing the lifecycle lock");
        Assert.IsTrue(returnedWhileLocked, "Process raises Exited under its internal monitor; waiting for the lifecycle lock here can deadlock Process.Dispose");
        jobs.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void QueuedOldExitCallback_DoesNotClearOrStopReplacement() {
        var jobs = new Mock<IJobService>(MockBehavior.Strict);
        using var service = new UIRunnerService(jobs.Object);
        using var oldProcess = new Process();
        using var replacement = new Process();
        var field = typeof(UIRunnerService).GetField("_processUI", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(service, replacement);
        typeof(UIRunnerService).GetMethod("HandleUiExited", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(service, [oldProcess]);
        Assert.AreSame(replacement, field.GetValue(service));
        jobs.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DisposedService_DoesNotRestartOrShowUi() {
        var jobs = new Mock<IJobService>(MockBehavior.Strict);
        using var service = new UIRunnerService(jobs.Object);
        service.Dispose();
        service.RestartUI();
        await service.ShowUIAsync();
        Assert.AreEqual(nint.Zero, service.GetUIHwnd());
        jobs.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ShuttingDownApp_DoesNotStartPendingUi() {
        bool previous = App.IsShuttingDown;
        var jobs = new Mock<IJobService>(MockBehavior.Strict);
        using var service = new UIRunnerService(jobs.Object);
        try {
            App.IsShuttingDown = true;
            await service.ShowUIAsync();
            service.RestartUI();
            Assert.AreEqual(nint.Zero, service.GetUIHwnd());
            jobs.VerifyNoOtherCalls();
        }
        finally { App.IsShuttingDown = previous; }
    }
}
