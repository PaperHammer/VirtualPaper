using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Moq;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.GrpcServers;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Core.Test.T_Grpc;

[TestClass]
public class ProjectOpenCommandStreamTests {
    [TestMethod]
    public async Task SubscriptionNotifiesRequestsQueuedBeforeTheUiConnected() {
        var runner = new Mock<IUIRunnerService>();
        runner.Setup(x => x.GetPendingProjectOpen()).Returns(new ProjectOpenRequest(Guid.NewGuid().ToString("N"),
            [Path.Combine(Path.GetTempPath(), "queued.vpd")]));
        var server = new CommandsServer(runner.Object);
        var writer = new PausedWriter();
        using var cancellation = new CancellationTokenSource();
        var stream = server.SubscribeUIRecievedCmd(new Empty(), writer, new TestServerCallContext(cancellation.Token));
        try {
            // No event is raised for the old request: connection itself must discover it.
            await writer.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            runner.Raise(x => x.UISendCmd += null, runner.Object, MessageType.cmd_active);
            runner.Raise(x => x.UISendCmd += null, runner.Object, MessageType.cmd_close);
            writer.Resume.TrySetResult();
            await writer.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            CollectionAssert.AreEqual(new[] { (int)MessageType.cmd_open_projects, (int)MessageType.cmd_active,
                (int)MessageType.cmd_close }, writer.Messages.ToArray());
        }
        finally {
            writer.Resume.TrySetResult();
            cancellation.Cancel();
            await stream.WaitAsync(TimeSpan.FromSeconds(5));
        }
        runner.VerifyRemove(x => x.UISendCmd -= It.IsAny<EventHandler<MessageType>>(), Times.Once);
    }

    [TestMethod]
    public async Task CommandsArrivingDuringPipeWriteRemainOrderedAndAreNotDropped() {
        var runner = new Mock<IUIRunnerService>();
        var server = new CommandsServer(runner.Object);
        var writer = new PausedWriter();
        using var cancellation = new CancellationTokenSource();
        var stream = server.SubscribeUIRecievedCmd(new Empty(), writer, new TestServerCallContext(cancellation.Token));
        try {
            runner.Raise(x => x.UISendCmd += null, runner.Object, MessageType.cmd_active);
            await writer.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            runner.Raise(x => x.UISendCmd += null, runner.Object, MessageType.cmd_open_projects);
            runner.Raise(x => x.UISendCmd += null, runner.Object, MessageType.cmd_close);
            writer.Resume.TrySetResult();
            await writer.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            CollectionAssert.AreEqual(new[] { (int)MessageType.cmd_active, (int)MessageType.cmd_open_projects, (int)MessageType.cmd_close }, writer.Messages.ToArray());
        }
        finally {
            writer.Resume.TrySetResult();
            cancellation.Cancel();
            await stream.WaitAsync(TimeSpan.FromSeconds(5));
        }
        int count = writer.Messages.Count;
        runner.Raise(x => x.UISendCmd += null, runner.Object, MessageType.cmd_open_projects);
        Assert.AreEqual(count, writer.Messages.Count);
        runner.VerifyRemove(x => x.UISendCmd -= It.IsAny<EventHandler<MessageType>>(), Times.Once);
    }

    [TestMethod]
    public async Task CancellationWhileIdleRemovesTheCommandSubscription() {
        var runner = new Mock<IUIRunnerService>();
        var writer = new PausedWriter();
        using var cancellation = new CancellationTokenSource();
        Task stream = new CommandsServer(runner.Object).SubscribeUIRecievedCmd(new Empty(), writer,
            new TestServerCallContext(cancellation.Token));

        cancellation.Cancel();
        await stream.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsEmpty(writer.Messages);
        Assert.IsFalse(writer.Started.Task.IsCompleted);
        runner.VerifyRemove(x => x.UISendCmd -= It.IsAny<EventHandler<MessageType>>(), Times.Once);
    }

    [TestMethod]
    public async Task FailedPipeWriteRemovesTheCommandSubscription() {
        var runner = new Mock<IUIRunnerService>();
        var writer = new FailingWriter();
        using var cancellation = new CancellationTokenSource();
        Task stream = new CommandsServer(runner.Object).SubscribeUIRecievedCmd(new Empty(), writer,
            new TestServerCallContext(cancellation.Token));
        runner.Raise(x => x.UISendCmd += null, runner.Object, MessageType.cmd_open_projects);

        await stream.WaitAsync(TimeSpan.FromSeconds(5));
        runner.Raise(x => x.UISendCmd += null, runner.Object, MessageType.cmd_active);

        Assert.AreEqual(1, writer.Attempts);
        runner.VerifyRemove(x => x.UISendCmd -= It.IsAny<EventHandler<MessageType>>(), Times.Once);
    }

    private sealed class FailingWriter : IServerStreamWriter<Grpc_UIRecievedCmd> {
        public WriteOptions? WriteOptions { get; set; }
        public int Attempts { get; private set; }
        public Task WriteAsync(Grpc_UIRecievedCmd message) {
            Attempts++;
            return Task.FromException(new IOException("The pipe has disconnected"));
        }
    }

    private sealed class PausedWriter : IServerStreamWriter<Grpc_UIRecievedCmd> {
        public WriteOptions? WriteOptions { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<int> Messages { get; } = [];
        public async Task WriteAsync(Grpc_UIRecievedCmd message) {
            Started.TrySetResult();
            await Resume.Task;
            Messages.Add(message.IpcMsg);
            if (Messages.Count == 3) Completed.TrySetResult();
        }
    }
}
