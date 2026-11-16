using VirtualPaper.Common;
using VirtualPaper.WpSettingsPanel.ViewModels;

namespace VirtualPaper.UI.Test.T_WpSettings;

[TestClass]
public class ImportScanTests {
    private string _root = null!;
    private readonly List<string> _junctions = [];

    [TestInitialize]
    public void Setup() => _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(),
        "VirtualPaper.ImportScan-" + Guid.NewGuid().ToString("N"))).FullName;

    [TestCleanup]
    public void Cleanup() {
        foreach (string link in _junctions.AsEnumerable().Reverse()) {
            Assert.IsTrue(Path.GetFullPath(link).StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            if (Directory.Exists(link)) Directory.Delete(link, recursive: false);
        }
        Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public async Task JunctionAlias_AndAncestorCycle_ImportEachPhysicalFileOnlyOnce() {
        string first = Write("first.png", Convert.FromHexString("89504E470D0A1A0A"));
        string child = Write("nested/child.gif", "GIF89a"u8.ToArray());
        string nested = Path.GetDirectoryName(child)!;
        CreateJunction(Path.Combine(nested, "back"), _root);
        CreateJunction(Path.Combine(_root, "alias"), nested);
        var result = new LibraryContentsViewModel.ImportResult();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var values = await LibraryContentsViewModel.ScanImportFilesAsync([_root, nested], result, cancellation.Token);

        CollectionAssert.AreEquivalent(new[] { first, child }, values.Select(item => item.FilePath).ToArray());
        Assert.IsEmpty(result.Failed);
        CollectionAssert.AreEqual("GIF89a"u8.ToArray(), File.ReadAllBytes(child));
    }

    [TestMethod]
    public async Task InvalidRoot_DoesNotPreventTheNextRootFromBeingScanned() {
        string valid = Write("valid.gif", "GIF89a"u8.ToArray());
        var result = new LibraryContentsViewModel.ImportResult();

        var values = await LibraryContentsViewModel.ScanImportFilesAsync(["invalid\0path", valid], result, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { valid }, values.Select(item => item.FilePath).ToArray());
        Assert.HasCount(1, result.Failed);
        Assert.IsInstanceOfType<ArgumentException>(result.Failed[0].Error);
    }

    [TestMethod]
    public void DirectoryEnumeration_ReadFailure_IsPropagatedAndDisposesItsEnumerator() {
        bool disposed = false;
        IEnumerable<string> Entries() {
            try { yield return "first.png"; throw new IOException("Directory enumeration failed"); }
            finally { disposed = true; }
        }

        Assert.ThrowsExactly<IOException>(() => LibraryContentsViewModel.CollectImportEntries(Entries(), CancellationToken.None));

        Assert.IsTrue(disposed);
    }

    [TestMethod]
    public async Task NestedFolders_KeepStableOrder_DeduplicateSelections_AndKeepUnsupportedFiles() {
        string first = Write("a.png", Convert.FromHexString("89504E470D0A1A0A"));
        string nested = Write("nested/b.gif", "GIF89a"u8.ToArray());
        string unsupported = Write("z.txt", "unsupported"u8.ToArray());
        var result = new LibraryContentsViewModel.ImportResult();

        var values = await LibraryContentsViewModel.ScanImportFilesAsync([
            _root, first.ToUpperInvariant(), Path.Combine(_root, "nested"), Path.Combine(_root, ".", "a.png")
        ], result, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { first, nested, unsupported }, values.Select(item => item.FilePath).ToArray());
        CollectionAssert.AreEqual(new[] { FileType.FImage, FileType.FGif, FileType.FUnknown }, values.Select(item => item.FType).ToArray());
        Assert.IsEmpty(result.Failed);
        CollectionAssert.AreEqual(Convert.FromHexString("89504E470D0A1A0A"), File.ReadAllBytes(first));
    }

    [TestMethod]
    public async Task UnreadableFile_AndMissingFolder_DoNotDiscardOtherCandidates() {
        string locked = Write("a.png", Convert.FromHexString("89504E470D0A1A0A"));
        string valid = Write("b.gif", "GIF89a"u8.ToArray());
        string missing = Path.Combine(_root, "missing");
        using var fileLock = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);
        var result = new LibraryContentsViewModel.ImportResult();

        var values = await LibraryContentsViewModel.ScanImportFilesAsync([_root, missing], result, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { valid }, values.Select(item => item.FilePath).ToArray());
        CollectionAssert.AreEqual(new[] { locked, missing }, result.Failed.Select(item => item.FilePath).ToArray());
        Assert.IsTrue(result.Failed.All(item => item.Error is IOException));
        Assert.IsEmpty(result.Unsupported, "Read errors must not be reported as unsupported formats.");
    }

    [TestMethod]
    public async Task CancellationBetweenRoots_StopsScanning_AndIsNotRecordedAsAReadError() {
        using var cancellation = new CancellationTokenSource();
        string missing = Path.Combine(_root, "first-missing");
        var result = new LibraryContentsViewModel.ImportResult();
        IEnumerable<string> Inputs() {
            yield return missing;
            cancellation.Cancel();
            yield return Path.Combine(_root, "must-not-be-scanned");
        }

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            LibraryContentsViewModel.ScanImportFilesAsync(Inputs(), result, cancellation.Token));

        CollectionAssert.AreEqual(new[] { missing }, result.Failed.Select(item => item.FilePath).ToArray());
    }

    [TestMethod]
    public void DirectoryEnumeration_CanBeCanceledBeforeAllEntriesAreCollected_AndDisposesTheEnumerator() {
        using var cancellation = new CancellationTokenSource();
        int enumerated = 0;
        bool disposed = false;
        IEnumerable<string> Entries() {
            try {
                for (int i = 0; i < 10000; i++) {
                    if (i == 1) cancellation.Cancel();
                    enumerated++;
                    yield return i.ToString();
                }
            }
            finally { disposed = true; }
        }

        Assert.Throws<OperationCanceledException>(() => LibraryContentsViewModel.CollectImportEntries(Entries(), cancellation.Token));

        Assert.AreEqual(2, enumerated, "Cancellation must stop enumeration before the remaining entries are read.");
        Assert.IsTrue(disposed);
    }

    [TestMethod]
    public void DirectoryEnumeration_AlreadyCanceled_DoesNotStartReading() {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        bool enumerated = false;
        IEnumerable<string> Entries() { enumerated = true; yield return "unused"; }

        Assert.Throws<OperationCanceledException>(() => LibraryContentsViewModel.CollectImportEntries(Entries(), cancellation.Token));

        Assert.IsFalse(enumerated);
    }

    [TestMethod]
    public async Task AlreadyCanceled_DoesNotReadAnySource() {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = new LibraryContentsViewModel.ImportResult();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            LibraryContentsViewModel.ScanImportFilesAsync([Path.Combine(_root, "missing")], result, cancellation.Token));

        Assert.IsEmpty(result.Failed);
    }

    private string Write(string relativePath, byte[] bytes) {
        string path = Path.GetFullPath(Path.Combine(_root, relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private void CreateJunction(string link, string target) {
        var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in new[] { "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(start)!;
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.AreEqual(0, process.ExitCode, output);
        _junctions.Add(link);
    }
}
