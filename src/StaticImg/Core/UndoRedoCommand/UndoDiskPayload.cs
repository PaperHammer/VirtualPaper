using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common.Utils.Files;
using VirtualPaper.Common;

namespace Workloads.Creation.StaticImg.Core.UndoRedoCommand {
    /// <summary>
    /// Contract used by the StaticImg undo history to move retained pixel data
    /// from managed memory to the session's temporary disk store.
    /// </summary>
    internal interface IDiskSpillableUndoCommand : IDisposable {
        long DiskStorageBytes { get; }

        Task<bool> TrySpillToDiskAsync(
            UndoDiskStore store,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// A payload that starts in memory and can be replaced by a temporary file.
    /// Disk-backed data is loaded only for the duration of an undo/redo operation.
    /// </summary>
    internal sealed partial class UndoDiskPayload : IDisposable {
        public long ResidentMemoryBytes {
            get {
                lock (_syncRoot) return _memory?.LongLength ?? 0;
            }
        }

        public long DiskStorageBytes {
            get {
                lock (_syncRoot) return _diskStorageBytes;
            }
        }

        public UndoDiskPayload(byte[] data) {
            _memory = data ?? throw new ArgumentNullException(nameof(data));
        }

        public async ValueTask<byte[]> ReadAsync(CancellationToken cancellationToken = default) {
            byte[]? memory;
            string? filePath;
            long expectedLength;
            lock (_syncRoot) {
                ObjectDisposedException.ThrowIf(_isDisposed, this);
                memory = _memory;
                filePath = _filePath;
                expectedLength = _diskStorageBytes;
            }

            if (memory != null) return memory;
            if (filePath == null) throw new InvalidOperationException("Undo payload has no backing storage.");

            byte[] data = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
            if (data.LongLength != expectedLength)
                throw new EndOfStreamException($"Undo payload is incomplete: expected {expectedLength} bytes, read {data.LongLength} bytes.");
            return data;
        }

        public async Task<bool> TrySpillToDiskAsync(
            UndoDiskStore store,
            CancellationToken cancellationToken = default) {
            ArgumentNullException.ThrowIfNull(store);

            byte[] memory;
            lock (_syncRoot) {
                if (_isDisposed || _memory == null) return false;
                memory = _memory;
            }

            string? filePath = null;
            try {
                filePath = await store.WritePayloadAsync(memory, cancellationToken).ConfigureAwait(false);
                lock (_syncRoot) {
                    if (_isDisposed || !ReferenceEquals(_memory, memory)) {
                        store.DeletePayload(filePath);
                        return false;
                    }

                    _diskStorageBytes = memory.LongLength;
                    _filePath = filePath;
                    _storeOwner = store;
                    _memory = null;
                }
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                if (filePath != null) store.DeletePayload(filePath);
                throw;
            }
            catch {
                if (filePath != null) store.DeletePayload(filePath);
                return false;
            }
        }

        public void Dispose() {
            string? filePath;
            UndoDiskStore? owner;
            lock (_syncRoot) {
                if (_isDisposed) return;
                _isDisposed = true;
                _memory = null;
                filePath = _filePath;
                owner = _storeOwner;
                _filePath = null;
                _diskStorageBytes = 0;
            }

            if (filePath != null) owner?.DeletePayload(filePath);
        }

        private readonly object _syncRoot = new();
        private byte[]? _memory;
        private string? _filePath;
        private UndoDiskStore? _storeOwner;
        private long _diskStorageBytes;
        private bool _isDisposed;
    }

    /// <summary>
    /// Owns one isolated temporary directory per open StaticImg project.
    /// </summary>
    internal sealed partial class UndoDiskStore : IDisposable {
        public string SessionDirectory { get; }
        internal Task CleanupTask { get; }

        public UndoDiskStore(string sessionId, string? rootDirectory = null) {
            ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

            _rootDirectory = Path.GetFullPath(rootDirectory ?? Constants.CommonPaths.TempStaticImgUndoDir);
            string safeSessionId = string.Concat(sessionId.Split(Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(safeSessionId))
                safeSessionId = Guid.NewGuid().ToString("N");

            Directory.CreateDirectory(_rootDirectory);
            SessionDirectory = Path.GetFullPath(Path.Combine(_rootDirectory, safeSessionId));
            EnsureChildPath(SessionDirectory);
            Directory.CreateDirectory(SessionDirectory);
            _sessionLock = new FileStream(
                Path.Combine(SessionDirectory, SessionLockFileName),
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None);
            CleanupTask = Task.Run(() =>
                CleanupAbandonedSessions(_rootDirectory, SessionDirectory));
        }

        public async Task<string> WritePayloadAsync(
            byte[] data,
            CancellationToken cancellationToken = default) {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            ArgumentNullException.ThrowIfNull(data);

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _disposeCancellation.Token);
            CancellationToken effectiveToken = linkedCancellation.Token;
            string finalPath = Path.Combine(SessionDirectory, $"{Guid.NewGuid():N}.undo");
            string temporaryPath = finalPath + ".tmp";
            EnsureChildPath(finalPath);
            Interlocked.Increment(ref _activeWrites);

            try {
                await using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 1024 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan)) {
                    await stream.WriteAsync(data, effectiveToken).ConfigureAwait(false);
                    await stream.FlushAsync(effectiveToken).ConfigureAwait(false);
                }
                effectiveToken.ThrowIfCancellationRequested();
                File.Move(temporaryPath, finalPath);
                effectiveToken.ThrowIfCancellationRequested();
                _ownedFiles.TryAdd(finalPath, 0);
                return finalPath;
            }
            catch {
                FileUtil.TryDeletFile(temporaryPath);
                FileUtil.TryDeletFile(finalPath);
                throw;
            }
            finally {
                if (Interlocked.Decrement(ref _activeWrites) == 0 && _isDisposed)
                    TryDeleteSessionDirectory();
            }
        }

