using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using VirtualPaper.Core.Test.Infrastructure;
using VirtualPaper.Services.Download;
using VirtualPaper.Services.Interfaces;

namespace VirtualPaper.Core.Test.T_Download;

[TestClass]
public class DownloadTransferTests {
    private string _root = null!;
    private static readonly byte[] Payload = Enumerable.Range(0, 256 * 1024).Select(i => (byte)(i % 251)).ToArray();

    [TestInitialize]
    public void Initialize() => _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(),
        "VirtualPaper.DownloadTests-" + Guid.NewGuid().ToString("N"))).FullName;

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, recursive: true);

    [TestMethod]
    public async Task SingleDownload_ReportsProgressAndWritesExactBytes_ThenCanDownloadAgain() {
        await using var server = new DownloadTestServer(_ => new(Payload));
        using var service = new MultiDownloadService();
        for (int i = 0; i < 2; i++) {
            string path = Path.Combine(_root, $"file-{i}.bin");
            var progress = await CollectAsync(service.DownloadAsync(new Uri(server.Address, "payload"), path, default));
            CollectionAssert.AreEqual(Payload, File.ReadAllBytes(path));
            Assert.IsTrue(progress.Any(p => p.Percent >= 100 && p.ReceivedBytes == Payload.Length));
        }
        Assert.IsTrue(server.Requests.Count >= 2);
    }

    [TestMethod]
    public async Task MultipleDownloads_WriteEachPayloadAndCompleteAggregateProgress() {
        await using var server = new DownloadTestServer(path => new(path == "/a" ? Payload : Payload[..32768]));
        using var service = new MultiDownloadService();
        string a = Path.Combine(_root, "a.bin"), b = Path.Combine(_root, "b.bin");
        var progress = await CollectAsync(service.DownloadMultipleAsync(
            [(new Uri(server.Address, "a"), a), (new Uri(server.Address, "b"), b)], default));
        CollectionAssert.AreEqual(Payload, File.ReadAllBytes(a));
        CollectionAssert.AreEqual(Payload[..32768], File.ReadAllBytes(b));
        Assert.IsTrue(progress.Any(p => p.Percent >= 100 && p.ReceivedBytes == Payload.Length + 32768));
    }

    [TestMethod]
    public async Task EmptyBatch_DoesNotContactTheServer() {
        using var service = new MultiDownloadService();
        Assert.IsEmpty(await CollectAsync(service.DownloadMultipleAsync([], default)));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task HttpFailure_IsPropagated_AndServiceCanBeReused(bool multiple) {
        await using var server = new DownloadTestServer(path => path == "/missing" ? new([], 404) : new(Payload));
        using var service = new MultiDownloadService();
        string path = Path.Combine(_root, "missing.bin");
        var failed = multiple
            ? service.DownloadMultipleAsync([(new Uri(server.Address, "payload"), Path.Combine(_root, "ok.bin")),
                (new Uri(server.Address, "missing"), path)], default)
            : service.DownloadAsync(new Uri(server.Address, "missing"), path, default);
        var error = await Assert.ThrowsAsync<Exception>(() => CollectAsync(failed));
        Assert.IsNotInstanceOfType<TimeoutException>(error, "Failure must propagate rather than leave the progress channel waiting.");
        Assert.IsNotInstanceOfType<OperationCanceledException>(error, "HTTP failure is not user cancellation.");
        string retry = Path.Combine(_root, "retry.bin");
        await CollectAsync(service.DownloadAsync(new Uri(server.Address, "payload"), retry, default));
        CollectionAssert.AreEqual(Payload, File.ReadAllBytes(retry));
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task CancelActiveTransfer_TerminatesAndAllowsAnotherDownload(bool multiple, bool useServiceCancel) {
        await using var server = new DownloadTestServer(path => new(Payload, DelayPerChunk: path == "/slow" ? 8 : 0));
        using var service = new MultiDownloadService();
        using var cancel = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string path = Path.Combine(_root, "cancelled.bin");
        var stream = multiple
            ? service.DownloadMultipleAsync([(new Uri(server.Address, "slow"), path),
                (new Uri(server.Address, "slow"), Path.Combine(_root, "cancelled-2.bin"))], cancel.Token)
            : service.DownloadAsync(new Uri(server.Address, "slow"), path, cancel.Token);
        var operation = CollectAsync(stream, progress => { if (progress.ReceivedBytes > 0) started.TrySetResult(); });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (useServiceCancel) await service.CancelAsync();
        else cancel.Cancel();
        var error = await Assert.ThrowsAsync<Exception>(() => operation);
        Assert.IsNotInstanceOfType<TimeoutException>(error, "Cancellation must stop the iterator.");
        Assert.IsInstanceOfType<OperationCanceledException>(error);
        string retry = Path.Combine(_root, "retry.bin");
        await CollectAsync(service.DownloadAsync(new Uri(server.Address, "payload"), retry, default));
        CollectionAssert.AreEqual(Payload, File.ReadAllBytes(retry));
    }

    [TestMethod]
    public async Task PauseAndResume_StopsProgressUntilResumed_AndPreservesBytes() {
        byte[] large = Enumerable.Range(0, 1024 * 1024).Select(i => (byte)(i % 251)).ToArray();
        await using var server = new DownloadTestServer(_ => new(large, DelayPerChunk: 5));
        using var service = new MultiDownloadService();
        var received = new ConcurrentQueue<long>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string path = Path.Combine(_root, "paused.bin");
        var download = CollectAsync(service.DownloadAsync(server.Address, path, default), p => {
            received.Enqueue(p.ReceivedBytes);
            if (p.ReceivedBytes > 0) started.TrySetResult();
        });
        try {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            service.Pause();
            await Task.Delay(150); // Drain progress notifications that preceded Pause.
            long pausedAt = received.Last();
            await Task.Delay(200);
            Assert.AreEqual(pausedAt, received.Last());
            Assert.IsFalse(download.IsCompleted);
        }
        finally { service.Resume(); }
        await download;
        CollectionAssert.AreEqual(large, File.ReadAllBytes(path));
    }

    [TestMethod]
    public async Task DisposingServiceProvider_CancelsItsActiveDownload() {
        await using var server = new DownloadTestServer(_ => new(Payload, DelayPerChunk: 8));
        using var provider = new ServiceCollection().AddSingleton<MultiDownloadService>().BuildServiceProvider();
        var service = provider.GetRequiredService<MultiDownloadService>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = CollectAsync(service.DownloadAsync(server.Address, Path.Combine(_root, "disposed.bin"), default),
            p => { if (p.ReceivedBytes > 0) started.TrySetResult(); });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        provider.Dispose();
        await Assert.ThrowsAsync<OperationCanceledException>(() => operation);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ShaResponse_IsValidatedRatherThanAcceptingArbitraryText(bool valid) {
        string text = valid ? "\r\n" + new string('a', 64) + "\n" : "not-a-checksum";
        await using var server = new DownloadTestServer(_ => new(Encoding.UTF8.GetBytes(text)));
        using var service = new MultiDownloadService();
        if (valid) Assert.AreEqual(new string('a', 64), await service.DownloadShaTxtAsync(server.Address, default));
        else await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadShaTxtAsync(server.Address, default));
    }

    private static async Task<List<DownloadProgress>> CollectAsync(IAsyncEnumerable<DownloadProgress> source,
        Action<DownloadProgress>? observe = null) {
        async Task<List<DownloadProgress>> ReadAsync() {
            var values = new List<DownloadProgress>();
            await foreach (var value in source) { values.Add(value); observe?.Invoke(value); }
            return values;
        }
        return await ReadAsync().WaitAsync(TimeSpan.FromSeconds(15));
    }
}
