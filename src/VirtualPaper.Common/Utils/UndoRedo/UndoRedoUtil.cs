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

        public UndoRedoUtil(bool isSaved, int? maxStackSize = null) {
            _maxStackSize = maxStackSize;
            _lastIsSaved = isSaved;
        }

        // 属性访问只需保护内存读取，使用 lock 即可
        public bool CanUndo {
            get {
                lock (_listLock) return _undoStack.Count > 0;
            }
        }

        public bool CanRedo {
            get {
                lock (_listLock) return _redoStack.Count > 0;
            }
        }

        public int UndoStackSize {
            get {
                lock (_listLock) return _undoStack.Count;
            }
        }

        public int RedoStackSize {
            get {
                lock (_listLock) return _redoStack.Count;
            }
        }

        private bool _isUndoingOrRedoing;
        public bool IsUndoingOrRedoing {
            get {
                lock (_listLock) return _isUndoingOrRedoing;
            }
            private set { lock (_listLock) _isUndoingOrRedoing = value; }
        }

        public bool IsSaved {
            get {
                lock (_listLock) {
                    // 栈为空的情况：如果保存的时候就是空的，现在也是空的，说明【已保存】(返回 true)
                    if (_undoStack.Count == 0) return _savedAtEmpty;

                    // 栈不为空的情况：当前栈顶的对象 和 记录的保存对象 是同一个内存地址，说明【已保存】(返回 true)
                    return object.ReferenceEquals((object?)_undoStack.Last!.Value, (object?)_savedCommand);
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
                    _savedCommand = _undoStack.Last!.Value;
                    _savedAtEmpty = false;
                }
            }
            CheckSavedStateChanged();
        }

        private void CheckSavedStateChanged() {
            bool currentIsSaved = IsSaved;
            if (currentIsSaved != _lastIsSaved) {
                _lastIsSaved = currentIsSaved;
                IsSavedChanged?.Invoke(this, new IsSavedChangedEventArgs(currentIsSaved));
            }
        }

        public void Record(TCommand command) {
            if (_isDisposed) return; // 容错

            lock (_listLock) {
                _undoStack.AddLast(command);
                if (_maxStackSize.HasValue && _undoStack.Count > _maxStackSize) {
                    _undoStack.RemoveFirst();
                }
                _redoStack.Clear();
            }
            // Record 通常不包含执行逻辑，只是记录结果，不需要触发 Before/AfterExecute
            CheckSavedStateChanged();
        }

        public async Task<bool> UndoAsync() {
            if (_isDisposed) return false;

            // 获取执行锁：防止疯狂点击 Undo 导致并发执行
            await _executionLock.WaitAsync();
            try {
                IsUndoingOrRedoing = true;
                TCommand command;

                // 快速获取命令
                lock (_listLock) {
                    if (_undoStack.Count == 0) return false;
                    command = _undoStack.Last!.Value;
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
                    if (_undoStack.Last != null && object.ReferenceEquals((object?)_undoStack.Last.Value, (object?)command)) {
                        _undoStack.RemoveLast();
                        _redoStack.AddLast(command);
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

            await _executionLock.WaitAsync();
            try {
                IsUndoingOrRedoing = true;
                TCommand command;
                lock (_listLock) {
                    if (_redoStack.Count == 0) return false;
                    command = _redoStack.Last!.Value;
                }

                BeforeRedo?.Invoke(this, new CommandEventArgs(command));

                try {
                    await command.ExecuteAsync();
                }
                catch {
                    throw;
                }

                lock (_listLock) {
                    if (_redoStack.Last != null && object.ReferenceEquals((object?)_redoStack.Last.Value, (object?)command)) {
                        _redoStack.RemoveLast();
                        _undoStack.AddLast(command);
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
                _undoStack.Clear();
                _redoStack.Clear();
            }
            CheckSavedStateChanged();
        }

        public void Dispose() {
            if (_isDisposed) return;
            _isDisposed = true;

            Clear();
            _executionLock.Dispose();
            GC.SuppressFinalize(this);
        }

        // 使用 object 作为轻量级锁，保护 _undoStack 和 _redoStack 的完整性
        private readonly object _listLock = new();
        // SemaphoreSlim 作为异步锁，确保 Undo/Redo 操作串行执行
        private readonly SemaphoreSlim _executionLock = new(1, 1);
        private readonly LinkedList<TCommand> _undoStack = new();
        private readonly LinkedList<TCommand> _redoStack = new();
        private readonly int? _maxStackSize;
        private bool _isDisposed;
        private TCommand? _savedCommand = default;
        private bool _savedAtEmpty = true;
        private bool _lastIsSaved = false;
    }
}