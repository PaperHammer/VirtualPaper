using VirtualPaper.Common.Utils.UndoRedo.Events;

namespace VirtualPaper.Common.Utils.UndoRedo {
    public sealed class UndoRedoUtil<TCommand> : IDisposable where TCommand : IUndoableCommand {
        public event EventHandler<CommandEventArgs>? BeforeExecute;
        public event EventHandler<CommandEventArgs>? AfterExecute;
        public event EventHandler<CommandEventArgs>? BeforeUndo;
        public event EventHandler<CommandEventArgs>? AfterUndo;
        public event EventHandler<CommandEventArgs>? BeforeRedo;
        public event EventHandler<CommandEventArgs>? AfterRedo;
        public event EventHandler<IsSavedChangedEventArgs>? IsSavedChanged;

        public class CommandEventArgs : EventArgs {
            public TCommand Command { get; }
            public CommandEventArgs(TCommand command) => Command = command;
        }

        public UndoRedoUtil(
            bool isSaved,
            int? maxStackSize = null,
            long? maxMemoryBytes = null,
            Func<TCommand, long>? commandSizeEstimator = null,
            long? maxStorageBytes = null,
            Func<TCommand, long>? commandStorageEstimator = null,
            Func<TCommand, bool>? tryReduceCommandMemory = null,
            Func<TCommand, CancellationToken, Task<bool>>? tryReduceCommandMemoryAsync = null,
            Action<TCommand>? commandReleased = null) {
            _maxStackSize = maxStackSize;
            _maxMemoryBytes = maxMemoryBytes;
            _commandSizeEstimator = commandSizeEstimator;
            _maxStorageBytes = maxStorageBytes;
            _commandStorageEstimator = commandStorageEstimator;
            _tryReduceCommandMemory = tryReduceCommandMemory;
            _tryReduceCommandMemoryAsync = tryReduceCommandMemoryAsync;
            _commandReleased = commandReleased;
            _eventContext = SynchronizationContext.Current;
            _lastIsSaved = isSaved;
        }

        /// <summary>
        /// 获取当前是否有可撤销的命令。
        /// </summary>
        public bool CanUndo {
            get {
                lock (_listLock) return _undoStack.Count > 0;
            }
        }

        /// <summary>
        /// 获取当前是否有可重做的命令。
        /// </summary>
        public bool CanRedo {
            get {
                lock (_listLock) return _redoStack.Count > 0;
            }
        }

        /// <summary>
        /// 获取当前撤销栈中的命令数量。
        /// </summary>
        public int UndoStackSize {
            get {
                lock (_listLock) return _undoStack.Count;
            }
        }

        /// <summary>
        /// 获取当前重做栈中的命令数量。
        /// </summary>
        public int RedoStackSize {
            get {
                lock (_listLock) return _redoStack.Count;
            }
        }

        /// <summary>
        /// 获取撤销栈命令当前估算的托管内存占用，单位为字节。
        /// </summary>
        public long UndoMemoryBytes {
            get {
                lock (_listLock) return _undoMemoryBytes;
            }
        }

        /// <summary>
        /// 获取重做栈命令当前估算的托管内存占用，单位为字节。
        /// </summary>
        public long RedoMemoryBytes {
            get {
                lock (_listLock) return _redoMemoryBytes;
            }
        }

        /// <summary>
        /// 获取撤销栈和重做栈当前估算的托管内存总占用，单位为字节。
        /// </summary>
        public long TotalMemoryBytes {
            get {
                lock (_listLock) return _undoMemoryBytes + _redoMemoryBytes;
            }
        }

        /// <summary>
        /// 获取撤销栈命令当前占用的外部存储空间，单位为字节。
        /// </summary>
        public long UndoStorageBytes {
            get {
                lock (_listLock) return _undoStorageBytes;
            }
        }

        /// <summary>
        /// 获取重做栈命令当前占用的外部存储空间，单位为字节。
        /// </summary>
        public long RedoStorageBytes {
            get {
                lock (_listLock) return _redoStorageBytes;
            }
        }

        /// <summary>
        /// 获取撤销栈和重做栈当前占用的外部存储总空间，单位为字节。
        /// </summary>
        public long TotalStorageBytes {
            get {
                lock (_listLock) return _undoStorageBytes + _redoStorageBytes;
            }
        }

