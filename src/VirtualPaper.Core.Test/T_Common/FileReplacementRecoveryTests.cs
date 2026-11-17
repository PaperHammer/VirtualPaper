using VirtualPaper.Common.Utils.Files;

namespace VirtualPaper.Core.Test.T_Common;

[TestClass]
public class FileReplacementRecoveryTests {
    [TestInitialize]
    public void Initialize() {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "VirtualPaper.Replace", Guid.NewGuid().ToString("N"))).FullName;
        _destination = Path.Combine(_root, "settings.json");
        _temporary = Path.Combine(_root, "pending.tmp");
        File.WriteAllText(_destination, "original bytes");
        File.WriteAllText(_temporary, "new bytes");
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, recursive: true);

    [TestMethod]
    [DataRow(1176, false)]
    [DataRow(1177, true)]
    public void FailedNativeReplacement_RestoresTheOriginalAndDoesNotRepeatTheOperation(int code, bool renamed) {
        var failure = new IOException("Replacement failed", unchecked((int)0x80070000) | code);
        int calls = 0;

        var error = Assert.ThrowsExactly<IOException>(() => FileUtil.ReplaceFileWithBackup(_temporary, _destination, (_, target, backup) => {
            calls++;
            if (renamed) File.Move(target, backup);
            throw failure;
        }));

        Assert.AreSame(failure, error);
        Assert.AreEqual(1, calls);
        Assert.AreEqual("original bytes", File.ReadAllText(_destination));
        Assert.AreEqual("new bytes", File.ReadAllText(_temporary));
        Assert.IsEmpty(Directory.GetFiles(_root, "*.bak"));
    }

    [TestMethod]
    public void FailedRecovery_PreservesBackupAndBothErrors() {
        var failure = new IOException("Replacement failed");
        string? backupPath = null;
        FileStream? locked = null;
        try {
            var error = Assert.ThrowsExactly<AggregateException>(() => FileUtil.ReplaceFileWithBackup(_temporary, _destination, (_, target, backup) => {
                backupPath = backup;
                File.Move(target, backup);
                locked = new FileStream(backup, FileMode.Open, FileAccess.Read, FileShare.Read);
                throw failure;
            }));

            Assert.AreSame(failure, error.InnerExceptions[0]);
            Assert.HasCount(2, error.InnerExceptions);
            Assert.IsNotNull(backupPath);
            StringAssert.Contains(error.Message, backupPath);
            Assert.AreEqual("original bytes", File.ReadAllText(backupPath));
            Assert.AreEqual("new bytes", File.ReadAllText(_temporary));
            Assert.IsFalse(File.Exists(_destination));
        }
        finally { locked?.Dispose(); }
    }

    [TestMethod]
    public void BackupCleanupFailure_DoesNotTurnACommittedSaveIntoFailure() {
        string? backupPath = null;
        FileStream? locked = null;
        try {
            FileUtil.ReplaceFileWithBackup(_temporary, _destination, (source, target, backup) => {
                backupPath = backup;
                File.Replace(source, target, backup);
                locked = new FileStream(backup, FileMode.Open, FileAccess.Read, FileShare.Read);
            });

            Assert.AreEqual("new bytes", File.ReadAllText(_destination));
            Assert.IsFalse(File.Exists(_temporary));
            Assert.IsNotNull(backupPath);
            Assert.AreEqual("original bytes", File.ReadAllText(backupPath));
        }
        finally { locked?.Dispose(); }
    }

    private string _root = null!, _destination = null!, _temporary = null!;
}
