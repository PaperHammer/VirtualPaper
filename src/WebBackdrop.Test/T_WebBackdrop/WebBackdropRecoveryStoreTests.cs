using System.Text;
using Workloads.Creation.WebBackdrop.Core.Utils;

namespace WebBackdrop.Test.T_WebBackdrop {
    [TestClass]
    public class WebBackdropRecoveryStoreTests {
        [TestInitialize]
        public void Initialize() {
            _testRoot = Path.Combine(
                Path.GetTempPath(),
                "VirtualPaper.Tests",
                $"WebRecovery-{Guid.NewGuid():N}");
            _projectRoot = Path.Combine(_testRoot, $"Project-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_projectRoot);
        }

        [TestCleanup]
        public void Cleanup() {
            DeleteDirectoryIfExists(WebBackdropRecoveryStore.GetProjectRecoveryDir(_projectRoot));
            DeleteDirectoryIfExists(_testRoot);
        }

        [TestMethod]
        public async Task WriteListRestoreAndDeleteBackup_RoundTripsNestedFile() {
            string sourcePath = Path.Combine(_projectRoot, "scripts", "app.js");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            await File.WriteAllTextAsync(sourcePath, "old");

            await WebBackdropRecoveryStore.WriteBackupAsync(
                _projectRoot,
                sourcePath,
                "const text = '新内容';",
                "UTF-8");

            string backupPath = WebBackdropRecoveryStore.GetBackupPath(_projectRoot, sourcePath);
            Assert.IsTrue(File.Exists(backupPath));
            Assert.HasCount(1, WebBackdropRecoveryStore.ListBackupPaths(_projectRoot));
            Assert.AreEqual(sourcePath, WebBackdropRecoveryStore.GetOriginalPath(_projectRoot, backupPath));

            await WebBackdropRecoveryStore.RestoreAsync(_projectRoot, backupPath);

            Assert.AreEqual("const text = '新内容';", await File.ReadAllTextAsync(sourcePath));
            Assert.IsTrue(WebBackdropRecoveryStore.AreFilesEqual(sourcePath, backupPath));

            WebBackdropRecoveryStore.DeleteBackup(_projectRoot, sourcePath);
            Assert.IsFalse(File.Exists(backupPath));
        }

        [TestMethod]
        public async Task WriteBackup_Utf8Bom_PreservesRequestedEncoding() {
            string sourcePath = Path.Combine(_projectRoot, "index.html");

            await WebBackdropRecoveryStore.WriteBackupAsync(
                _projectRoot,
                sourcePath,
                "你好",
                "UTF-8 BOM");

            byte[] bytes = await File.ReadAllBytesAsync(
                WebBackdropRecoveryStore.GetBackupPath(_projectRoot, sourcePath));
            CollectionAssert.AreEqual(Encoding.UTF8.Preamble.ToArray(), bytes[..3]);
            Assert.AreEqual("你好", Encoding.UTF8.GetString(bytes[3..]));
        }

        [TestMethod]
        public async Task WriteBackup_EmptyContent_DoesNotCreateFile() {
            string sourcePath = Path.Combine(_projectRoot, "empty.txt");

            await WebBackdropRecoveryStore.WriteBackupAsync(
                _projectRoot,
                sourcePath,
                string.Empty,
                "UTF-8");

            Assert.IsFalse(File.Exists(
                WebBackdropRecoveryStore.GetBackupPath(_projectRoot, sourcePath)));
        }

        [TestMethod]
        public void PathMapping_RejectsPathsOutsideConfiguredRoots() {
            string outsideFile = Path.Combine(_testRoot, "outside.txt");
            string outsideBackup = Path.Combine(_testRoot, "outside.backup");

            Assert.Throws<ArgumentException>(() =>
                WebBackdropRecoveryStore.GetBackupPath(_projectRoot, outsideFile));
            Assert.Throws<ArgumentException>(() =>
                WebBackdropRecoveryStore.GetOriginalPath(_projectRoot, outsideBackup));
        }

        [TestMethod]
        public void AreFilesEqual_HandlesEqualDifferentAndMissingFiles() {
            string left = Path.Combine(_projectRoot, "left.bin");
            string right = Path.Combine(_projectRoot, "right.bin");
            File.WriteAllBytes(left, [1, 2, 3]);
            File.WriteAllBytes(right, [1, 2, 3]);

            Assert.IsTrue(WebBackdropRecoveryStore.AreFilesEqual(left, right));

            File.WriteAllBytes(right, [1, 2, 4]);
            Assert.IsFalse(WebBackdropRecoveryStore.AreFilesEqual(left, right));
            Assert.IsFalse(WebBackdropRecoveryStore.AreFilesEqual(left, Path.Combine(_projectRoot, "missing")));
        }

        private static void DeleteDirectoryIfExists(string path) {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }

        private string _testRoot = null!;
        private string _projectRoot = null!;
    }
}
