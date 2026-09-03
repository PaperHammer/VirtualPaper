using Google.Protobuf.WellKnownTypes;
using GrpcDotNetNamedPipes;
using Moq;
using VirtualPaper.Common.Utils.IPC;
using VirtualPaper.Cores.AppUpdate;
using VirtualPaper.Cores.ScreenSaver;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.Grpc.Service.Commands;
using VirtualPaper.Grpc.Service.TwoWay;
using VirtualPaper.GrpcServers;
using VirtualPaper.Models.Events;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Core.Test.T_Grpc;

[TestClass]
[DoNotParallelize]
public class GrpcServerContractTests {
    [TestMethod]
    [DoNotParallelize]
    public async Task CommandsService_NamedPipeRoundTripInvokesServer() {
        var runner = new Mock<IUIRunnerService>();
        runner.Setup(x => x.ShowUIAsync()).Returns(Task.CompletedTask);
        var implementation = new CommandsServer(runner.Object);
        var pipeName = $"VirtualPaper.Test.{Guid.NewGuid():N}";
        using var pipeServer = new NamedPipeServer(pipeName);
        Grpc_CommandsService.BindService(pipeServer.ServiceBinder, implementation);
        pipeServer.Start();

        try {
            var client = new Grpc_CommandsService.Grpc_CommandsServiceClient(
                new NamedPipeChannel(".", pipeName));

            await client.ShowUIAsync(new Empty());

            runner.Verify(x => x.ShowUIAsync(), Times.Once);
        }
        finally {
            pipeServer.Kill();
        }
    }

    [TestMethod]
    public async Task CommandsServer_ForwardsUiLifecycleCommands() {
        var runner = new Mock<IUIRunnerService>();
        runner.Setup(x => x.ShowUIAsync()).Returns(Task.CompletedTask);
        var server = new CommandsServer(runner.Object);

        await server.ShowUI(new Empty(), null!);
        await server.CloseUI(new Empty(), null!);
        await server.RestartUI(new Empty(), null!);

        runner.Verify(x => x.ShowUIAsync(), Times.Once);
        runner.Verify(x => x.CloseUI(), Times.Once);
        runner.Verify(x => x.RestartUI(), Times.Once);
    }

    [TestMethod]
    public async Task ScrCommandsServer_ForwardsAllRequests() {
        var control = new Mock<IScrControl>();
        control.Setup(x => x.StartAsync()).Returns(Task.CompletedTask);
        var server = new ScrCommandsServer(control.Object);

        await server.ChangeLockStatu(new Grpc_LockData { IsLock = true }, null!);
        await server.Start(new Empty(), null!);
        await server.Stop(new Empty(), null!);
        await server.AddToWhiteList(new Grpc_ProcInfoData { ProcName = "player.exe" }, null!);
        await server.RemoveFromWhiteList(new Grpc_ProcInfoData { ProcName = "player.exe" }, null!);

        control.Verify(x => x.ChangeLockStatu(true), Times.Once);
        control.Verify(x => x.StartAsync(), Times.Once);
        control.Verify(x => x.Stop(), Times.Once);
        control.Verify(x => x.AddToWhiteList("player.exe"), Times.Once);
        control.Verify(x => x.RemoveFromWhiteList("player.exe"), Times.Once);
    }

    [TestMethod]
    public async Task AppUpdateServer_CheckUpdateUsesImmediateFetch() {
        var updater = new Mock<IAppUpdaterService>();
        updater.Setup(x => x.CheckUpdateAsync(0)).ReturnsAsync(AppUpdateStatus.Uptodate);
        var server = new AppUpdateServer(updater.Object);

        await server.CheckUpdate(new Empty(), null!);

        updater.Verify(x => x.CheckUpdateAsync(0), Times.Once);
    }

