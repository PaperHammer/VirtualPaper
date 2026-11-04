using VirtualPaper.Common.Utils.UndoRedo;
using Workloads.Creation.StaticImg.Core.UndoRedoCommand;
using Workloads.Creation.StaticImg.InkSystem.Utils;

namespace StaticImg.Test.T_StaticImg {
    [TestClass]
    public class UndoDiskPayloadTests {
        private string _testRoot = null!;

        [TestInitialize]
        public void Setup() {
            _testRoot = Path.Combine(
                Path.GetTempPath(),
                "VirtualPaper.Tests",
                nameof(UndoDiskPayloadTests),
                Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public void Cleanup() {
            if (Directory.Exists(_testRoot))
                Directory.Delete(_testRoot, recursive: true);
        }

        [TestMethod]
        public async Task SpillToDisk_ReleasesResidentMemory_AndReadsSameBytes() {
            byte[] expected = Enumerable.Range(0, 4096).Select(i => (byte)(i % 251)).ToArray();
            using var store = new UndoDiskStore("session", _testRoot);
            using var payload = new UndoDiskPayload(expected);

            bool spilled = await payload.TrySpillToDiskAsync(store);
            byte[] actual = await payload.ReadAsync();

            Assert.IsTrue(spilled);
            Assert.AreEqual(0L, payload.ResidentMemoryBytes);
            Assert.AreEqual(expected.LongLength, payload.DiskStorageBytes);
            CollectionAssert.AreEqual(expected, actual);
            Assert.HasCount(1, Directory.GetFiles(store.SessionDirectory, "*.undo"));
        }

        [TestMethod]
        public async Task DisposePayload_DeletesItsBackingFile() {
            using var store = new UndoDiskStore("session", _testRoot);
            var payload = new UndoDiskPayload(new byte[1024]);
            Assert.IsTrue(await payload.TrySpillToDiskAsync(store));
            string filePath = Directory.GetFiles(store.SessionDirectory, "*.undo").Single();

            payload.Dispose();

            Assert.IsFalse(File.Exists(filePath));
        }

        [TestMethod]
        public async Task DisposeStore_RemovesSessionDirectory() {
            var store = new UndoDiskStore("session", _testRoot);
            using var payload = new UndoDiskPayload(new byte[1024]);
            Assert.IsTrue(await payload.TrySpillToDiskAsync(store));
            string sessionDirectory = store.SessionDirectory;

            store.Dispose();

            Assert.IsFalse(Directory.Exists(sessionDirectory));
        }

        [TestMethod]
        public async Task ReadAsync_WhenBackingFileIsTruncated_ThrowsEndOfStreamException() {
            using var store = new UndoDiskStore("session", _testRoot);
            using var payload = new UndoDiskPayload(new byte[1024]);
            Assert.IsTrue(await payload.TrySpillToDiskAsync(store));
            string filePath = Directory.GetFiles(store.SessionDirectory, "*.undo").Single();
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None))
                stream.SetLength(100);

            await Assert.ThrowsAsync<EndOfStreamException>(async () =>
                await payload.ReadAsync());
        }

        [TestMethod]
        public async Task UndoHistory_WhenMemoryExceeded_SpillsAndCanUndoFromDisk() {
            byte[] expected = Enumerable.Range(0, 4096).Select(i => (byte)(i % 251)).ToArray();
            var command = new PayloadCommand(expected);
            string sessionId = Guid.NewGuid().ToString("N");
            string sessionDirectory;

            using (var history = new StaticImgUndoRedoUtil(
                isSaved: true,
                maxStackSize: 20,
                maxMemoryBytes: 100,
                maxDiskBytes: 10_000,
                sessionId: sessionId,
                diskRootDirectory: _testRoot)) {
                sessionDirectory = Path.Combine(_testRoot, sessionId);
                history.RecordCommand(command);
                await history.WaitForPendingMaintenanceAsync();

                Assert.IsTrue(history.CanUndo);
                Assert.AreEqual(16L, history.TotalMemoryBytes);
                Assert.AreEqual(expected.LongLength, history.TotalDiskBytes);
                Assert.HasCount(1, Directory.GetFiles(sessionDirectory, "*.undo"));

                await history.UndoAsync();
                CollectionAssert.AreEqual(expected, command.LastReadBytes);
            }

            Assert.IsFalse(Directory.Exists(sessionDirectory));
        }

        [TestMethod]
        public async Task OpeningStore_RemovesAbandonedSessionDirectory() {
            string abandonedDirectory = Path.Combine(_testRoot, "abandoned");
            Directory.CreateDirectory(abandonedDirectory);
            File.WriteAllText(Path.Combine(abandonedDirectory, ".session.lock"), string.Empty);
            File.WriteAllBytes(Path.Combine(abandonedDirectory, "stale.undo"), new byte[32]);

            using var current = new UndoDiskStore("current", _testRoot);
            await current.CleanupTask;

            Assert.IsFalse(Directory.Exists(abandonedDirectory));
            Assert.IsTrue(Directory.Exists(current.SessionDirectory));
        }

        [TestMethod]
        public async Task OpeningStore_PreservesAnotherActiveSession() {
            using var active = new UndoDiskStore("active", _testRoot);
            using var current = new UndoDiskStore("current", _testRoot);
            await Task.WhenAll(active.CleanupTask, current.CleanupTask);

            Assert.IsTrue(Directory.Exists(active.SessionDirectory));
            Assert.IsTrue(Directory.Exists(current.SessionDirectory));
        }

        private sealed class PayloadCommand :
            IUndoableCommand,
            IMemoryAwareUndoableCommand,
            IDiskSpillableUndoCommand {
            public string Description => "payload";
            public long EstimatedMemoryBytes => _payload.ResidentMemoryBytes + 16;
            long IDiskSpillableUndoCommand.DiskStorageBytes => _payload.DiskStorageBytes;
            public byte[]? LastReadBytes { get; private set; }

            public PayloadCommand(byte[] bytes) {
                _payload = new UndoDiskPayload(bytes);
            }

            public Task ExecuteAsync() => ReadAsync();
            public Task UndoAsync() => ReadAsync();

            Task<bool> IDiskSpillableUndoCommand.TrySpillToDiskAsync(
                UndoDiskStore store,
                CancellationToken cancellationToken) =>
                _payload.TrySpillToDiskAsync(store, cancellationToken);

            public void Dispose() => _payload.Dispose();

            private async Task ReadAsync() {
                LastReadBytes = await _payload.ReadAsync();
            }

            private readonly UndoDiskPayload _payload;
        }
    }
}
