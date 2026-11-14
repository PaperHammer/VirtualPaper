using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GrpcDotNetNamedPipes;
using Moq;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Grpc.Service.Commands;
using VirtualPaper.GrpcServers;
using VirtualPaper.Grpc.Client;
using VirtualPaper.Services;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Core.Test.T_Common;

[TestClass]
public class ProjectFileActivationTests {
    [TestMethod]
    public void Arguments_PreserveUnicodeSpacesAndNormalizeDuplicates() {
        string first = Path.Combine(Path.GetTempPath(), "项目 files", "image.VPD");
        string second = Path.Combine(Path.GetTempPath(), "项目 files", "site.vpw");
        CollectionAssert.AreEqual(new[] { first, second }, ProjectFileActivation.ReadArguments(
            [ProjectFileActivation.OpenArgument, first, second, first.ToUpperInvariant()]));
        CollectionAssert.AreEqual(new[] { first }, ProjectFileActivation.ReadArguments([first]));
    }

    [TestMethod]
    public void Arguments_LeaveNormalAndPackageTestLaunchesAlone() {
        Assert.IsEmpty(ProjectFileActivation.ReadArguments([]));
        Assert.IsEmpty(ProjectFileActivation.ReadArguments([
            "--virtualpaper-test-session", Guid.NewGuid().ToString("N"), "--virtualpaper-test-data-root", Path.GetTempPath()]));
    }

    [TestMethod]
    public void Arguments_RejectMissingUnsupportedAndRelativeFiles() {
        Assert.ThrowsExactly<ArgumentException>(() => ProjectFileActivation.ReadArguments([ProjectFileActivation.OpenArgument]));
        Assert.ThrowsExactly<ArgumentException>(() => ProjectFileActivation.NormalizePaths(["relative.vpd"]));
        Assert.ThrowsExactly<ArgumentException>(() => ProjectFileActivation.NormalizePaths([Path.Combine(Path.GetTempPath(), "script.exe")]));
    }

    [TestMethod]
    public void Queue_KeepsRequestUntilMatchingAcknowledgementAndPreservesOrder() {
        var queue = new ProjectOpenQueue();
        string image = Path.Combine(Path.GetTempPath(), "first.vpd");
        string web = Path.Combine(Path.GetTempPath(), "second.vpw");
        queue.Enqueue([image]);
        queue.Enqueue([web]);
        var first = queue.Peek()!;
        Assert.AreEqual(image, first.Paths.Single());
        Assert.AreSame(first, queue.Peek(), "Reading must not consume a request before UI handles it.");
        queue.Complete("outdated acknowledgement");
        Assert.AreSame(first, queue.Peek());
        queue.Complete(first.Id);
        var second = queue.Peek()!;
        Assert.AreEqual(web, second.Paths.Single());
        queue.Complete(first.Id);
        Assert.AreSame(second, queue.Peek());
        queue.Complete(second.Id);
        Assert.IsNull(queue.Peek());
    }

    [TestMethod]
    public void Queue_ValidatesEntireBatchBeforeEnqueuing() {
        var queue = new ProjectOpenQueue();
        Assert.ThrowsExactly<ArgumentException>(() => queue.Enqueue([Path.Combine(Path.GetTempPath(), "valid.vpd"), "invalid.vpw"]));
        Assert.IsNull(queue.Peek());
        queue.Enqueue([]);
        Assert.IsNull(queue.Peek());
    }

    [TestMethod]
    public void Queue_RetriesAnInterruptedUiOnceAndAcceptsLaterRequests() {
        var queue = new ProjectOpenQueue();
        Assert.IsFalse(queue.TryReopenUi());
        queue.Enqueue([Path.Combine(Path.GetTempPath(), "project.vpd")]);
        Assert.IsTrue(queue.TryReopenUi());
        Assert.IsFalse(queue.TryReopenUi(), "A crashing UI must not create an infinite restart loop.");
        queue.Enqueue([Path.Combine(Path.GetTempPath(), "another.vpw")]);
        Assert.IsTrue(queue.TryReopenUi());
        queue.Complete(queue.Peek()!.Id);
        queue.Complete(queue.Peek()!.Id);
        Assert.IsFalse(queue.TryReopenUi());
    }