        public void DeletePayload(string filePath) {
            if (!FileUtil.IsPathWithinDirectory(_rootDirectory, filePath)) return;
            _ownedFiles.TryRemove(Path.GetFullPath(filePath), out _);
            FileUtil.TryDeletFile(filePath);
        }

        public void Dispose() {
            if (_isDisposed) return;
            _isDisposed = true;
            _disposeCancellation.Cancel();
            _sessionLock.Dispose();

            foreach (string filePath in _ownedFiles.Keys)
                FileUtil.TryDeletFile(filePath);
            _ownedFiles.Clear();
            TryDeleteSessionDirectory();
        }

        private void TryDeleteSessionDirectory() {
            if (Volatile.Read(ref _activeWrites) != 0) return;
            FileUtil.TryDeleteDirectory(SessionDirectory, isRecursive: true);
        }

        private static void CleanupAbandonedSessions(
            string rootDirectory,
            string currentSessionDirectory) {
            try {
                foreach (string directory in Directory.EnumerateDirectories(rootDirectory)) {
                    if (string.Equals(
                        Path.GetFullPath(directory),
                        currentSessionDirectory,
                        StringComparison.OrdinalIgnoreCase))
                        continue;

                    string lockFile = Path.Combine(directory, SessionLockFileName);
                    FileStream? abandonedLock = null;
                    try {
                        if (File.Exists(lockFile)) {
                            abandonedLock = new FileStream(
                                lockFile,
                                FileMode.Open,
                                FileAccess.ReadWrite,
                                FileShare.None);
                        }
                    }
                    catch (IOException) {
                        // Another process still owns this session.
                        continue;
                    }
                    catch (UnauthorizedAccessException) {
                        continue;
                    }

                    abandonedLock?.Dispose();
                    FileUtil.TryDeleteDirectory(directory, isRecursive: true);
                }
            }
            catch {
                // Failure to enumerate stale data must not prevent opening a project.
            }
        }

        private void EnsureChildPath(string path) {
            if (!FileUtil.IsPathWithinDirectory(_rootDirectory, path))
                throw new InvalidOperationException("Undo payload path escaped the configured temporary root.");
        }

        private readonly string _rootDirectory;
        private readonly ConcurrentDictionary<string, byte> _ownedFiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly CancellationTokenSource _disposeCancellation = new();
        private readonly FileStream _sessionLock;
        private int _activeWrites;
        private volatile bool _isDisposed;
        private const string SessionLockFileName = ".session.lock";
    }
}
