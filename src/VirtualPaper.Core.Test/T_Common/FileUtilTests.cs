using VirtualPaper.Common.Utils.Files;

namespace VirtualPaper.Core.Test.T_Common {
    [TestClass]
    public class FileUtilTests {
        // ── GetSafeFilename ───────────────────────────────────────────

        [TestMethod]
        public void GetSafeFilename_NoInvalidChars_Unchanged() {
            Assert.AreEqual("normal_file.txt", FileUtil.GetSafeFilename("normal_file.txt"));
        }

        [TestMethod]
        public void GetSafeFilename_WithInvalidChars_Replaced() {
            var result = FileUtil.GetSafeFilename("file<name>:test.txt");
            Assert.DoesNotContain('<', result);
            Assert.DoesNotContain('>', result);
            Assert.DoesNotContain(':', result);
        }

        // ── IsValidPathSegmentName ───────────────────────────────────────────

        [TestMethod]
        [DataRow("test.vpd")]
        [DataRow("my file.png")]
        [DataRow("document (1).jpg")]
        public void IsValidFileName_ValidNames_ReturnsTrue(string name) {
            Assert.IsTrue(FileUtil.IsValidFileName(name));
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("test<file>.vpd")]
        [DataRow("test|file.vpd")]
        [DataRow("test\"file.vpd")]
        public void IsValidFileName_InvalidNames_ReturnsFalse(string name) {
            Assert.IsFalse(FileUtil.IsValidFileName(name));
        }

        // ── IsValidFilePath ───────────────────────────────────────────

        [TestMethod]
        public void IsValidFilePath_ExistingDirectory_ReturnsTrue() {
            var path = Path.Combine(Path.GetTempPath(), "test.vpd");
            Assert.IsTrue(FileUtil.IsValidFilePath(path));
        }

        [TestMethod]
        public void IsValidFilePath_InvalidChars_ReturnsFalse() {
            Assert.IsFalse(FileUtil.IsValidFilePath("C:\\test<file>.vpd"));
        }

        // ── SizeSuffix ────────────────────────────────────────────────

        [TestMethod]
        [DataRow(0, "0.0 bytes")]
        [DataRow(500, "500.0 bytes")]
        [DataRow(1024, "1.0 KB")]
        [DataRow(1536, "1.5 KB")]
        [DataRow(1048576, "1.0 MB")]
        [DataRow(1073741824, "1.0 GB")]
        public void SizeSuffix_CorrectFormatting(long value, string expected) {
            Assert.AreEqual(expected, FileUtil.SizeSuffix(value));
        }

        [TestMethod]
        public void SizeSuffix_NegativeValue_HasMinusPrefix() {
            var result = FileUtil.SizeSuffix(-1024);
            Assert.StartsWith("-", result);
        }

        [TestMethod]
        public void SizeSuffix_LargeValue_RoundsUp() {
            // 1024 MB → 应显示为 1 GB 而非 1024.0 MB
            var result = FileUtil.SizeSuffix(1024L * 1024 * 1024);
            Assert.AreEqual("1.0 GB", result);
        }

        // ── NextAvailableFilename ─────────────────────────────────────

        [TestMethod]
        public void NextAvailableFilename_NonExistent_ReturnsOriginal() {
            var path = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.txt");
            Assert.AreEqual(path, FileUtil.NextAvailableFilename(path));
        }

        [TestMethod]
        public void NextAvailableFilename_Existing_ReturnsNumbered() {
            var dir = Path.GetTempPath();
            var baseName = $"testfile_{Guid.NewGuid():N}.txt";
            var basePath = Path.Combine(dir, baseName);
            File.WriteAllText(basePath, "");
            try {
                var next = FileUtil.NextAvailableFilename(basePath);
                Assert.AreNotEqual(basePath, next);
                Assert.Contains("(1)", next);
            } finally {
                File.Delete(basePath);
            }
        }

        // ── GetTempFile ───────────────────────────────────────────────

