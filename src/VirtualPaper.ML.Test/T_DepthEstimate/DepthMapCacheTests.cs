using VirtualPaper.ML.DepthEstimate;
using VirtualPaper.ML.DepthEstimate.Interfaces;
using VirtualPaper.ML.DepthEstimate.Models;

namespace VirtualPaper.ML.Test.T_DepthEstimate;

[TestClass]
[TestCategory("Unit")]
public class DepthMapCacheTests {
    private string folder = null!, source = null!, model = null!;
    private int runs;

    [TestInitialize]
    public void Setup() {
        folder = Path.Combine(Path.GetTempPath(), "depth-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        source = Path.Combine(folder, "source.png");
        model = Path.Combine(folder, "model.onnx");
        File.WriteAllText(source, "fake source read by fake estimator");
        File.WriteAllText(model, "fake model");
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(folder, true);

    private Task<string> Generate(CancellationToken ct = default, Action? duringRun = null) =>
        DepthMapCache.GetOrCreateAsync(source, folder,
            () => new FakeEstimator(() => { Interlocked.Increment(ref runs); duringRun?.Invoke(); }), ct, model);

    [TestMethod]
    public async Task ConcurrentRequests_ReuseOnePublishedPng_AndInvalidateOnContentChange() {
        var paths = await Task.WhenAll(Generate(), Generate());
        Assert.AreEqual(paths[0], paths[1]);
        Assert.AreEqual(1, runs);
        File.WriteAllText(source, "changed source");
        string changedSource = await Generate();
        Assert.AreNotEqual(paths[0], changedSource);
        File.WriteAllText(model, "changed model");
        Assert.AreNotEqual(changedSource, await Generate());
        Assert.AreEqual(3, runs);
    }

    [TestMethod]
    public async Task CorruptedCache_IsRegenerated() {
        string path = await Generate();
        File.WriteAllText(path, "broken PNG");
        Assert.AreEqual(path, await Generate());
        Assert.AreEqual(2, runs);
    }

    [TestMethod]
    public async Task CancellationDuringRun_DoesNotPublish_AndReleasesGate() {
        using var cts = new CancellationTokenSource();
        await Assert.ThrowsAsync<OperationCanceledException>(() => Generate(cts.Token, cts.Cancel));
        Assert.AreEqual(0, Directory.GetFiles(folder, "depth-*.png").Length);
        Assert.AreEqual(0, Directory.GetDirectories(folder, ".depth-*").Length);
        Assert.IsTrue(File.Exists(await Generate()));
    }

    private sealed class FakeEstimator(Action run) : IDepthEstimate {
        public string ModelPath { get; private set; } = "";
        public void LoadModel(string? path = null) => ModelPath = path!;
        public DepthEstimateModelOutput Run(string imagePath) => Run(imagePath, null);
        public DepthEstimateModelOutput Run(string imagePath, DepthAnythingOptions? options, CancellationToken ct = default) {
            run();
            ct.ThrowIfCancellationRequested();
            return new([0, 1, 1, 0], 2, 2, 2, 2);
        }
        public string SaveDepthMap(DepthEstimateModelOutput output, string destination) {
            using var writer = new DepthAnythingV2();
            return writer.SaveDepthMap(output, destination);
        }
        public void Dispose() { }
    }
}
