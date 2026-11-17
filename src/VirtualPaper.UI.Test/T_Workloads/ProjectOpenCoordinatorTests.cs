using Moq;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Grpc.Client.Interfaces;
using VirtualPaper.UI.Services;

namespace VirtualPaper.UI.Test.T_Workloads;

[TestClass]
public class ProjectOpenCoordinatorTests {
    [TestMethod]
    public async Task StartupAndLaterIpcDrainTheSameCoreQueueInOrder() {
        var first = Request("启动 项目.vpd");
        var second = Request("second.vpw");
        var fixture = new Fixture(first);
        var opened = new List<string>();
        var coordinator = fixture.Create(paths => { opened.Add(paths.Single()); return Task.CompletedTask; });

        await coordinator.ProcessAsync(); // An early IPC notification must not access the workspace.
        fixture.Client.Verify(x => x.GetPendingProjectOpenAsync(), Times.Never);
        await coordinator.StartAsync();
        fixture.Pending.Enqueue(second);
        await coordinator.ProcessAsync();
        CollectionAssert.AreEqual(new[] { first.Paths[0], second.Paths[0] }, opened);
        CollectionAssert.AreEqual(new[] { first.Id, second.Id }, fixture.Completed);
        Assert.IsEmpty(fixture.Pending);
        Assert.IsEmpty(fixture.Errors);
        // Repeated Loaded/IPC events must not open completed requests again.
        await coordinator.StartAsync();
        await coordinator.ProcessAsync();
        Assert.HasCount(2, opened);
    }

    [TestMethod]
    public async Task IpcWhileOpeningDoesNotOpenProjectsConcurrently() {
        var first = Request("first.vpd");
        var second = Request("second.vpw");
        var fixture = new Fixture(first);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var opened = new List<string>();
        var coordinator = fixture.Create(async paths => {
            opened.Add(paths.Single());
            if (paths[0] == first.Paths[0]) { entered.SetResult(); await release.Task; }
        });
        Task startup = coordinator.StartAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Pending.Enqueue(second);
        Task notification = coordinator.ProcessAsync();
        Assert.HasCount(1, opened);
        Assert.IsEmpty(fixture.Completed, "Opening is not complete until the existing loader returns.");
        release.SetResult();
        await Task.WhenAll(startup, notification).WaitAsync(TimeSpan.FromSeconds(5));
        CollectionAssert.AreEqual(new[] { first.Paths[0], second.Paths[0] }, opened);
        Assert.IsEmpty(fixture.Errors);
    }

    [TestMethod]
    public async Task CloseConfirmationWaitsForOpeningAndCancelResumesPendingWork() {
        var fixture = new Fixture(Request("first.vpd"));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int count = 0;
        var coordinator = fixture.Create(async _ => {
            if (++count == 1) { entered.SetResult(); await release.Task; }
        });
        Task startup = coordinator.StartAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Pending.Enqueue(Request("second.vpw"));
        Task closeCheck = coordinator.PauseAsync();
        Assert.IsFalse(closeCheck.IsCompleted);
        release.SetResult();
        await Task.WhenAll(startup, closeCheck).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(1, count);
        await coordinator.ProcessAsync();
        Assert.AreEqual(1, count, "Projects must wait while the save/discard prompt is open.");
        await coordinator.ResumeAsync();
        Assert.AreEqual(2, count);
        Assert.IsEmpty(fixture.Pending);
    }

    [TestMethod]
    public async Task ConfirmedCloseLeavesUnopenedRequestForTheNextUi() {
        var request = Request("project.vpd");
        var fixture = new Fixture(request);
        int count = 0;
        var coordinator = fixture.Create(_ => { count++; return Task.CompletedTask; });
        await coordinator.PauseAsync();
        await coordinator.StartAsync();
        coordinator.Stop();
        await coordinator.ResumeAsync();
        Assert.AreEqual(0, count);
        Assert.IsEmpty(fixture.Completed);
        Assert.AreSame(request, fixture.Pending.Peek());
        var replacement = fixture.Create(_ => { count++; return Task.CompletedTask; });
        await replacement.StartAsync();
        Assert.AreEqual(1, count);
        CollectionAssert.AreEqual(new[] { request.Id }, fixture.Completed);
        Assert.IsEmpty(fixture.Pending);
    }

    [TestMethod]
    public async Task InterruptedOpenIsReplayedByANewUiAndCompletedOnlyOnce() {
        var request = Request("project.vpw");
        var fixture = new Fixture(request);
        var interruptedUi = fixture.Create(_ => throw new OperationCanceledException());
        await interruptedUi.StartAsync();
        Assert.IsEmpty(fixture.Completed);
        Assert.AreSame(request, fixture.Pending.Peek());
        interruptedUi.Stop();

        int opened = 0;
        var replacement = fixture.Create(_ => { opened++; return Task.CompletedTask; });
        await replacement.StartAsync();
        await replacement.ProcessAsync();
        Assert.AreEqual(1, opened);
        CollectionAssert.AreEqual(new[] { request.Id }, fixture.Completed);
        Assert.IsEmpty(fixture.Pending);
        Assert.IsEmpty(fixture.Errors);
    }