        [TestMethod]
        public void GetTempFile_CreatesFile() {
            var dir = Path.Combine(Path.GetTempPath(), $"testdir_{Guid.NewGuid():N}");
            try {
                var tempFile = FileUtil.GetTempFile(dir);
                Assert.IsTrue(File.Exists(tempFile));
                Assert.StartsWith(dir, tempFile);
            } finally {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void GetTempFile_WithExtension_HasCorrectExt() {
            var dir = Path.Combine(Path.GetTempPath(), $"testdir_{Guid.NewGuid():N}");
            try {
                var tempFile = FileUtil.GetTempFile(dir, ".vpd");
                Assert.EndsWith(".vpd", tempFile);
            } finally {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
        }

        // ── GetChecksumSHA256 ─────────────────────────────────────────

        [TestMethod]
        public void IsPathWithinDirectory_DirectChild_ReturnsTrue() {
            string root = Path.Combine(Path.GetTempPath(), "path-root");
            string child = Path.Combine(root, "session", "payload.undo");

            Assert.IsTrue(FileUtil.IsPathWithinDirectory(root, child));
        }

        [TestMethod]
        public void IsPathWithinDirectory_ParentTraversalOutsideRoot_ReturnsFalse() {
            string root = Path.Combine(Path.GetTempPath(), "path-root");
            string escaped = Path.Combine(root, "session", "..", "..", "payload.undo");

            Assert.IsFalse(FileUtil.IsPathWithinDirectory(root, escaped));
        }

        [TestMethod]
        public void IsPathWithinDirectory_SiblingWithSamePrefix_ReturnsFalse() {
            string root = Path.Combine(Path.GetTempPath(), "path-root");
            string sibling = Path.Combine(Path.GetTempPath(), "path-root-backup", "payload.undo");

            Assert.IsFalse(FileUtil.IsPathWithinDirectory(root, sibling));
        }

        [TestMethod]
        public void IsPathWithinDirectory_RootItself_ReturnsFalse() {
            string root = Path.Combine(Path.GetTempPath(), "path-root");

            Assert.IsFalse(FileUtil.IsPathWithinDirectory(root, root));
        }

        [TestMethod]
        public void GetContainedRelativePath_NestedPath_ReturnsNormalizedRelativePath() {
            string root = Path.Combine(Path.GetTempPath(), "contained-root");
            string path = Path.Combine(root, "images", "cover.png");

            string relative = FileUtil.GetContainedRelativePath(root, path, nameof(path));

            Assert.AreEqual(Path.Combine("images", "cover.png"), relative);
        }

        [TestMethod]
        public void GetContainedRelativePath_RootItself_ReturnsCurrentDirectory() {
            string root = Path.Combine(Path.GetTempPath(), "contained-root");

            string relative = FileUtil.GetContainedRelativePath(root, root, nameof(root));

            Assert.AreEqual(".", relative);
        }

        [TestMethod]
        public void GetContainedRelativePath_SiblingWithSamePrefix_Throws() {
            string root = Path.Combine(Path.GetTempPath(), "contained-root");
            string sibling = Path.Combine(Path.GetTempPath(), "contained-root-backup", "cover.png");

            var exception = Assert.ThrowsExactly<ArgumentException>(() =>
                FileUtil.GetContainedRelativePath(root, sibling, nameof(sibling)));

            Assert.AreEqual(nameof(sibling), exception.ParamName);
        }

        [TestMethod]
        public void GetContainedRelativePath_ParentTraversalOutsideRoot_Throws() {
            string root = Path.Combine(Path.GetTempPath(), "contained-root");
            string escaped = Path.Combine(root, "images", "..", "..", "cover.png");

            var exception = Assert.ThrowsExactly<ArgumentException>(() =>
                FileUtil.GetContainedRelativePath(root, escaped, nameof(escaped)));

            Assert.AreEqual(nameof(escaped), exception.ParamName);
        }

        [TestMethod]
        [DataRow(@"C:\Library\images\cover.png", @"C:\Library", @"D:\Moved", @"D:\Moved\images\cover.png")]
        [DataRow(@"c:\library\cover.png", @"C:\Library", @"D:\Moved", @"D:\Moved\cover.png")]
        [DataRow(@"C:\Library", @"C:\Library", @"D:\Moved", @"D:\Moved")]
        [DataRow(@"C:\Library\", @"C:\Library", @"D:\Moved", @"D:\Moved")]
        [DataRow(@"C:\Library-backup\cover.png", @"C:\Library", @"D:\Moved", @"C:\Library-backup\cover.png")]
        [DataRow(@"C:\Library\..\external.png", @"C:\Library", @"D:\Moved", @"C:\Library\..\external.png")]
        [DataRow(@"E:\external.png", @"C:\Library", @"D:\Moved", @"E:\external.png")]
        [DataRow("", @"C:\Library", @"D:\Moved", "")]
        [DataRow(@"C:\cover.png", @"C:\", @"D:\Moved", @"D:\Moved\cover.png")]
        public void RelocatePath_PreservesBoundariesAndExternalReferences(string path, string source, string target, string expected) {
            Assert.AreEqual(expected, FileUtil.RelocatePath(path, source, target));
        }

        [TestMethod]
        public void CopyDirectory_WithOverwriteDisabled_PreservesExistingNestedFile() {
            string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.CopyDirectory", Guid.NewGuid().ToString("N"));
            string source = Path.Combine(root, "source");
            string target = Path.Combine(root, "target");
            try {
                Directory.CreateDirectory(Path.Combine(source, "nested"));
                Directory.CreateDirectory(Path.Combine(target, "nested"));
                File.WriteAllText(Path.Combine(source, "nested", "media.txt"), "source content");
                string existing = Path.Combine(target, "nested", "media.txt");
                File.WriteAllText(existing, "user content");
                Assert.Throws<IOException>(() => FileUtil.CopyDirectory(source, target, true, overwrite: false));
                Assert.AreEqual("user content", File.ReadAllText(existing));
                Assert.AreEqual("source content", File.ReadAllText(Path.Combine(source, "nested", "media.txt")));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
        }

        [TestMethod]
        public void CopyDirectory_Cancelled_DoesNotCreateDestination() {
            string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.CopyDirectory", Guid.NewGuid().ToString("N"));
            try {
                string source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
                string target = Path.Combine(root, "target");
                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();
                Assert.Throws<OperationCanceledException>(() => FileUtil.CopyDirectory(source, target,
                    copySubDirs: true, overwrite: false, cancellationToken: cancellation.Token));
                Assert.IsFalse(Directory.Exists(target));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void CopyDirectory_ReadOnlyPolicy_DoesNotChangeSource(bool clearReadOnly) {
            string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.CopyDirectory", Guid.NewGuid().ToString("N"));
            string sourceFile = Path.Combine(root, "source", "nested", "media.txt");
            string targetFile = Path.Combine(root, "target", "nested", "media.txt");
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
                Directory.CreateDirectory(Path.Combine(root, "source", "empty"));
                File.WriteAllText(sourceFile, "source content");
                File.SetAttributes(sourceFile, File.GetAttributes(sourceFile) | FileAttributes.ReadOnly);
                if (clearReadOnly) {
                    FileUtil.CopyDirectory(Path.Combine(root, "source"), Path.Combine(root, "target"),
                        copySubDirs: true, overwrite: false, cancellationToken: CancellationToken.None,
                        rejectLinks: true, clearReadOnly: true);
                }
                else {
                    FileUtil.CopyDirectory(Path.Combine(root, "source"), Path.Combine(root, "target"), true);
                }
                Assert.AreEqual("source content", File.ReadAllText(targetFile));
                Assert.IsTrue(Directory.Exists(Path.Combine(root, "target", "empty")));
                Assert.AreEqual(FileAttributes.ReadOnly, File.GetAttributes(sourceFile) & FileAttributes.ReadOnly);
                Assert.AreEqual(!clearReadOnly, (File.GetAttributes(targetFile) & FileAttributes.ReadOnly) != 0);
            }
            finally {
                foreach (string file in new[] { sourceFile, targetFile }) {
                    if (File.Exists(file)) File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
                }
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void CopyDirectory_RejectLinks_DoesNotCopyThroughSourceOrDestinationJunction(bool sourceLink) {
            string root = Path.Combine(Path.GetTempPath(), "VirtualPaper.CopyDirectory", Guid.NewGuid().ToString("N"));
            string source = Path.Combine(root, "source");
            string target = Path.Combine(root, "target");
            string external = Path.Combine(root, "external");
            string link = Path.Combine(sourceLink ? source : target, "nested");
            try {
                Directory.CreateDirectory(source);
                Directory.CreateDirectory(target);
                Directory.CreateDirectory(external);
                File.WriteAllText(Path.Combine(external, "media.txt"), "external content");
                if (!sourceLink) {
                    Directory.CreateDirectory(Path.Combine(source, "nested"));
                    File.WriteAllText(Path.Combine(source, "nested", "media.txt"), "source content");
                }
                var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                foreach (string argument in new[] { "/c", "mklink", "/J", link, external }) start.ArgumentList.Add(argument);
                using var process = System.Diagnostics.Process.Start(start)!;
                string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit();
                Assert.AreEqual(0, process.ExitCode, output);

                Assert.Throws<IOException>(() => FileUtil.CopyDirectory(source, target,
                    copySubDirs: true, overwrite: true, cancellationToken: CancellationToken.None, rejectLinks: true));
                Assert.Throws<IOException>(() => FileUtil.EnsureNoDirectoryLinks(Path.Combine(link, "child")));
                Assert.AreEqual("external content", File.ReadAllText(Path.Combine(external, "media.txt")));
                if (sourceLink) Assert.IsFalse(File.Exists(Path.Combine(target, "nested", "media.txt")));
            }
            finally {
                if (Directory.Exists(link)) Directory.Delete(link, recursive: false);
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        [TestMethod]
        public void GetChecksumSHA256_SameContent_SameHash() {
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, "hello world");
            try {
                var h1 = FileUtil.GetChecksumSHA256(tempFile);
                var h2 = FileUtil.GetChecksumSHA256(tempFile);
                Assert.AreEqual(h1, h2);
                Assert.AreEqual(64, h1.Length); // SHA256 → 64 hex chars
            } finally {
                File.Delete(tempFile);
            }
        }

        [TestMethod]
        public void GetChecksumSHA256_DifferentContent_DifferentHash() {
            var f1 = Path.GetTempFileName();
            var f2 = Path.GetTempFileName();
            File.WriteAllText(f1, "hello");
            File.WriteAllText(f2, "world");
            try {
                Assert.AreNotEqual(
                    FileUtil.GetChecksumSHA256(f1),
                    FileUtil.GetChecksumSHA256(f2));
            } finally {
                File.Delete(f1);
                File.Delete(f2);
            }
        }

        // ── IsFileGreaterThanThreshold ────────────────────────────────

        [TestMethod]
        public void IsFileGreaterThanThreshold_Larger_ReturnsTrue() {
            var tempFile = Path.GetTempFileName();
            File.WriteAllBytes(tempFile, new byte[1000]);
            try {
                Assert.IsTrue(FileUtil.IsFileGreaterThanThreshold(tempFile, 500));
            } finally {
                File.Delete(tempFile);
            }
        }

        [TestMethod]
        public void IsFileGreaterThanThreshold_Smaller_ReturnsFalse() {
            var tempFile = Path.GetTempFileName();
            File.WriteAllBytes(tempFile, new byte[100]);
            try {
                Assert.IsFalse(FileUtil.IsFileGreaterThanThreshold(tempFile, 500));
            } finally {
                File.Delete(tempFile);
            }
        }

        [TestMethod]
        public void IsFileGreaterThanThreshold_NonExistent_ReturnsFalse() {
            Assert.IsFalse(FileUtil.IsFileGreaterThanThreshold(@"C:\nonexistent_file", 100));
        }
    }
}
