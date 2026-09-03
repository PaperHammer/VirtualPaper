using VirtualPaper.Common;
using VirtualPaper.Common.Utils.Files;

namespace VirtualPaper.Core.Test.T_Common;

[TestClass]
public class FileFilterTests {
    private string _tempDirectory = null!;

    [TestInitialize]
    public void Initialize() {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"FileFilterTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [TestCleanup]
    public void Cleanup() {
        if (Directory.Exists(_tempDirectory)) {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void GetFileType_MagicHeaderWithSpoofedExtensionIsRejected() {
        var path = Write("spoofed.png", [0x47, 0x49, 0x46, 0x38, 0x39, 0x61]);

        Assert.AreEqual(FileType.FUnknown, FileFilter.GetFileType(path));
    }

    [TestMethod]
    public void GetFileType_WebPRequiresRiffAndWebPMarkers() {
        var valid = Write("valid.webp", [
            0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50,
        ]);
        var invalid = Write("invalid.webp", [
            0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x4E, 0x4F, 0x50, 0x45,
        ]);

        Assert.AreEqual(FileType.FImage, FileFilter.GetFileType(valid));
        Assert.AreEqual(FileType.FUnknown, FileFilter.GetFileType(invalid));
    }

    [TestMethod]
    [DataRow("archive.zip", new byte[] { 0x50, 0x4B, 0x03, 0x04 })]
    [DataRow("archive.rar", new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07 })]
    [DataRow("archive.7z", new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C })]
    public void GetFileType_RecognizesSupportedArchiveSignatures(string name, byte[] header) {
        Assert.AreEqual(FileType.FWebZip, FileFilter.GetFileType(Write(name, header)));
    }

    [TestMethod]
    public void GetFileType_MissingAndShortFilesReturnUnknown() {
        Assert.AreEqual(FileType.FUnknown, FileFilter.GetFileType(Path.Combine(_tempDirectory, "missing.png")));
        Assert.AreEqual(FileType.FUnknown, FileFilter.GetFileType(Write("short.png", [0x89, 0x50, 0x4E])));
    }

    [TestMethod]
    public void GetRuntimeFileType_IsCaseInsensitiveAndRejectsUnknownExtension() {
        Assert.AreEqual(FileType.FDesign, FileFilter.GetRuntimeFileType(".VPD"));
        Assert.ThrowsExactly<ArgumentException>(() => FileFilter.GetRuntimeFileType(".unsupported"));
    }

    private string Write(string name, byte[] content) {
        var path = Path.Combine(_tempDirectory, name);
        File.WriteAllBytes(path, content);
        return path;
    }
}
