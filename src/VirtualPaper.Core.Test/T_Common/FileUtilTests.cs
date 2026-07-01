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

        // ── IsValidFileName ───────────────────────────────────────────

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