    [TestMethod]
    public async Task CommandsSubscription_CancellationRemovesEventHandler() {
        var runner = new Mock<IUIRunnerService>();
        var server = new CommandsServer(runner.Object);
        using var cancellation = new CancellationTokenSource();
        var context = new TestServerCallContext(cancellation.Token);
        var writer = new RecordingStreamWriter<Grpc_UIRecievedCmd>();

        var subscription = server.SubscribeUIRecievedCmd(new Empty(), writer, context);
        await WaitUntilAsync(() => runner.VerifyAdd(x => x.UISendCmd += It.IsAny<EventHandler<MessageType>>(), Times.Once));
        cancellation.Cancel();
        await subscription;

        runner.VerifyRemove(x => x.UISendCmd -= It.IsAny<EventHandler<MessageType>>(), Times.Once);
        Assert.IsEmpty(writer.Items);
    }

    [TestMethod]
    public async Task TwoWayStream_BroadcastReachesAllConnectedClients() {
        var firstReader = new ChannelStreamReader<TwoWayMessage>();
        var secondReader = new ChannelStreamReader<TwoWayMessage>();
        var firstWriter = new RecordingStreamWriter<TwoWayMessage>();
        var secondWriter = new RecordingStreamWriter<TwoWayMessage>();
        var server = new TwoWayServer();
        var first = server.TwoWayStream(firstReader, firstWriter, new TestServerCallContext(CancellationToken.None));
        var second = server.TwoWayStream(secondReader, secondWriter, new TestServerCallContext(CancellationToken.None));

        try {
            await WaitUntilConnectedAsync(firstWriter, secondWriter);
            firstWriter.Clear();
            secondWriter.Clear();
            var message = new TwoWayMessage { Type = "STATE_CHANGED", Payload = "ready" };

            await TwoWayServer.BroadcastAsync(message);

            CollectionAssert.AreEqual(new[] { message }, firstWriter.Items.ToArray());
            CollectionAssert.AreEqual(new[] { message }, secondWriter.Items.ToArray());
        }
        finally {
            firstReader.Complete();
            secondReader.Complete();
            await Task.WhenAll(first, second);
        }
    }

    [TestMethod]
    public async Task RequestUIClose_MatchingClientResponseCompletesRequest() {
        var reader = new ChannelStreamReader<TwoWayMessage>();
        var writer = new RecordingStreamWriter<TwoWayMessage>();
        var server = new TwoWayServer();
        var stream = server.TwoWayStream(reader, writer, new TestServerCallContext(CancellationToken.None));

        try {
            await WaitUntilConnectedAsync(writer);
            writer.Clear();

            var request = TwoWayServer.RequestUICloseAsync(TimeSpan.FromSeconds(2));
            await WaitUntilAsync(() => Assert.IsTrue(writer.Items.Any(x => x.Type == "REQUEST_CLOSE")));
            var outbound = writer.Items.Single(x => x.Type == "REQUEST_CLOSE");
            await reader.WriteAsync(new TwoWayMessage {
                Type = "UI_CLOSE_RESULT",
                RequestId = outbound.RequestId,
                Payload = "true",
            });

            Assert.IsTrue(await request);
        }
        finally {
            reader.Complete();
            await stream;
        }
    }

    [TestMethod]
    public async Task TwoWayStream_AbnormalDisconnectRemovesClientFromBroadcasts() {
        var reader = new ChannelStreamReader<TwoWayMessage>();
        var writer = new RecordingStreamWriter<TwoWayMessage>();
        var server = new TwoWayServer();
        var stream = server.TwoWayStream(reader, writer, new TestServerCallContext(CancellationToken.None));

        await WaitUntilConnectedAsync(writer);
        writer.Clear();
        reader.Complete(new InvalidDataException("connection lost"));

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => stream);
        await TwoWayServer.BroadcastAsync(new TwoWayMessage { Type = "AFTER_DISCONNECT" });

