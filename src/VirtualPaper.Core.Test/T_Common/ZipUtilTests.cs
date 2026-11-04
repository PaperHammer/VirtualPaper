using System.IO.Compression;
using System.Text;
using VirtualPaper.Common.Utils.Archive;

namespace VirtualPaper.Core.Test.T_Common;

[TestClass]
public class ZipUtilTests {
    private string _tempDirectory = null!;

    [TestInitialize]
    public void Initialize() {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"ZipUtilTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [TestCleanup]
    public void Cleanup() {
        if (Directory.Exists(_tempDirectory)) {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task CompressAndDecompress_RoundTripsBinaryPayload() {
        var payload = Enumerable.Range(0, 4096).Select(i => (byte)(i % 251)).ToArray();

        var compressed = await ZipUtil.CompressAsync(payload);
        var restored = await ZipUtil.DecompressAsync(compressed);

        CollectionAssert.AreEqual(payload, restored);
        Assert.IsLessThan(payload.Length, compressed.Length);
    }

    [TestMethod]
    public async Task Decompress_CorruptedPayloadThrows() {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => ZipUtil.DecompressAsync([0x01, 0x02, 0x03, 0x04]));
    }

    [TestMethod]
    public void CreateZip_UsesRelativeEntryNames() {
        var source = Path.Combine(_tempDirectory, "source");
        var nested = Path.Combine(source, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "value.txt"), "content", Encoding.UTF8);
        var archivePath = Path.Combine(_tempDirectory, "output.zip");

        ZipUtil.CreateZip(archivePath, [source]);

        using var archive = ZipFile.OpenRead(archivePath);
        Assert.HasCount(1, archive.Entries);
        Assert.AreEqual("nested/value.txt", archive.Entries[0].FullName);
    }

    [TestMethod]
    public void CreateZip_FileOutsideDeclaredParentIsRejected() {
        var declaredParent = Path.Combine(_tempDirectory, "declared");
        Directory.CreateDirectory(declaredParent);
        var outsideFile = Path.Combine(_tempDirectory, "outside.txt");
        File.WriteAllText(outsideFile, "must not be archived");
        var archivePath = Path.Combine(_tempDirectory, "output.zip");
        var input = new ZipUtil.FileData {
            ParentDirectory = declaredParent,
            Files = [outsideFile],
        };

        Assert.ThrowsExactly<ArgumentException>(
            () => ZipUtil.CreateZip(archivePath, [input]));
    }
}