    [TestMethod]
    public void Queue_NormalCloseHandsOffPendingWorkWithoutConsumingCrashRecovery() {
        var queue = new ProjectOpenQueue();
        Assert.IsFalse(queue.TryReopenUi(expectedExit: true), "An ordinary close with no pending opens must stay closed.");
        queue.Enqueue([Path.Combine(Path.GetTempPath(), "project.vpd")]);
        var pending = queue.Peek();
        Assert.IsTrue(queue.TryReopenUi(expectedExit: true), "An open received during close confirmation needs a replacement UI.");
        Assert.AreSame(pending, queue.Peek());
        Assert.IsTrue(queue.TryReopenUi(), "Normal handoff must not consume crash recovery.");
        Assert.IsFalse(queue.TryReopenUi());
        queue.Complete(queue.Peek()!.Id);
        Assert.IsFalse(queue.TryReopenUi());
        Assert.IsFalse(queue.TryReopenUi(expectedExit: true));
    }

    [TestMethod]
    public async Task Queue_ConcurrentRequestsAreNotLostAndHaveDistinctAcknowledgements() {
        var queue = new ProjectOpenQueue();
        string[] paths = Enumerable.Range(0, 32).Select(index => Path.Combine(Path.GetTempPath(), $"project-{index}.vpd")).ToArray();
        await Task.WhenAll(paths.Select(path => Task.Run(() => queue.Enqueue([path]))));
        var ids = new HashSet<string>();
        var received = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (queue.Peek() is { } request) {
            Assert.IsTrue(ids.Add(request.Id), "Every request needs its own acknowledgement identity.");
            Assert.IsTrue(received.Add(request.Paths.Single()), "A concurrent enqueue duplicated another request.");
            queue.Complete(request.Id);
        }
        CollectionAssert.AreEquivalent(paths, received.ToArray());
        Assert.IsFalse(queue.TryReopenUi());
    }

    [TestMethod]
    public void Queue_InvalidOrEmptyBatchPreservesPendingRequestAndRecoveryLimit() {
        var queue = new ProjectOpenQueue();
        queue.Enqueue([Path.Combine(Path.GetTempPath(), "original.vpw")]);
        var request = queue.Peek();
        Assert.IsTrue(queue.TryReopenUi());

        Assert.ThrowsExactly<ArgumentException>(() => queue.Enqueue([Path.Combine(Path.GetTempPath(), "valid.vpd"), "invalid.vpw"]));
        queue.Enqueue([]);

        Assert.AreSame(request, queue.Peek());
        Assert.IsFalse(queue.TryReopenUi(), "A rejected/empty request must not reset crash recovery.");
        queue.Complete(request!.Id);
        Assert.IsNull(queue.Peek());
    }

