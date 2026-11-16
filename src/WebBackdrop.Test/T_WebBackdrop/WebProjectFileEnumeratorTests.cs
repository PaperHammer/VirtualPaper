using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using Workloads.Creation.WebBackdrop.Core.Utils;

namespace WebBackdrop.Test.T_WebBackdrop;

[TestClass]
public sealed class WebProjectFileEnumeratorTests {
    [TestInitialize]
    public void Initialize() {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VirtualPaper.FileEnumeration-" + Guid.NewGuid().ToString("N"))).FullName;
        _project = Directory.CreateDirectory(Path.Combine(_root, "project")).FullName;
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, recursive: true);

    [TestMethod]
    public void Enumeration_ExcludesDependencyDirectoriesAtEveryDepth_ButKeepsOrdinaryAndHiddenFiles() {
        var expected = new[] { Write("index.html"), Write(".gitignore"), Write("assets/styles/site.css"), Write("assets/node_modules.txt") };
        foreach (string name in new[] { ".git", ".HG", ".svn", ".VS", "BIN", "obj", "Node_Modules" }) {
            Write(name + "/ignored.txt");
            Write("assets/" + name + "/ignored.txt");
        }
        CollectionAssert.AreEquivalent(expected, Enumerate());
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(4)]
    [DataRow(int.MaxValue)]
    public void FileLimit_BoundsTheWholeTraversal_WithoutDuplicates(int limit) {
        var files = new[] { Write("a.txt"), Write("b.txt"), Write("nested/c.txt"), Write("nested/deep/d.txt") };
        var found = WebProjectFileEnumerator.EnumerateFiles(_project, CancellationToken.None, limit).ToArray();
        Assert.HasCount(Math.Clamp(limit, 0, files.Length), found);
        Assert.HasCount(found.Length, found.Distinct().ToArray());
        Assert.IsTrue(found.All(files.Contains));
    }

    [TestMethod]
    public void MissingRoot_ProducesNoFiles() {
        Assert.IsEmpty(WebProjectFileEnumerator.EnumerateFiles(Path.Combine(_root, "missing"), CancellationToken.None).ToArray());
    }

    [TestMethod]
    public void CancellationBeforeOrDuringEnumeration_StopsWithoutReadingLaterFiles() {
        Write("a.txt");
        Write("b.txt");
        Write("nested/c.txt");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.IsEmpty(WebProjectFileEnumerator.EnumerateFiles(_project, canceled.Token).ToArray());

        using var cancellation = new CancellationTokenSource();
        using var iterator = WebProjectFileEnumerator.EnumerateFiles(_project, cancellation.Token).GetEnumerator();
        Assert.IsTrue(iterator.MoveNext());
        Assert.IsTrue(File.Exists(iterator.Current));
        cancellation.Cancel();
        Assert.IsFalse(iterator.MoveNext());
    }

    [TestMethod]
    public void ChildRemovedAfterParentSnapshot_IsSkipped() {
        string first = Write("index.html");
        string child = Path.GetDirectoryName(Write("removed/child.txt"))!;
        using var iterator = WebProjectFileEnumerator.EnumerateFiles(_project, CancellationToken.None).GetEnumerator();
        Assert.IsTrue(iterator.MoveNext());
        Assert.AreEqual(first, iterator.Current);
        Directory.Delete(child, recursive: true);
        Assert.IsFalse(iterator.MoveNext());
    }

    [TestMethod]
    public void PendingDirectoryRemovedDuringTraversal_IsSkipped() {
        var files = new[] { Write("one/a.txt"), Write("two/b.txt") };
        using var iterator = WebProjectFileEnumerator.EnumerateFiles(_project, CancellationToken.None).GetEnumerator();
        Assert.IsTrue(iterator.MoveNext());
        Assert.Contains(iterator.Current, files);
        string retained = iterator.Current;
        string pending = files.Single(file => file != retained);
        Directory.Delete(Path.GetDirectoryName(pending)!, recursive: true);
        Assert.IsFalse(iterator.MoveNext());
        Assert.IsTrue(File.Exists(retained));
    }

    [TestMethod]
    public void UnreadableDirectory_IsSkipped_AndReadableFilesRemainAvailable() {
        string readable = Write("index.html");
        var blocked = new DirectoryInfo(Path.GetDirectoryName(Write("blocked/private.txt"))!);
        byte[] original = blocked.GetAccessControl().GetSecurityDescriptorBinaryForm();
        var denied = blocked.GetAccessControl();
        denied.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ListDirectory, AccessControlType.Deny));
        try {
            blocked.SetAccessControl(denied);
            Assert.Throws<UnauthorizedAccessException>(() => Directory.GetFiles(blocked.FullName));
            CollectionAssert.AreEqual(new[] { readable }, Enumerate());
            Assert.IsEmpty(WebProjectFileEnumerator.EnumerateFiles(blocked.FullName, CancellationToken.None).ToArray());
        }
        finally {
            // Mark the restored DACL as modified; persisting an untouched snapshot is a no-op.
            var restored = new DirectorySecurity();
            restored.SetSecurityDescriptorBinaryForm(original, AccessControlSections.Access);
            blocked.SetAccessControl(restored);
        }
    }

    [TestMethod]
    public void DirectoryJunction_IsNotFollowed_AndItsTargetIsUntouched() {
        string readable = Write("index.html");
        string external = Directory.CreateDirectory(Path.Combine(_root, "external")).FullName;
        string protectedFile = Path.Combine(external, "outside.txt");
        File.WriteAllText(protectedFile, "outside content");
        string link = Path.Combine(_project, "linked");
        try {
            var start = new ProcessStartInfo("cmd.exe") {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            };
            foreach (string argument in new[] { "/c", "mklink", "/J", link, external }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.AreEqual(0, process.ExitCode, output);
            Assert.AreEqual(FileAttributes.ReparsePoint, File.GetAttributes(link) & FileAttributes.ReparsePoint);
            CollectionAssert.AreEqual(new[] { readable }, Enumerate());
            Assert.AreEqual("outside content", File.ReadAllText(protectedFile));
        }
        finally { if (Directory.Exists(link)) Directory.Delete(link, recursive: false); }
    }

    private string[] Enumerate() => WebProjectFileEnumerator.EnumerateFiles(_project, CancellationToken.None).ToArray();

    private string Write(string relativePath) {
        string path = Path.Combine(_project, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, relativePath);
        return path;
    }

    private string _root = null!;
    private string _project = null!;
}
