using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Workloads.Creation.WebBackdrop.Core.Utils;
using Workloads.Creation.WebBackdrop.Models.SerializableData;

namespace WebBackdrop.Test.T_WebBackdrop;

[TestClass]
public class WebProjectCopyTests {
    private string _root = null!;
    private WebDesignFileUtil _project = null!;

    [TestInitialize]
    public void Initialize() {
        _root = Path.Combine(Path.GetTempPath(), "VirtualPaper.Tests", "WebCopy-" + Guid.NewGuid().ToString("N"));
        string source = Directory.CreateDirectory(Path.Combine(_root, "Original")).FullName;
        File.WriteAllText(Path.Combine(source, "index.html"), "<html>original</html>");
        _project = WebDesignFileUtil.Create(source);
        _project.EnsureProjectStructure();
    }

    [TestCleanup]
    public void Cleanup() {
        if (Directory.Exists(_root)) {
            foreach (string path in Directory.GetFiles(_root, "*", SearchOption.AllDirectories)) {
                File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
            }
            Directory.Delete(_root, recursive: true);
        }
    }

    [TestMethod]
    public void Copy_PreservesSourceAndCopiesUnregisteredAssetsEmptyDirectoriesAndUnsavedEdits() {
        string source = _project.ProjectFolder;
        Directory.CreateDirectory(Path.Combine(source, "assets", "empty"));
        File.WriteAllBytes(Path.Combine(source, "assets", "image.bin"), [0, 1, 255]);
        File.WriteAllText(Path.Combine(source, ".hidden"), "keep");
        var before = Snapshot(source);
        string destination = Directory.CreateDirectory(Path.Combine(_root, "Renamed")).FullName;
        string projectFile = WebProjectCopy.Copy(_project, destination, [
            new(Path.Combine(source, "index.html"), "<html>unsaved</html>", "UTF-8"),
            new(Path.Combine(source, "notes.txt"), "未保存\r\n", "UTF-16 LE")
        ]);

        Assert.AreEqual(Path.Combine(destination, "Renamed.vpw"), projectFile);
        Assert.AreEqual("<html>unsaved</html>", File.ReadAllText(Path.Combine(destination, "index.html")));
        CollectionAssert.AreEqual(Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("未保存\r\n")).ToArray(),
            File.ReadAllBytes(Path.Combine(destination, "notes.txt")));
        CollectionAssert.AreEqual(new byte[] { 0, 1, 255 }, File.ReadAllBytes(Path.Combine(destination, "assets", "image.bin")));
        Assert.IsTrue(Directory.Exists(Path.Combine(destination, "assets", "empty")));
        Assert.AreEqual("keep", File.ReadAllText(Path.Combine(destination, ".hidden")));
        Assert.IsFalse(File.Exists(Path.Combine(destination, "Original.vpw")));
        AssertSnapshot(before, source);

        var manifest = JsonNode.Parse(File.ReadAllText(projectFile))!;
        Assert.AreEqual("Renamed", manifest["name"]!.GetValue<string>());
        Assert.AreEqual("Renamed.vpw", manifest["files"]!.AsArray()
            .Single(file => file!["role"]!.GetValue<string>() == "solution")!["path"]!.GetValue<string>());