    [TestMethod]
    public async Task CorruptProjectIsReportedAndDoesNotPreventTheNextProjectOpening() {
        var bad = Request("corrupt.vpd");
        var good = Request("valid.vpw");
        var fixture = new Fixture(bad, good);
        var opened = new List<string>();
        var coordinator = fixture.Create(paths => {
            if (paths[0] == bad.Paths[0]) throw new InvalidDataException("Corrupt project");
            opened.Add(paths[0]);
            return Task.CompletedTask;
        });
        await coordinator.StartAsync();
        Assert.IsInstanceOfType<InvalidDataException>(fixture.Errors.Single());
        CollectionAssert.AreEqual(new[] { good.Paths[0] }, opened);
        CollectionAssert.AreEqual(new[] { bad.Id, good.Id }, fixture.Completed);
        Assert.IsEmpty(fixture.Pending);
    }

    [TestMethod]
    public async Task CloseDuringPendingRequestReadDoesNotStartOrAcknowledgeIt() {
        var request = Request("project.vpd");
        var fixture = new Fixture(request);
        var read = new TaskCompletionSource<ProjectOpenRequest?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Client.Setup(x => x.GetPendingProjectOpenAsync()).Returns(read.Task);
        int count = 0;
        var coordinator = fixture.Create(_ => { count++; return Task.CompletedTask; });
        Task startup = coordinator.StartAsync();
        Task closeCheck = coordinator.PauseAsync();
        read.SetResult(request);
        await Task.WhenAll(startup, closeCheck).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(0, count);
        Assert.IsEmpty(fixture.Completed);
    }

    [TestMethod]
    public async Task FailedRequestReadIsReportedAndLaterNotificationCanRetry() {
        var request = Request("retry.vpd");
        var fixture = new Fixture(request);
        var failure = new IOException("Core connection interrupted");
        fixture.Client.SetupSequence(x => x.GetPendingProjectOpenAsync())
            .ThrowsAsync(failure).ReturnsAsync(request).ReturnsAsync((ProjectOpenRequest?)null);
        int opened = 0;
        var coordinator = fixture.Create(_ => { opened++; return Task.CompletedTask; });

        await coordinator.StartAsync();
        Assert.AreEqual(0, opened);
        Assert.IsEmpty(fixture.Completed);
        Assert.AreSame(request, fixture.Pending.Peek());
        Assert.AreSame(failure, fixture.Errors.Single());

        await coordinator.ProcessAsync();
        Assert.AreEqual(1, opened);
        CollectionAssert.AreEqual(new[] { request.Id }, fixture.Completed);
        Assert.IsEmpty(fixture.Pending);
    }

    [TestMethod]
    public async Task FailedAcknowledgementKeepsRequestAvailableAndReleasesTheGate() {
        var request = Request("retry.vpw");
        var fixture = new Fixture(request);
        var failure = new IOException("Acknowledgement interrupted");
        int acknowledgements = 0;
        fixture.Client.Setup(x => x.CompleteProjectOpenAsync(request.Id)).Returns(() => {
            if (++acknowledgements == 1) return Task.FromException(failure);
            fixture.Pending.Dequeue();
            fixture.Completed.Add(request.Id);
            return Task.CompletedTask;
        });
        int opened = 0;
        var coordinator = fixture.Create(_ => { opened++; return Task.CompletedTask; });

        await coordinator.StartAsync();
        Assert.AreEqual(1, opened);
        Assert.IsEmpty(fixture.Completed);
        Assert.AreSame(request, fixture.Pending.Peek());
        Assert.AreSame(failure, fixture.Errors.Single());

        await coordinator.ProcessAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(2, opened, "An unacknowledged open is replayed; the workspace reuses its existing tab.");
        CollectionAssert.AreEqual(new[] { request.Id }, fixture.Completed);
        Assert.IsEmpty(fixture.Pending);
    }

    [TestMethod]
    public async Task StopDuringOpenAcknowledgesOnlyFinishedWorkAndKeepsTheNextRequest() {
        var first = Request("first.vpd");
        var second = Request("second.vpw");
        var fixture = new Fixture(first, second);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var opened = new List<string>();
        var coordinator = fixture.Create(async paths => {
            opened.Add(paths.Single());
            entered.SetResult();
            await release.Task;
        });
        Task startup = coordinator.StartAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.Stop();
        release.SetResult();
        await startup.WaitAsync(TimeSpan.FromSeconds(5));
        await coordinator.ProcessAsync();

        CollectionAssert.AreEqual(new[] { first.Paths[0] }, opened);
        CollectionAssert.AreEqual(new[] { first.Id }, fixture.Completed);
        Assert.AreSame(second, fixture.Pending.Peek());
        Assert.IsEmpty(fixture.Errors);
    }

    private static ProjectOpenRequest Request(string name) =>
        new(Guid.NewGuid().ToString("N"), [Path.Combine(Path.GetTempPath(), name)]);

    private sealed class Fixture {
        public Fixture(params ProjectOpenRequest[] requests) {
            Pending = new(requests);
            Client.Setup(x => x.GetPendingProjectOpenAsync()).ReturnsAsync(() => Pending.TryPeek(out var request) ? request : null);
            Client.Setup(x => x.CompleteProjectOpenAsync(It.IsAny<string>())).Returns((string id) => {
                Assert.AreEqual(Pending.Peek().Id, id);
                Pending.Dequeue();
                Completed.Add(id);
                return Task.CompletedTask;
            });
        }

        public ProjectOpenCoordinator Create(Func<IReadOnlyList<string>, Task> open) => new(Client.Object, open, Errors.Add);
        public Mock<ICommandsClient> Client { get; } = new(MockBehavior.Strict);
        public Queue<ProjectOpenRequest> Pending { get; }
        public List<string> Completed { get; } = [];
        public List<Exception> Errors { get; } = [];
    }
}