        private bool _isUndoingOrRedoing;
        /// <summary>
        /// 获取当前是否正在执行撤销或重做命令。
        /// </summary>
        public bool IsUndoingOrRedoing {
            get {
                lock (_listLock) return _isUndoingOrRedoing;
            }
            private set { lock (_listLock) _isUndoingOrRedoing = value; }
        }

        /// <summary>
        /// 获取当前撤销栈状态是否与最近一次标记的已保存状态一致。
        /// </summary>
        public bool IsSaved {
            get {
                lock (_listLock) {
                    // 栈为空的情况：如果保存的时候就是空的，现在也是空的，说明【已保存】(返回 true)
                    if (_undoStack.Count == 0) return _savedAtEmpty;

                    // 栈不为空的情况：当前栈顶的对象 和 记录的保存对象 是同一个内存地址，说明【已保存】(返回 true)
                    return object.ReferenceEquals(
                        (object?)_undoStack.Last!.Value.Command,
                        (object?)_savedCommand);
                }
            }
        }

        public void MarkAsSaved() {
            lock (_listLock) {
                if (_undoStack.Count == 0) {
                    _savedCommand = default;
                    _savedAtEmpty = true;
                }
                else {
                    _savedCommand = _undoStack.Last!.Value.Command;
                    _savedAtEmpty = false;
                }
            }
            CheckSavedStateChanged();
        }

        private void CheckSavedStateChanged() {
            bool currentIsSaved = IsSaved;
            lock (_savedStateEventLock) {
                if (currentIsSaved == _lastIsSaved) return;
                _lastIsSaved = currentIsSaved;
            }
            IsSavedChanged?.Invoke(this, new IsSavedChangedEventArgs(currentIsSaved));
        }

        private void CheckSavedStateChangedFromBackground() {
            if (_eventContext == null || SynchronizationContext.Current == _eventContext) {
                CheckSavedStateChanged();
                return;
            }

            _eventContext.Post(static state =>
                ((UndoRedoUtil<TCommand>)state!).CheckSavedStateChanged(), this);
        }

        public void Record(TCommand command) {
            if (_isDisposed) return; // 容错

            long commandMemoryBytes = EstimateCommandMemory(command);
            long commandStorageBytes = EstimateCommandStorage(command);
            bool scheduleMaintenance;
            lock (_listLock) {
                _undoStack.AddLast(new CommandEntry(command, commandMemoryBytes, commandStorageBytes));
                _undoMemoryBytes += commandMemoryBytes;
                _undoStorageBytes += commandStorageBytes;
                ClearRedoStackLocked();
                TrimUndoCountLocked();
                if (_tryReduceCommandMemoryAsync == null)
                    TrimUndoResourceBudgetsLocked();
                scheduleMaintenance = _tryReduceCommandMemoryAsync != null &&
                    IsUndoResourceBudgetExceededLocked();

                // A command larger than the entire budget is intentionally not retained.
                // The document has still changed, so an empty history must not look saved.
                if (_undoStack.Count == 0)
                    _savedAtEmpty = false;
            }
            // Record 通常不包含执行逻辑，只是记录结果，不需要触发 Before/AfterExecute
            if (scheduleMaintenance) ScheduleBudgetMaintenance();
            CheckSavedStateChanged();
        }

        /// <summary>
        /// Waits until all budget maintenance requested before this call has completed.
        /// Primarily useful for orderly shutdown and deterministic tests.
        /// </summary>
        public Task WaitForPendingMaintenanceAsync() {
            lock (_maintenanceTaskLock) return _maintenanceTask;
        }

        public async Task<bool> UndoAsync() {
            if (_isDisposed) return false;
            await AwaitPendingMaintenanceSafeAsync().ConfigureAwait(true);
            if (_isDisposed) return false;

            // 获取执行锁：防止疯狂点击 Undo 导致并发执行
            await _executionLock.WaitAsync();
            try {
                IsUndoingOrRedoing = true;
                TCommand command;

                // 快速获取命令
                lock (_listLock) {
                    if (_undoStack.Count == 0) return false;
                    command = _undoStack.Last!.Value.Command;
                }

                // 触发事件（在 List 锁之外，但在执行锁之内）
                BeforeUndo?.Invoke(this, new CommandEventArgs(command));

                try {
                    // 执行真正的撤销（耗时操作，await 期间释放 CPU）
                    await command.UndoAsync();
                }
                catch (Exception) {
                    // 如果撤销失败，命令通常应该保留在 Undo 栈中，或者根据业务逻辑处理
                    // 这里选择直接抛出，不移动栈
                    throw;
                }

                // 成功后，移动栈（再次获取 List 锁）
                lock (_listLock) {
                    // 双重检查：理论上有了 _executionLock 不会变，但为了健壮性
                    if (_undoStack.Last != null &&
                        object.ReferenceEquals((object?)_undoStack.Last.Value.Command, (object?)command)) {
                        CommandEntry entry = _undoStack.Last.Value;
                        _undoStack.RemoveLast();
                        _redoStack.AddLast(entry);
                        _undoMemoryBytes -= entry.MemoryBytes;
                        _redoMemoryBytes += entry.MemoryBytes;
                        _undoStorageBytes -= entry.StorageBytes;
                        _redoStorageBytes += entry.StorageBytes;
                    }
                }

                AfterUndo?.Invoke(this, new CommandEventArgs(command));
                CheckSavedStateChanged();
                return true;
            }
            finally {
                IsUndoingOrRedoing = false;
                _executionLock.Release();
            }
        }