        Assert.IsEmpty(writer.Items);
    }

    [TestMethod]
    public async Task Broadcast_FailedClientDoesNotBlockHealthyClientAndIsRemoved() {
        var failingReader = new ChannelStreamReader<TwoWayMessage>();
        var healthyReader = new ChannelStreamReader<TwoWayMessage>();
        var failingWriter = new FailingStreamWriter<TwoWayMessage>(successfulWritesBeforeFailure: 1);
        var healthyWriter = new RecordingStreamWriter<TwoWayMessage>();
        var server = new TwoWayServer();
        var failingStream = server.TwoWayStream(failingReader, failingWriter, new TestServerCallContext(CancellationToken.None));
        var healthyStream = server.TwoWayStream(healthyReader, healthyWriter, new TestServerCallContext(CancellationToken.None));

        try {
            await WaitUntilAsync(() => {
                TwoWayServer.BroadcastAsync(new TwoWayMessage { Type = "CONNECTIVITY_PROBE" }).GetAwaiter().GetResult();
                Assert.AreEqual(1, failingWriter.SuccessfulWrites);
                Assert.IsNotEmpty(healthyWriter.Items);
            });
            healthyWriter.Clear();

            var first = new TwoWayMessage { Type = "FIRST" };
            var second = new TwoWayMessage { Type = "SECOND" };
            await TwoWayServer.BroadcastAsync(first);
            await TwoWayServer.BroadcastAsync(second);

            CollectionAssert.AreEqual(new[] { first, second }, healthyWriter.Items.ToArray());
            Assert.AreEqual(2, failingWriter.WriteAttempts);
        }
        finally {
            failingReader.Complete();
            healthyReader.Complete();
            await Task.WhenAll(failingStream, healthyStream);
        }
    }

    [TestMethod]
    public async Task RequestUIClose_ConcurrentRequestsAreCorrelatedIndependently() {
        var reader = new ChannelStreamReader<TwoWayMessage>();
        var writer = new RecordingStreamWriter<TwoWayMessage>();
        var stream = new TwoWayServer().TwoWayStream(
            reader, writer, new TestServerCallContext(CancellationToken.None));

        try {
            await WaitUntilConnectedAsync(writer);
            writer.Clear();

            var firstRequest = TwoWayServer.RequestUICloseAsync(TimeSpan.FromSeconds(2));
            await WaitUntilAsync(() => Assert.HasCount(1, writer.Items));
            var firstId = writer.Items.Single().RequestId;
            var secondRequest = TwoWayServer.RequestUICloseAsync(TimeSpan.FromSeconds(2));
            await WaitUntilAsync(() => Assert.HasCount(2, writer.Items));
            var secondId = writer.Items.Last().RequestId;

            await reader.WriteAsync(new TwoWayMessage {
                Type = "UI_CLOSE_RESULT", RequestId = secondId, Payload = "false",
            });
            await reader.WriteAsync(new TwoWayMessage {
                Type = "UI_CLOSE_RESULT", RequestId = firstId, Payload = "true",
            });

            Assert.IsTrue(await firstRequest);
            Assert.IsFalse(await secondRequest);
        }
        finally {
            reader.Complete();
            await stream;
        }
    }

    private static async Task WaitUntilConnectedAsync(params RecordingStreamWriter<TwoWayMessage>[] writers) {
        for (var attempt = 0; attempt < 50; attempt++) {
            var probe = new TwoWayMessage { Type = "CONNECTIVITY_PROBE" };
            await TwoWayServer.BroadcastAsync(probe);
            if (writers.All(writer => writer.Items.Contains(probe))) {
                return;
            }
            await Task.Delay(10);
        }

        Assert.Fail("The two-way stream did not register all clients.");
    }

    private static async Task WaitUntilAsync(Action assertion) {
        for (var attempt = 0; attempt < 50; attempt++) {
            try {
                assertion();
                return;
            }
            catch (MockException) when (attempt < 49) {
                await Task.Delay(10);
            }
        }
    }
}