    [TestMethod]
    public async Task NamedPipe_ProjectPathsSurviveForwardingUntilUiAcknowledges() {
        var queue = new ProjectOpenQueue();
        var runner = new Mock<IUIRunnerService>();
        runner.Setup(x => x.OpenProjectsAsync(It.IsAny<IEnumerable<string>>()))
            .Returns((IEnumerable<string> paths) => { queue.Enqueue(paths); return Task.CompletedTask; });
        runner.Setup(x => x.GetPendingProjectOpen()).Returns(() => queue.Peek());
        runner.Setup(x => x.CompleteProjectOpen(It.IsAny<string>())).Callback<string>(queue.Complete);
        string pipeName = $"VirtualPaper.ProjectOpen.{Guid.NewGuid():N}";
        using var server = new NamedPipeServer(pipeName);
        Grpc_CommandsService.BindService(server.ServiceBinder, new CommandsServer(runner.Object));
        server.Start();
        // Use a private pipe; this test never connects to an installed/running daily application.
        var client = new Grpc_CommandsService.Grpc_CommandsServiceClient(new NamedPipeChannel(".", pipeName));
        try {
            string path = Path.Combine(Path.GetTempPath(), "中文 project", "example.VPW");
            var request = new Grpc_ProjectPaths();
            request.Paths.AddRange(new[] { path, path.ToLowerInvariant() });
            var host = await client.GetActivationHostAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(5));
            Assert.AreEqual(Environment.ProcessId, host.ProcessId);
            await client.OpenProjectsAsync(request, deadline: DateTime.UtcNow.AddSeconds(5));
            var pending = await client.GetPendingProjectOpenAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(5));
            Assert.IsFalse(string.IsNullOrEmpty(pending.Id));
            CollectionAssert.AreEqual(new[] { path }, pending.Paths.ToArray());
            var retry = await client.GetPendingProjectOpenAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(5));
            Assert.AreEqual(pending.Id, retry.Id);
            using var uiClient = new CommandsClient(new NamedPipeChannel(".", pipeName));
            var uiRequest = await uiClient.GetPendingProjectOpenAsync();
            Assert.AreEqual(pending.Id, uiRequest!.Id);
            CollectionAssert.AreEqual(new[] { path }, uiRequest.Paths);
            await uiClient.CompleteProjectOpenAsync(pending.Id);
            var completed = await client.GetPendingProjectOpenAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(5));
            Assert.AreEqual(string.Empty, completed.Id);
            Assert.IsEmpty(completed.Paths);
            var invalid = new Grpc_ProjectPaths();
            invalid.Paths.Add("relative.vpw");
            var error = await Assert.ThrowsExactlyAsync<RpcException>(async () =>
                await client.OpenProjectsAsync(invalid, deadline: DateTime.UtcNow.AddSeconds(5)));
            Assert.AreEqual(StatusCode.InvalidArgument, error.StatusCode);
            Assert.IsNull(queue.Peek());
        }
        finally { server.Kill(); }
    }

    [TestMethod]
    public async Task NewUi_ConnectedToOlderCore_DoesNotFailNormalStartup() {
        string pipeName = $"VirtualPaper.LegacyProjectOpen.{Guid.NewGuid():N}";
        using var server = new NamedPipeServer(pipeName);
        Grpc_CommandsService.BindService(server.ServiceBinder, new LegacyCommandsServer());
        server.Start();
        try {
            using var client = new CommandsClient(new NamedPipeChannel(".", pipeName));
            Assert.IsNull(await client.GetPendingProjectOpenAsync());
        }
        finally { server.Kill(); }
    }

    [TestMethod]
    [DataRow(StatusCode.Unavailable)]
    [DataRow(StatusCode.PermissionDenied)]
    public async Task NewUi_DoesNotTreatConnectionOrPermissionFailuresAsAnOlderCore(StatusCode status) {
        string pipeName = $"VirtualPaper.ProjectOpenFailure.{Guid.NewGuid():N}";
        using var server = new NamedPipeServer(pipeName);
        Grpc_CommandsService.BindService(server.ServiceBinder, new LegacyCommandsServer(status));
        server.Start();
        try {
            using var client = new CommandsClient(new NamedPipeChannel(".", pipeName));
            var failure = await Assert.ThrowsExactlyAsync<RpcException>(() => client.GetPendingProjectOpenAsync());
            Assert.AreEqual(status, failure.StatusCode);
        }
        finally { server.Kill(); }
    }

    private sealed class LegacyCommandsServer(StatusCode status = StatusCode.Unimplemented) : CommandsServer(Mock.Of<IUIRunnerService>()) {
        public override Task<Grpc_ProjectOpenRequest> GetPendingProjectOpen(Empty request, ServerCallContext context) =>
            throw new RpcException(new Status(status, "Project request unavailable"));
    }
}