        public async Task<bool> RedoAsync() {
            if (_isDisposed) return false;
            await AwaitPendingMaintenanceSafeAsync().ConfigureAwait(true);
            if (_isDisposed) return false;

            await _executionLock.WaitAsync();
            try {
                IsUndoingOrRedoing = true;
                TCommand command;
                lock (_listLock) {
                    if (_redoStack.Count == 0) return false;
                    command = _redoStack.Last!.Value.Command;
                }

                BeforeRedo?.Invoke(this, new CommandEventArgs(command));

                try {
                    await command.ExecuteAsync();
                }
                catch {
                    throw;
                }

                lock (_listLock) {
                    if (_redoStack.Last != null &&
                        object.ReferenceEquals((object?)_redoStack.Last.Value.Command, (object?)command)) {
                        CommandEntry entry = _redoStack.Last.Value;
                        _redoStack.RemoveLast();
                        _undoStack.AddLast(entry);
                        _redoMemoryBytes -= entry.MemoryBytes;
                        _undoMemoryBytes += entry.MemoryBytes;
                        _redoStorageBytes -= entry.StorageBytes;
                        _undoStorageBytes += entry.StorageBytes;
                    }
                }

                AfterRedo?.Invoke(this, new CommandEventArgs(command));
                CheckSavedStateChanged();
                return true;
            }
            finally {
                IsUndoingOrRedoing = false;
                _executionLock.Release();
            }
        }

        public void Clear() {
            lock (_listLock) {
                ReleaseSavedCommandIfContainedLocked(_undoStack);
                ReleaseSavedCommandIfContainedLocked(_redoStack);
                ReleaseCommandsLocked(_undoStack);
                ReleaseCommandsLocked(_redoStack);
                _undoStack.Clear();
                _redoStack.Clear();
                _undoMemoryBytes = 0;
                _redoMemoryBytes = 0;
                _undoStorageBytes = 0;
                _redoStorageBytes = 0;
            }
            CheckSavedStateChanged();
        }

        private long EstimateCommandMemory(TCommand command) {
            long estimatedBytes = _commandSizeEstimator?.Invoke(command) ??
                (command is IMemoryAwareUndoableCommand memoryAware
                    ? memoryAware.EstimatedMemoryBytes
                    : DefaultCommandOverheadBytes);
            return Math.Max(0, estimatedBytes);
        }

        private long EstimateCommandStorage(TCommand command) =>
            Math.Max(0, _commandStorageEstimator?.Invoke(command) ?? 0);

        private void TrimUndoCountLocked() {
            while (_undoStack.Count > 0 &&
                _maxStackSize.HasValue &&
                _undoStack.Count > _maxStackSize.Value) {
                RemoveOldestUndoCommandLocked();
            }
        }

        private void TrimUndoResourceBudgetsLocked() {
            while (_undoStack.Count > 0 &&
                _maxMemoryBytes.HasValue &&
                _undoMemoryBytes > _maxMemoryBytes.Value) {
                if (!TryReduceOldestCommandMemoryLocked())
                    RemoveOldestUndoCommandLocked();
            }

            while (_undoStack.Count > 0 &&
                _maxStorageBytes.HasValue &&
                _undoStorageBytes > _maxStorageBytes.Value) {
                RemoveOldestStoredUndoCommandLocked();
            }
            if (_undoMemoryBytes < 0) _undoMemoryBytes = 0;
            if (_undoStorageBytes < 0) _undoStorageBytes = 0;
        }

