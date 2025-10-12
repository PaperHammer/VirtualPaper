namespace VirtualPaper.Common.Utils.UndoRedo {
    public sealed class UndoRedoUtil<TCommand> : IDisposable where TCommand : IUndoableCommand {
        public event EventHandler<CommandEventArgs>? BeforeExecute;
        public event EventHandler<CommandEventArgs>? AfterExecute;
        public event EventHandler<CommandEventArgs>? BeforeUndo;
        public event EventHandler<CommandEventArgs>? AfterUndo;
        public event EventHandler<CommandEventArgs>? BeforeRedo;
        public event EventHandler<CommandEventArgs>? AfterRedo;

        public class CommandEventArgs : EventArgs {
            public TCommand Command { get; }
            public CommandEventArgs(TCommand command) => Command = command;
        }

        public bool CanUndo {
            get {
                ThrowIfDisposed();
                try {
                    _lock.EnterReadLock();
                    return _undoStack.Count > 0;
                }
                finally {
                    _lock.ExitReadLock();
                }
            }
        }

        public bool CanRedo {
            get {
                ThrowIfDisposed();
                try {
                    _lock.EnterReadLock();
                    return _redoStack.Count > 0;
                }
                finally {
                    _lock.ExitReadLock();
                }
            }
        }

        public int UndoStackSize {
            get {
                ThrowIfDisposed();
                try {
                    _lock.EnterReadLock();
                    return _undoStack.Count;
                }
                finally {
                    _lock.ExitReadLock();
                }
            }
        }

        public int RedoStackSize {
            get {
                ThrowIfDisposed();
                try {
                    _lock.EnterReadLock();
                    return _redoStack.Count;
                }
                finally {
                    _lock.ExitReadLock();
                }
            }
        }

        /// <param name="maxStackSize">最大栈大小，null 表示无限制</param>
        public UndoRedoUtil(int? maxStackSize = null) {
            _maxStackSize = maxStackSize;
        }

        /// <summary>
        /// 记录新命令（自动清除重做栈）
        /// </summary>
        public void Record(TCommand command, CancellationToken cancellationToken = default) {
            ThrowIfDisposed();
            try {
                _lock.EnterWriteLock();

                _undoStack.AddLast(command);
                TrimUndoStackIfNeeded();

                _redoStack.Clear();
            }
            finally {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// 执行撤销
        /// </summary>
        public async Task<bool> UndoAsync() {
            ThrowIfDisposed();
            try {
                _lock.EnterWriteLock();
                if (_undoStack.Count == 0) return false;

                var command = _undoStack.Last!.Value;
                BeforeUndo?.Invoke(this, new CommandEventArgs(command));

                await command.UndoAsync();
                _undoStack.RemoveLast();
                _redoStack.AddLast(command);

                AfterUndo?.Invoke(this, new CommandEventArgs(command));
                return true;
            }
            finally {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// 执行重做
        /// </summary>
        public async Task<bool> RedoAsync() {
            ThrowIfDisposed();
            try {
                _lock.EnterWriteLock();
                if (_redoStack.Count == 0) return false;

                var command = _redoStack.Last!.Value;
                BeforeRedo?.Invoke(this, new CommandEventArgs(command));

                await command.ExecuteAsync();
                _redoStack.RemoveLast();
                _undoStack.AddLast(command);

                AfterRedo?.Invoke(this, new CommandEventArgs(command));
                return true;
            }
            finally {
                _lock.ExitWriteLock();
            }
        }

        public void Clear() {
            ThrowIfDisposed();
            try {
                _lock.EnterWriteLock();
                _undoStack.Clear();
                _redoStack.Clear();
            }
            finally {
                _lock.ExitWriteLock();
            }
        }

        public void Dispose() {
            if (_isDisposed) return;
            GC.SuppressFinalize(this);
            _isDisposed = true;

            Clear();
            _lock.Dispose();
        }

        #region utils
        private void TrimUndoStackIfNeeded() {
            if (_maxStackSize.HasValue && _undoStack.Count > _maxStackSize) {
                _undoStack.RemoveFirst();
            }
        }

        private void ThrowIfDisposed() {
            ObjectDisposedException.ThrowIf(_isDisposed, nameof(UndoRedoUtil<TCommand>));
        }
        #endregion

        private readonly LinkedList<TCommand> _undoStack = new();
        private readonly LinkedList<TCommand> _redoStack = new();
        private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);
        private readonly int? _maxStackSize;
        private bool _isDisposed;
    }
}
