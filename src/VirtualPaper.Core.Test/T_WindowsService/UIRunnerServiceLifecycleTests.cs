using System.Diagnostics;
using System.Reflection;
using Moq;
using VirtualPaper.Services;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Core.Test.T_WindowsService;

[TestClass, DoNotParallelize]
public class UIRunnerServiceLifecycleTests {
    [TestMethod]
    public void QueuedOldExitCallback_DoesNotClearOrStopReplacement() {
        var jobs = new Mock<IJobService>(MockBehavior.Strict);
        using var service = new UIRunnerService(jobs.Object);
        using var oldProcess = new Process();
        using var replacement = new Process();
        var field = typeof(UIRunnerService).GetField("_processUI", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(service, replacement);
        typeof(UIRunnerService).GetMethod("Proc_UI_Exited", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(service, [oldProcess, EventArgs.Empty]);
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
