using MessagePack;
using VirtualPaper.Common.Utils.Storage;

namespace VirtualPaper.Core.Test.T_Common;

[TestClass]
public class BufferSaverTests {
    private string _tempDirectory = null!;

    [TestInitialize]
    public void Initialize() {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"BufferSaverTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [TestCleanup]
    public void Cleanup() {
        if (Directory.Exists(_tempDirectory)) {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task SaveManually_BeforeAnyData_DoesNotCreateFile() {
        var path = Path.Combine(_tempDirectory, "unused.bin");
        var saver = new BufferSaver<string>();

        await saver.SaveManuallyAsync();

        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public async Task SaveManually_WritesAllBufferedValuesInOrder() {
        var path = Path.Combine(_tempDirectory, "values.bin");
        var saver = new BufferSaver<string>();
        var expected = new[] { "first", "second", "third" }
            .SelectMany(value => MessagePackSerializer.Serialize(value))
            .ToArray();

        await saver.SaveToBufferAsync("first", path);
        await saver.SaveToBufferAsync("second", path);
        await saver.SaveToBufferAsync("third", path);
        Assert.IsFalse(File.Exists(path));
        await saver.SaveManuallyAsync();

        CollectionAssert.AreEqual(expected, await File.ReadAllBytesAsync(path));
    }

    [TestMethod]
    public async Task ConcurrentSaves_DoNotLoseBufferedData() {
        var path = Path.Combine(_tempDirectory, "concurrent.bin");
        var saver = new BufferSaver<string>();
        const int count = 50;
        var serializedValue = MessagePackSerializer.Serialize("same-value");

        await Task.WhenAll(Enumerable.Range(0, count)
            .Select(_ => saver.SaveToBufferAsync("same-value", path)));
        await saver.SaveManuallyAsync();

        var actual = await File.ReadAllBytesAsync(path);
        Assert.HasCount(serializedValue.Length * count, actual);
        for (var offset = 0; offset < actual.Length; offset += serializedValue.Length) {
            CollectionAssert.AreEqual(
                serializedValue,
                actual.AsSpan(offset, serializedValue.Length).ToArray());
        }
    }

    [TestMethod]
    public async Task FailedFlush_KeepsBufferForRetry() {
        var path = Path.Combine(_tempDirectory, "retry.bin");
        await File.WriteAllBytesAsync(path, []);
        var saver = new BufferSaver<string>();
        var expected = MessagePackSerializer.Serialize("recoverable");

        await using (var lockStream = new FileStream(
            path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
            await saver.SaveToBufferAsync("recoverable", path);
            await Assert.ThrowsAsync<IOException>(() => saver.SaveManuallyAsync());
        }

        await saver.SaveManuallyAsync();
        CollectionAssert.AreEqual(expected, await File.ReadAllBytesAsync(path));
    }

    [TestMethod]
    public async Task ThresholdExceeded_FlushesWithoutManualSave() {
        var path = Path.Combine(_tempDirectory, "automatic.bin");
        var saver = new BufferSaver<string>(bufferSizeThreshold: 0);

        await saver.SaveToBufferAsync("value", path);

        CollectionAssert.AreEqual(
            MessagePackSerializer.Serialize("value"),
            await File.ReadAllBytesAsync(path));
    }
}
