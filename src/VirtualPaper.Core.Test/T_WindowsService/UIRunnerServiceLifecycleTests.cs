using System.Diagnostics;
using System.Reflection;
using Moq;
using VirtualPaper.Core.Test.Infrastructure;
using VirtualPaper.Services;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Core.Test.T_WindowsService;

[TestClass, DoNotParallelize]
public class UIRunnerServiceLifecycleTests {
    [TestMethod]
    public async Task ClosingTimeout_TerminatesOnlyTrackedProcessAndCleansPid() {
        using var environment = new WorkflowEnvironment();
        using var process = StartWaitingProcess();
        var jobs = new Mock<IJobService>(MockBehavior.Strict);
        jobs.Setup(j => j.StopPlugin(process.Id));
        using var service = new UIRunnerService(jobs.Object, TimeSpan.FromMilliseconds(500));
        TrackProcess(service, process.Id);

        service.NotifyUIClosing(process.Id);
        var watchdog = GetExitTask(service)!;
        service.NotifyUIClosing(process.Id);
        Assert.AreSame(watchdog, GetExitTask(service), "Duplicate notifications must not reset the deadline");

        Assert.IsTrue(await watchdog.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.IsTrue(process.HasExited);
        Assert.IsNull(GetTrackedProcess(service));
        jobs.Verify(j => j.StopPlugin(process.Id), Times.Once);
        jobs.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task NormalExit_CancelsWatchdogWithoutWaitingForTimeout() {
        using var environment = new WorkflowEnvironment();
        using var process = StartWaitingProcess();
        var jobs = new Mock<IJobService>(MockBehavior.Strict);
        jobs.Setup(j => j.StopPlugin(process.Id));
        using var service = new UIRunnerService(jobs.Object, TimeSpan.FromSeconds(10));
        TrackProcess(service, process.Id);
        service.NotifyUIClosing(process.Id);
        var watchdog = GetExitTask(service)!;

        await process.StandardInput.WriteLineAsync("finish");
        Assert.IsTrue(await watchdog.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.IsTrue(process.HasExited);
        Assert.AreEqual(0, process.ExitCode, "Normal exit must not be forcibly terminated");
        Assert.IsNull(GetTrackedProcess(service));
        jobs.Verify(j => j.StopPlugin(process.Id), Times.Once);
        jobs.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task StaleClosingNotification_DoesNotArmWatchdogForReplacement() {
        using var environment = new WorkflowEnvironment();
        using var oldProcess = StartWaitingProcess();
        using var replacement = StartWaitingProcess();
        var jobs = new Mock<IJobService>(MockBehavior.Strict);
        using var service = new UIRunnerService(jobs.Object, TimeSpan.FromMilliseconds(100));
        TrackProcess(service, oldProcess.Id);
        var gate = typeof(UIRunnerService).GetField("_processLock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        Task<bool> watchdog;
        lock (gate) {
            service.NotifyUIClosing(oldProcess.Id);
            watchdog = GetExitTask(service)!;
            typeof(UIRunnerService).GetMethod("ReleaseProcess", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(service, null);
            TrackProcess(service, replacement.Id);
        }
        service.NotifyUIClosing(oldProcess.Id);
        Assert.IsNull(GetExitTask(service));
        Assert.IsTrue(await watchdog.WaitAsync(TimeSpan.FromSeconds(5)));
        await Task.Delay(200);

        Assert.IsFalse(replacement.HasExited);
        Assert.AreEqual(replacement.Id, GetTrackedProcess(service)!.Id);
        // This fixture deliberately detached the old process while it was alive.
        oldProcess.Kill();
        Assert.IsTrue(oldProcess.WaitForExit(5000));
        jobs.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ShowUi_WhileClosingWaitsWithoutActivatingOldProcess() {
        using var environment = new WorkflowEnvironment();
        using var process = StartWaitingProcess();
        bool previous = App.IsShuttingDown;
        var jobs = new Mock<IJobService>(MockBehavior.Strict);
        // Stop before launching a real UI; the opener must reach this cleanup first.
        jobs.Setup(j => j.StopPlugin(process.Id)).Callback(() => App.IsShuttingDown = true);
        using var service = new UIRunnerService(jobs.Object, TimeSpan.FromSeconds(10));
        TrackProcess(service, process.Id);
        int activationRequests = 0;
        service.UISendCmd += (_, _) => Interlocked.Increment(ref activationRequests);
        try {
            service.NotifyUIClosing(process.Id);
            Task opener = service.ShowUIAsync();
            Assert.IsFalse(opener.IsCompleted, "Reopening must wait for the closing process");
            Assert.AreEqual(0, activationRequests);

            await process.StandardInput.WriteLineAsync("finish");
            await opener.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.IsTrue(process.HasExited);
            Assert.IsNull(GetTrackedProcess(service));
            jobs.Verify(j => j.StopPlugin(process.Id), Times.Once);
            jobs.VerifyNoOtherCalls();
        }
        finally { App.IsShuttingDown = previous; }
    }

    private static Process StartWaitingProcess() => Process.Start(new ProcessStartInfo("cmd.exe", "/d /c set /p VP_WAIT=") {
        UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true
    })!;

    private static void TrackProcess(UIRunnerService service, int pid) {
        var process = Process.GetProcessById(pid);
        _ = process.Handle;
        var callback = typeof(UIRunnerService).GetMethod("Proc_UI_Exited", BindingFlags.Instance | BindingFlags.NonPublic)!;
        process.Exited += (EventHandler)callback.CreateDelegate(typeof(EventHandler), service);
        typeof(UIRunnerService).GetField("_processUI", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, process);
        process.EnableRaisingEvents = true;
    }

    private static Process? GetTrackedProcess(UIRunnerService service) {
        var gate = typeof(UIRunnerService).GetField("_processLock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        lock (gate) {
            return (Process?)typeof(UIRunnerService).GetField("_processUI", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service);
        }
    }

    private static Task<bool>? GetExitTask(UIRunnerService service) =>
        (Task<bool>?)typeof(UIRunnerService).GetField("_uiExitTask", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service);

    [TestMethod]
    public async Task ShowUi_WhilePendingUpdateWaitsBeforeReopening() {
        var jobs = new Mock<IJobService>(MockBehavior.Strict);
        using var service = new UIRunnerService(jobs.Object);
        var update = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        typeof(UIRunnerService).GetField("_uiUpdateTask", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, update.Task);
        bool previous = App.IsShuttingDown;
        try {
            Task opener = service.ShowUIAsync();
            Assert.IsFalse(opener.IsCompleted, "Reopening must also wait for pending plugin updates");
            App.IsShuttingDown = true; // Stop before launching a real UI.
            update.SetResult();
            await opener.WaitAsync(TimeSpan.FromSeconds(5));
            jobs.VerifyNoOtherCalls();
        }
        finally {
            App.IsShuttingDown = previous;
            update.TrySetResult();
        }
    }

    [TestMethod]
    public void ExitHandledBeforeQueuedCallback_CleansOldProcessOnlyOnce() {
        using var environment = new WorkflowEnvironment();
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", "/d /c exit 0") {
            UseShellExecute = false, CreateNoWindow = true
        })!;
        Assert.IsTrue(process.WaitForExit(5000), "Exit fixture did not finish");
        int pid = process.Id;
        var jobs = new Mock<IJobService>(MockBehavior.Strict);
        jobs.Setup(j => j.StopPlugin(pid));
        using var service = new UIRunnerService(jobs.Object);
        var field = typeof(UIRunnerService).GetField("_processUI", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var handler = typeof(UIRunnerService).GetMethod("HandleUiExited", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(service, process);

        // ShowUI can handle the exit before the queued event gets the lifecycle lock.
        Assert.IsFalse((bool)handler.Invoke(service, [process])!);
        Assert.IsNull(field.GetValue(service));
        jobs.Verify(j => j.StopPlugin(pid), Times.Once);

        using var replacement = new Process();
        field.SetValue(service, replacement);
        handler.Invoke(service, [process]);
        Assert.AreSame(replacement, field.GetValue(service));
        jobs.Verify(j => j.StopPlugin(pid), Times.Once);
        jobs.VerifyNoOtherCalls();
    }

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
        await service.RestartUIAsync();
        await service.ShowUIAsync();
        Assert.AreEqual(nint.Zero, service.GetUIHwnd());
        jobs.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RestartUi_AllowsClosingNotificationAndWaitsForNormalExit() {
        using var environment = new WorkflowEnvironment();
        using var process = StartWaitingProcess();
        bool previous = App.IsShuttingDown;
        var jobs = new Mock<IJobService>(MockBehavior.Strict);
        jobs.Setup(j => j.StopPlugin(process.Id)).Callback(() => App.IsShuttingDown = true);
        UIRunnerService? service = null;
        bool notified = false;
        using var runner = service = new UIRunnerService(jobs.Object, TimeSpan.FromSeconds(5), async () => {
            // Closing UI must be able to notify Core while restart waits.
            await Task.Run(() => service!.NotifyUIClosing(process.Id)).WaitAsync(TimeSpan.FromSeconds(2));
            notified = true;
            await process.StandardInput.WriteLineAsync("finish");
            return true;
        });
        TrackProcess(runner, process.Id);
        try {
            await runner.RestartUIAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(notified);
            Assert.IsTrue(process.HasExited);
            Assert.AreEqual(0, process.ExitCode);
            Assert.IsNull(GetTrackedProcess(runner));
            jobs.Verify(j => j.StopPlugin(process.Id), Times.Once);
            jobs.VerifyNoOtherCalls();
        }
        finally { App.IsShuttingDown = previous; }
    }

    [TestMethod]
    public async Task RestartUi_WhenCloseIsRefusedRetainsCurrentProcessWithoutTimeout() {
        using var environment = new WorkflowEnvironment();
        using var process = StartWaitingProcess();
        var jobs = new Mock<IJobService>(MockBehavior.Strict);
        using var service = new UIRunnerService(jobs.Object, TimeSpan.FromMilliseconds(100), () => Task.FromResult(false));
        TrackProcess(service, process.Id);
        await service.RestartUIAsync();
        await Task.Delay(200);
        Assert.IsFalse(process.HasExited);
        Assert.AreEqual(process.Id, GetTrackedProcess(service)!.Id);
        Assert.IsNull(GetExitTask(service));
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
            await service.RestartUIAsync();
            Assert.AreEqual(nint.Zero, service.GetUIHwnd());
            jobs.VerifyNoOtherCalls();
        }
        finally { App.IsShuttingDown = previous; }
    }
}