        private bool IsUndoResourceBudgetExceededLocked() =>
            (_maxMemoryBytes.HasValue && _undoMemoryBytes > _maxMemoryBytes.Value) ||
            (_maxStorageBytes.HasValue && _undoStorageBytes > _maxStorageBytes.Value);

        private void ScheduleBudgetMaintenance() {
            lock (_maintenanceTaskLock) {
                Task previous = _maintenanceTask;
                _maintenanceTask = Task.Run(async () => {
                    try {
                        await previous.ConfigureAwait(false);
                    }
                    catch {
                        // A prior maintenance failure must not block later requests.
                    }

                    await EnforceUndoBudgetsAsync(_maintenanceCancellation.Token).ConfigureAwait(false);
                });
            }
        }

        private async Task AwaitPendingMaintenanceSafeAsync() {
            Task pending;
            lock (_maintenanceTaskLock) pending = _maintenanceTask;
            try {
                await pending.ConfigureAwait(true);
            }
            catch {
                // Undo/redo remains available if best-effort maintenance failed.
            }
        }

        private async Task EnforceUndoBudgetsAsync(CancellationToken cancellationToken) {
            if (_tryReduceCommandMemoryAsync == null || _isDisposed) return;

            try {
                await _executionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                return;
            }
            try {
                var failedEntries = new HashSet<CommandEntry>();
                while (true) {
                    cancellationToken.ThrowIfCancellationRequested();
                    CommandEntry? candidate = null;
                    lock (_listLock) {
                        if (!_maxMemoryBytes.HasValue ||
                            _undoMemoryBytes <= _maxMemoryBytes.Value ||
                            _undoStack.Count == 0)
                            break;

                        for (LinkedListNode<CommandEntry>? node = _undoStack.First;
                             node != null;
                             node = node.Next) {
                            if (!failedEntries.Contains(node.Value)) {
                                candidate = node.Value;
                                break;
                            }
                        }

                        if (candidate == null) {
                            RemoveOldestUndoCommandLocked();
                            failedEntries.Clear();
                            continue;
                        }
                    }

                    try {
                        await _tryReduceCommandMemoryAsync(
                            candidate.Command,
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                        throw;
                    }
                    catch {
                        // A failed spill is treated as a command that cannot reduce memory.
                    }

                    lock (_listLock) {
                        LinkedListNode<CommandEntry>? node = _undoStack.Find(candidate);
                        if (node == null) continue;

                        long previousMemoryBytes = candidate.MemoryBytes;
                        long previousStorageBytes = candidate.StorageBytes;
                        candidate.MemoryBytes = EstimateCommandMemory(candidate.Command);
                        candidate.StorageBytes = EstimateCommandStorage(candidate.Command);
                        _undoMemoryBytes += candidate.MemoryBytes - previousMemoryBytes;
                        _undoStorageBytes += candidate.StorageBytes - previousStorageBytes;

                        if (candidate.MemoryBytes < previousMemoryBytes)
                            failedEntries.Clear();
                        else
                            failedEntries.Add(candidate);
                    }
                }

                lock (_listLock) {
                    while (_undoStack.Count > 0 &&
                        _maxStorageBytes.HasValue &&
                        _undoStorageBytes > _maxStorageBytes.Value) {
                        RemoveOldestStoredUndoCommandLocked();
                    }
                    if (_undoMemoryBytes < 0) _undoMemoryBytes = 0;
                    if (_undoStorageBytes < 0) _undoStorageBytes = 0;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                return;
            }
            finally {
                _executionLock.Release();
            }

            if (!_isDisposed) CheckSavedStateChangedFromBackground();
        }

        private bool TryReduceOldestCommandMemoryLocked() {
            if (_tryReduceCommandMemory == null) return false;

            for (LinkedListNode<CommandEntry>? node = _undoStack.First;
                 node != null;
                 node = node.Next) {
                CommandEntry entry = node.Value;
                long previousMemoryBytes = entry.MemoryBytes;
                long previousStorageBytes = entry.StorageBytes;
                bool attempted;
                try {
                    attempted = _tryReduceCommandMemory(entry.Command);
                }
                catch {
                    attempted = false;
                }

                if (!attempted) continue;

                entry.MemoryBytes = EstimateCommandMemory(entry.Command);
                entry.StorageBytes = EstimateCommandStorage(entry.Command);
                _undoMemoryBytes += entry.MemoryBytes - previousMemoryBytes;
                _undoStorageBytes += entry.StorageBytes - previousStorageBytes;
                if (entry.MemoryBytes < previousMemoryBytes) return true;
            }

            return false;
        }

        private void RemoveOldestUndoCommandLocked() {
            RemoveUndoCommandLocked(_undoStack.First!);
        }

        private void RemoveOldestStoredUndoCommandLocked() {
            LinkedListNode<CommandEntry>? node = _undoStack.First;
            while (node != null && node.Value.StorageBytes == 0)
                node = node.Next;
            RemoveUndoCommandLocked(node ?? _undoStack.First!);
        }

        private void RemoveUndoCommandLocked(LinkedListNode<CommandEntry> node) {
            CommandEntry removed = node.Value;
            _undoStack.Remove(node);
            _undoMemoryBytes -= removed.MemoryBytes;
            _undoStorageBytes -= removed.StorageBytes;
            ReleaseSavedCommandIfMatchesLocked(removed.Command);
            ReleaseCommandLocked(removed.Command);
        }

        private void ClearRedoStackLocked() {
            ReleaseSavedCommandIfContainedLocked(_redoStack);
            ReleaseCommandsLocked(_redoStack);
            _redoStack.Clear();
            _redoMemoryBytes = 0;
            _redoStorageBytes = 0;
        }

        private void ReleaseCommandsLocked(LinkedList<CommandEntry> commands) {
            foreach (CommandEntry entry in commands)
                ReleaseCommandLocked(entry.Command);
        }

        private void ReleaseCommandLocked(TCommand command) {
            try {
                _commandReleased?.Invoke(command);
            }
            catch {
                // History cleanup must not break the caller when a best-effort
                // external storage cleanup fails.
            }
        }

        private void ReleaseSavedCommandIfContainedLocked(LinkedList<CommandEntry> commands) {
            if (_savedCommand == null) return;
            foreach (CommandEntry entry in commands) {
                if (object.ReferenceEquals((object?)entry.Command, (object?)_savedCommand)) {
                    _savedCommand = default;
                    _savedAtEmpty = false;
                    return;
                }
            }
        }

        private void ReleaseSavedCommandIfMatchesLocked(TCommand command) {
            if (object.ReferenceEquals((object?)command, (object?)_savedCommand)) {
                _savedCommand = default;
                _savedAtEmpty = false;
            }
        }

        public void Dispose() {
            if (_isDisposed) return;
            _isDisposed = true;
            _maintenanceCancellation.Cancel();

            Clear();
            GC.SuppressFinalize(this);
        }

        // 使用 object 作为轻量级锁，保护 _undoStack 和 _redoStack 的完整性
        private readonly object _listLock = new();
        // SemaphoreSlim 作为异步锁，确保 Undo/Redo 操作串行执行
        private readonly SemaphoreSlim _executionLock = new(1, 1);
        private readonly LinkedList<CommandEntry> _undoStack = new();
        private readonly LinkedList<CommandEntry> _redoStack = new();
        private readonly int? _maxStackSize;
        private readonly long? _maxMemoryBytes;
        private readonly Func<TCommand, long>? _commandSizeEstimator;
        private readonly long? _maxStorageBytes;
        private readonly Func<TCommand, long>? _commandStorageEstimator;
        private readonly Func<TCommand, bool>? _tryReduceCommandMemory;
        private readonly Func<TCommand, CancellationToken, Task<bool>>? _tryReduceCommandMemoryAsync;
        private readonly Action<TCommand>? _commandReleased;
        private readonly SynchronizationContext? _eventContext;
        private readonly object _savedStateEventLock = new();
        private readonly object _maintenanceTaskLock = new();
        private readonly CancellationTokenSource _maintenanceCancellation = new();
        private Task _maintenanceTask = Task.CompletedTask;
        private long _undoMemoryBytes;
        private long _redoMemoryBytes;
        private long _undoStorageBytes;
        private long _redoStorageBytes;
        private const long DefaultCommandOverheadBytes = 256;
        private bool _isDisposed;
        private TCommand? _savedCommand = default;
        private bool _savedAtEmpty = true;
        private bool _lastIsSaved = false;

        private sealed class CommandEntry {
            public TCommand Command { get; }
            public long MemoryBytes { get; set; }
            public long StorageBytes { get; set; }

            public CommandEntry(TCommand command, long memoryBytes, long storageBytes) {
                Command = command;
                MemoryBytes = memoryBytes;
                StorageBytes = storageBytes;
            }
        }
    }
}
