namespace VirtualPaper.Common.Utils.UnReUtil {
    public sealed class UndoRedoSnapshotUtil : IDisposable {
        public event EventHandler? OnPreviewUndo;
        public event EventHandler? OnUndoDone;
        public event EventHandler? OnPreviewRedo;
        public event EventHandler? OnRedoDone;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// 记录同步操作
        /// </summary>
        /// <param name="execute">恢复</param>
        /// <param name="undo">撤销</param>
        /// <param name="opType">操作类型</param>
        public void RecordCommand(Action execute, Action undo, int opType)
            => RecordCommand(
                execute: () => { execute(); return Task.CompletedTask; },
                undo: () => { undo(); return Task.CompletedTask; },
                opType
            );

        /// <summary>
        /// 记录异步操作
        /// </summary>
        /// <param name="execute">恢复</param>
        /// <param name="undo">撤销</param>
        /// <param name="opType">操作类型</param>
        public void RecordCommand(Func<Task> execute, Func<Task> undo, int opType) {
            try {
                _rwSlim.EnterWriteLock();
                _redoStack.Clear(); // 清除 redo 栈
                _undoStack.Push(new Command(execute, undo, opType));
            }
            finally { _rwSlim.ExitWriteLock(); }
        }

        /// <summary> 
        /// 撤销
        /// </summary>
        public async Task<bool> TryUndoAsync() {
            try {
                _rwSlim.EnterWriteLock();
                if (_undoStack.Count == 0) return false;

                OnPreviewUndo?.Invoke(this, EventArgs.Empty);
                var command = _undoStack.Pop();
                await command.Undo();
                _redoStack.Push(command);
                OnUndoDone?.Invoke(this, EventArgs.Empty);

                return true;
            }
            finally { _rwSlim.ExitWriteLock(); }
        }

        /// <summary> 
        /// 重做
        /// </summary>
        public async Task<bool> TryRedoAsync() {
            try {
                _rwSlim.EnterWriteLock();
                if (_redoStack.Count == 0) return false;

                OnPreviewRedo?.Invoke(this, EventArgs.Empty);
                var command = _redoStack.Pop();
                await command.Execute();
                _undoStack.Push(command);
                OnRedoDone?.Invoke(this, EventArgs.Empty);

                return true;
            }
            finally { _rwSlim.ExitWriteLock(); }
        }

        public void Dispose() {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        private record Command(Func<Task> Execute, Func<Task> Undo, int OpType);

        private readonly Stack<Command> _undoStack = new();
        private readonly Stack<Command> _redoStack = new();
        // 轻量级读写锁（比lock更好）&& 让 _currentState 也能保证线程安全
        private readonly ReaderWriterLockSlim _rwSlim = new(LockRecursionPolicy.SupportsRecursion);
    }
}
