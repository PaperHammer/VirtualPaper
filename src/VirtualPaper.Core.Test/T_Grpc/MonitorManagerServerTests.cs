using System.Collections.ObjectModel;
using System.Drawing;
using Google.Protobuf.WellKnownTypes;
using Moq;
using VirtualPaper.Cores.Monitor;
using VirtualPaper.Grpc.Service.CommonModels;
using VirtualPaper.GrpcServers;
using Monitor = VirtualPaper.Models.Cores.Monitor;

namespace VirtualPaper.Core.Test.T_Grpc;

[TestClass]
public class MonitorManagerServerTests {
    [TestMethod]
    public async Task GetMonitors_MapsIdentityGeometryAndThumbnail() {
        var monitor = new Monitor {
            DeviceId = "DISPLAY-2",
            Content = "Secondary",
            SystemIndex = 4,
            IsPrimary = false,
            Bounds = new Rectangle(-1920, 0, 1920, 1080),
            WorkingArea = new Rectangle(-1920, 0, 1920, 1040),
            ThumbnailPath = "thumbnail.png",
        };
        Mock<IMonitorManager> manager = CreateManager(monitor);
        var server = new MonitorManagerServer(manager.Object);

        var response = await server.GetMonitors(new Empty(), null!);

        Grpc_MonitorData result = AssertSingle(response.Monitors);
        Assert.AreEqual("DISPLAY-2", result.DeviceId);
        Assert.AreEqual("Secondary", result.Content);
        Assert.AreEqual(4, result.SystemIndex);
        Assert.AreEqual(-1920, result.Bounds.X);
        Assert.AreEqual(1080, result.Bounds.Height);
        Assert.AreEqual(1040, result.WorkingArea.Height);
        Assert.AreEqual("thumbnail.png", result.ThumbnailPath);
    }

    [TestMethod]
    public async Task GetVirtualScreenBounds_MapsNegativeCoordinates() {
        Mock<IMonitorManager> manager = CreateManager();
        manager.SetupGet(value => value.VirtualScreenBounds)
            .Returns(new Rectangle(-1920, -100, 3840, 1180));
        var server = new MonitorManagerServer(manager.Object);

        Grpc_Rectangle response = await server.GetVirtualScreenBounds(new Empty(), null!);

        Assert.AreEqual(-1920, response.X);
        Assert.AreEqual(-100, response.Y);
        Assert.AreEqual(3840, response.Width);
        Assert.AreEqual(1180, response.Height);
    }

    [TestMethod]
    public async Task MonitorChangedSubscription_WritesNotificationAndUnsubscribesOnCancellation() {
        Mock<IMonitorManager> manager = CreateManager();
        var server = new MonitorManagerServer(manager.Object);
        var writer = new RecordingStreamWriter<Empty>();
        using var cancellation = new CancellationTokenSource();
        Task subscription = server.SubscribeMonitorChanged(
            new Empty(),
            writer,
            new TestServerCallContext(cancellation.Token));
        await WaitUntilAsync(() =>
            manager.VerifyAdd(value => value.MonitorUpdated += It.IsAny<EventHandler>(), Times.Once));

        manager.Raise(value => value.MonitorUpdated += null, EventArgs.Empty);
        await WaitUntilAsync(() => writer.Items.Count == 1);
        cancellation.Cancel();
        await subscription;

        Assert.HasCount(1, writer.Items);
        manager.VerifyRemove(
            value => value.MonitorUpdated -= It.IsAny<EventHandler>(),
            Times.AtLeastOnce());
    }

    [TestMethod]
    public async Task MonitorPropertySubscription_WritesNotificationAndUnsubscribesOnCancellation() {
        Mock<IMonitorManager> manager = CreateManager();
        var server = new MonitorManagerServer(manager.Object);
        var writer = new RecordingStreamWriter<Empty>();
        using var cancellation = new CancellationTokenSource();
        Task subscription = server.SubscribeMonitorPropertyChanged(
            new Empty(),
            writer,
            new TestServerCallContext(cancellation.Token));
        await WaitUntilAsync(() =>
            manager.VerifyAdd(value => value.MonitorPropertyUpdated += It.IsAny<EventHandler>(), Times.Once));

        manager.Raise(value => value.MonitorPropertyUpdated += null, EventArgs.Empty);
        await WaitUntilAsync(() => writer.Items.Count == 1);
        cancellation.Cancel();
        await subscription;

        Assert.HasCount(1, writer.Items);
        manager.VerifyRemove(
            value => value.MonitorPropertyUpdated -= It.IsAny<EventHandler>(),
            Times.AtLeastOnce());
    }

    [TestMethod]
    public async Task Subscription_WhenWriterFails_CompletesWithoutLeakingHandler() {
        Mock<IMonitorManager> manager = CreateManager();
        var server = new MonitorManagerServer(manager.Object);
        var writer = new FailingStreamWriter<Empty>(successfulWritesBeforeFailure: 0);
        using var cancellation = new CancellationTokenSource();
        Task subscription = server.SubscribeMonitorChanged(
            new Empty(),
            writer,
            new TestServerCallContext(cancellation.Token));
        await WaitUntilAsync(() =>
            manager.VerifyAdd(value => value.MonitorUpdated += It.IsAny<EventHandler>(), Times.Once));

        manager.Raise(value => value.MonitorUpdated += null, EventArgs.Empty);
        await subscription;

        Assert.AreEqual(1, writer.WriteAttempts);
        manager.VerifyRemove(
            value => value.MonitorUpdated -= It.IsAny<EventHandler>(),
            Times.AtLeastOnce());
    }

    private static Mock<IMonitorManager> CreateManager(params Monitor[] monitors) {
        var manager = new Mock<IMonitorManager>();
        manager.SetupGet(value => value.Monitors)
            .Returns(new ObservableCollection<Monitor>(monitors));
        return manager;
    }

    private static T AssertSingle<T>(IEnumerable<T> values) {
        T[] items = values.ToArray();
        Assert.HasCount(1, items);
        return items[0];
    }

    private static async Task WaitUntilAsync(Action assertion) {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 100; attempt++) {
            try {
                assertion();
                return;
            }
            catch (Exception ex) when (ex is MockException or AssertFailedException) {
                lastError = ex;
                await Task.Delay(10);
            }
        }
        throw lastError ?? new AssertFailedException("Condition was not reached.");
    }

    private static async Task WaitUntilAsync(Func<bool> condition) {
        for (var attempt = 0; attempt < 100; attempt++) {
            if (condition()) return;
            await Task.Delay(10);
        }
        Assert.Fail("Condition was not reached.");
    }
}