        using var copySession = new WebProjectSession(projectFile);
        Assert.AreEqual(Path.Combine(destination, "index.html"), copySession.DesignFileUtil.EntryFilePath);
        string archive = WebProjectExporter.Export(copySession.DesignFileUtil, Path.Combine(_root, "copy.zip"));
        using (var zip = ZipFile.OpenRead(archive)) {
            Assert.AreEqual("<html>unsaved</html>", ReadArchiveText(zip, "index.html"));
            Assert.IsNotNull(zip.GetEntry("project.json"));
            Assert.IsNull(zip.GetEntry("Renamed.vpw"));
            Assert.IsNull(zip.GetEntry("Original.vpw"));
        }
        string fullArchive = WebProjectExporter.ExportFull(copySession.DesignFileUtil, Path.Combine(_root, "copy-full.zip"));
        using (var zip = ZipFile.OpenRead(fullArchive)) {
            Assert.AreEqual("<html>unsaved</html>", ReadArchiveText(zip, "index.html"));
            Assert.AreEqual("未保存\r\n", ReadArchiveText(zip, "notes.txt"));
            using var asset = new MemoryStream();
            using (var stream = zip.GetEntry("assets/image.bin")!.Open()) stream.CopyTo(asset);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 255 }, asset.ToArray());
            var archivedManifest = JsonNode.Parse(ReadArchiveText(zip, "Renamed.vpw"))!;
            Assert.AreEqual("Renamed", archivedManifest["name"]!.GetValue<string>());
            Assert.AreEqual("Renamed.vpw", archivedManifest["files"]!.AsArray()
                .Single(file => file!["role"]!.GetValue<string>() == "solution")!["path"]!.GetValue<string>());
            Assert.IsNull(zip.GetEntry("Original.vpw"));
        }
        File.WriteAllText(copySession.DesignFileUtil.EntryFilePath, "copy only");
        AssertSnapshot(before, source);
    }

    [TestMethod]
    public void Copy_UsesUnsavedManifestAndPreservesCustomMetadata() {
        var manifest = JsonNode.Parse(File.ReadAllText(_project.ProjectFilePath))!;
        manifest["custom"] = "preserve";
        var before = Snapshot(_project.ProjectFolder);
        string path = WebProjectCopy.Copy(_project, Path.Combine(_root, "Copy"), [
            new(_project.ProjectFilePath, manifest.ToJsonString(), "UTF-8")
        ]);
        Assert.AreEqual("preserve", JsonNode.Parse(File.ReadAllText(path))!["custom"]!.GetValue<string>());
        CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(_project.ProjectFolder, "project.json")),
            File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(path)!, "project.json")));
        AssertSnapshot(before, _project.ProjectFolder);
    }

    [TestMethod]
    [DataRow("same")]
    [DataRow("child")]
    [DataRow("parent")]
    public void Copy_RejectsOverlappingDirectories(string location) {
        string destination = location switch {
            "same" => _project.ProjectFolder,
            "child" => Path.Combine(_project.ProjectFolder, "copy"),
            _ => _root
        };
        var before = Snapshot(_project.ProjectFolder);
        Assert.ThrowsExactly<IOException>(() => WebProjectCopy.Copy(_project, destination, []));
        AssertSnapshot(before, _project.ProjectFolder);
        AssertNoStaging();
    }

    [TestMethod]
    public void Copy_RejectsOccupiedDestinationWithoutOverwritingFiles() {
        string destination = Directory.CreateDirectory(Path.Combine(_root, "Copy")).FullName;
        File.WriteAllText(Path.Combine(destination, "user.txt"), "keep");
        Assert.ThrowsExactly<IOException>(() => WebProjectCopy.Copy(_project, destination, []));
        Assert.AreEqual("keep", File.ReadAllText(Path.Combine(destination, "user.txt")));
        Assert.HasCount(1, Directory.GetFiles(destination));
        AssertNoStaging();
    }

    [TestMethod]
    public void Copy_CancellationLeavesBothProjectsUntouched() {
        string destination = Directory.CreateDirectory(Path.Combine(_root, "Copy")).FullName;
        var before = Snapshot(_project.ProjectFolder);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => WebProjectCopy.Copy(_project, destination, [], cancellation.Token));
        Assert.IsEmpty(Directory.GetFileSystemEntries(destination));
        AssertSnapshot(before, _project.ProjectFolder);
        AssertNoStaging();
    }

    [TestMethod]
    public void Copy_CancelledAfterFirstUnsavedEdit_RemovesStagingAndPreservesBothProjects() {
        string destination = Directory.CreateDirectory(Path.Combine(_root, "Copy")).FullName;
        string entry = Path.Combine(_project.ProjectFolder, "index.html");
        var before = Snapshot(_project.ProjectFolder);
        using var cancellation = new CancellationTokenSource();
        var edits = new CancelAfterFirstEdit([
            new(entry, "first unsaved edit", "UTF-8"),
            new(Path.Combine(_project.ProjectFolder, "notes.txt"), "second unsaved edit", "UTF-8")
        ], () => {
            // Enumeration resumes only after the first edit has been written to the copy.
            string staging = Directory.GetDirectories(_root, ".vp-copy-*").Single();
            Assert.AreEqual("first unsaved edit", File.ReadAllText(Path.Combine(staging, "index.html")));
            cancellation.Cancel();
        });

        Assert.Throws<OperationCanceledException>(() => WebProjectCopy.Copy(_project, destination, edits, cancellation.Token));

        Assert.IsTrue(cancellation.IsCancellationRequested);
        Assert.IsEmpty(Directory.GetFileSystemEntries(destination));
        AssertSnapshot(before, _project.ProjectFolder);
        AssertNoStaging();
    }

    [TestMethod]
    public void Copy_LockedSourceFile_RemovesStagingAndPreservesBothProjects() {
        string source = Path.Combine(_project.ProjectFolder, "assets", "locked.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllBytes(source, [1, 2, 3]);
        string destination = Directory.CreateDirectory(Path.Combine(_root, "Copy")).FullName;
        var before = Snapshot(_project.ProjectFolder);

        using (var locked = new FileStream(source, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
            Assert.Throws<IOException>(() => WebProjectCopy.Copy(_project, destination, []));
        }

        Assert.IsEmpty(Directory.GetFileSystemEntries(destination));
        AssertSnapshot(before, _project.ProjectFolder);
        AssertNoStaging();
    }

    [TestMethod]
    public void Copy_InvalidUnsavedManifestCleansUpAndKeepsTheOriginal() {
        string destination = Directory.CreateDirectory(Path.Combine(_root, "Copy")).FullName;
        var before = Snapshot(_project.ProjectFolder);
        Assert.Throws<System.Text.Json.JsonException>(() => WebProjectCopy.Copy(_project, destination, [
            new(_project.ProjectFilePath, "{ invalid", "UTF-8")
        ]));
        Assert.IsEmpty(Directory.GetFileSystemEntries(destination));
        AssertSnapshot(before, _project.ProjectFolder);
        AssertNoStaging();
    }

    [TestMethod]
    public void Copy_RejectsExternalEditsAndManifestPaths() {
        string destination = Path.Combine(_root, "Copy");
        Assert.ThrowsExactly<ArgumentException>(() => WebProjectCopy.Copy(_project, destination, [
            new(Path.Combine(_root, "outside.txt"), "not project data", "UTF-8")
        ]));
        var manifest = JsonNode.Parse(File.ReadAllText(_project.ProjectFilePath))!;
        manifest["files"]!.AsArray().Add(new JsonObject { ["path"] = "../outside.txt", ["role"] = "asset" });
        Assert.ThrowsExactly<ArgumentException>(() => WebProjectCopy.Copy(_project, destination, [
            new(_project.ProjectFilePath, manifest.ToJsonString(), "UTF-8")
        ]));
        Assert.IsFalse(Directory.Exists(destination));
        AssertNoStaging();
    }

    [TestMethod]
    public void Copy_RejectsProjectNameCollisionWithoutLosingSourceFiles() {
        File.WriteAllText(Path.Combine(_project.ProjectFolder, "Copy.vpw"), "another project");
        var before = Snapshot(_project.ProjectFolder);
        Assert.ThrowsExactly<IOException>(() => WebProjectCopy.Copy(_project, Path.Combine(_root, "Copy"), []));
        AssertSnapshot(before, _project.ProjectFolder);
        AssertNoStaging();
    }

    [TestMethod]
    public void Copy_ReadOnlySourceProducesAnEditableCopyWithoutChangingSourceAttributes() {
        string source = Path.Combine(_project.ProjectFolder, "index.html");
        File.SetAttributes(source, File.GetAttributes(source) | FileAttributes.ReadOnly);
        string path = WebProjectCopy.Copy(_project, Path.Combine(_root, "Copy"), [new(source, "copy edit", "UTF-8")]);
        string copy = Path.Combine(Path.GetDirectoryName(path)!, "index.html");
        Assert.AreEqual("copy edit", File.ReadAllText(copy));
        Assert.IsFalse((File.GetAttributes(copy) & FileAttributes.ReadOnly) != 0);
        Assert.IsTrue((File.GetAttributes(source) & FileAttributes.ReadOnly) != 0);
        Assert.AreEqual("<html>original</html>", File.ReadAllText(source));
    }

    private static string ReadArchiveText(ZipArchive archive, string path) {
        var entry = archive.GetEntry(path);
        Assert.IsNotNull(entry, $"Missing archive entry: {path}");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    // Trigger cancellation during preparation through the existing snapshot input,
    // without timers, background filesystem races or production-only test hooks.
    private sealed class CancelAfterFirstEdit(IReadOnlyList<WebProjectTextSnapshot> edits, Action afterFirstEdit)
        : IReadOnlyList<WebProjectTextSnapshot> {
        public int Count => edits.Count;
        public WebProjectTextSnapshot this[int index] => edits[index];
        public IEnumerator<WebProjectTextSnapshot> GetEnumerator() {
            for (int i = 0; i < edits.Count; i++) {
                if (i == 1) afterFirstEdit();
                yield return edits[i];
            }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private void AssertNoStaging() => Assert.IsEmpty(Directory.GetDirectories(_root, ".vp-copy-*"));
    private static Dictionary<string, byte[]> Snapshot(string directory) => Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(directory, path), File.ReadAllBytes);
    private static void AssertSnapshot(Dictionary<string, byte[]> expected, string directory) {
        var actual = Snapshot(directory);
        CollectionAssert.AreEquivalent(expected.Keys.ToArray(), actual.Keys.ToArray());
        foreach (var pair in expected) CollectionAssert.AreEqual(pair.Value, actual[pair.Key], pair.Key);
    }
}
