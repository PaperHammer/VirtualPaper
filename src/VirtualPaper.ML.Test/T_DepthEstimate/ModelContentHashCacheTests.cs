using System.Security.Cryptography;
using VirtualPaper.ML.DepthEstimate;

namespace VirtualPaper.ML.Test.T_DepthEstimate;

[TestClass]
public class ModelContentHashCacheTests {
    private string _root = null!, _model = null!;

    [TestInitialize]
    public void Setup() {
        _root = Path.Combine(Path.GetTempPath(), "vp-model-hash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _model = Path.Combine(_root, "model.onnx");
        File.WriteAllText(_model, "first model bytes");
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, recursive: true);

    [TestMethod]
    public void UnchangedModel_ReusesVerifiedHash_WithoutReadingModelBytesAgain() {
        var cache = new ModelContentHashCache();
        string expected = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(_model)));
        using (var first = new CountingStream(_model)) {
            Assert.AreEqual(expected, cache.GetHash(first, default));
            Assert.AreEqual(first.Length, first.BytesRead);
        }
        using var next = new CountingStream(_model);
        Assert.AreEqual(expected, cache.GetHash(next, default));
        Assert.AreEqual(0L, next.BytesRead, "A cache hit must avoid scanning the model contents.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ChangedModel_InvalidatesOnLengthOrTimestamp(bool sameLength) {
        var cache = new ModelContentHashCache();
        string original;
        using (var first = new CountingStream(_model)) original = cache.GetHash(first, default);
        DateTime timestamp = File.GetLastWriteTimeUtc(_model);
        File.WriteAllText(_model, sameLength ? "other model bytes" : "updated model with different length");
        File.SetLastWriteTimeUtc(_model, sameLength ? timestamp.AddSeconds(2) : timestamp);
        using var updated = new CountingStream(_model);
        string expected = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(_model)));
        Assert.AreEqual(expected, cache.GetHash(updated, default));
        Assert.AreNotEqual(original, expected);
        Assert.AreEqual(updated.Length, updated.BytesRead);
    }

    [TestMethod]
    public void ReplacementCreationTime_InvalidatesEvenWhenLengthAndWriteTimeMatch() {
        var cache = new ModelContentHashCache();
        using (var first = new CountingStream(_model)) _ = cache.GetHash(first, default);
        DateTime timestamp = File.GetLastWriteTimeUtc(_model);
        DateTime created = File.GetCreationTimeUtc(_model);
        File.WriteAllText(_model, "other model bytes");
        File.SetLastWriteTimeUtc(_model, timestamp);
        File.SetCreationTimeUtc(_model, created.AddSeconds(-10));
        using var updated = new CountingStream(_model);
        Assert.AreEqual(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(_model))), cache.GetHash(updated, default));
        Assert.AreEqual(updated.Length, updated.BytesRead);
    }

    [TestMethod]
    public void DifferentPaths_CannotShareFingerprint_AndOnlyLatestEntryIsRetained() {
        var cache = new ModelContentHashCache();
        string other = Path.Combine(_root, "other.onnx");
        File.WriteAllText(other, "other model bytes");
        File.SetLastWriteTimeUtc(other, File.GetLastWriteTimeUtc(_model));
        File.SetCreationTimeUtc(other, File.GetCreationTimeUtc(_model));
        string original;
        using (var first = new CountingStream(_model)) original = cache.GetHash(first, default);
        using (var second = new CountingStream(other)) Assert.AreNotEqual(original, cache.GetHash(second, default));
        using var again = new CountingStream(_model);
        Assert.AreEqual(original, cache.GetHash(again, default));
        Assert.AreEqual(again.Length, again.BytesRead, "The cache must stay bounded to one model entry.");
    }

    [TestMethod]
    public void InterruptedHash_IsNotCached_AndNextAttemptReadsCompleteModel() {
        var cache = new ModelContentHashCache();
        using var cancellation = new CancellationTokenSource();
        using (var interrupted = new CountingStream(_model, cancellation.Cancel)) {
            Assert.Throws<OperationCanceledException>(() => cache.GetHash(interrupted, cancellation.Token));
        }
        using var retry = new CountingStream(_model);
        Assert.AreEqual(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(_model))), cache.GetHash(retry, default));
        Assert.AreEqual(retry.Length, retry.BytesRead);
    }

    [TestMethod]
    public void ReadFailure_IsNotCached_AndCachedHitsStillHonorCancellation() {
        var cache = new ModelContentHashCache();
        using (var failed = new CountingStream(_model, () => throw new IOException("Controlled read failure")))
            Assert.Throws<IOException>(() => cache.GetHash(failed, default));
        using (var successful = new CountingStream(_model)) {
            _ = cache.GetHash(successful, default);
            Assert.AreEqual(successful.Length, successful.BytesRead);
        }
        using var cached = new CountingStream(_model);
        Assert.Throws<OperationCanceledException>(() => cache.GetHash(cached, new CancellationToken(canceled: true)));
        Assert.AreEqual(0L, cached.BytesRead);
    }

    private sealed class CountingStream(string path, Action? onRead = null) : FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) {
        public long BytesRead { get; private set; }
        public override int Read(byte[] buffer, int offset, int count) {
            int read = base.Read(buffer, offset, count);
            BytesRead += read;
            if (read > 0) onRead?.Invoke();
            return read;
        }
    }
}
