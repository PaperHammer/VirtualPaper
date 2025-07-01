using System.Diagnostics;

namespace VirtualPaper.Common.Utils.UnReUtil {
    public sealed class UndoRedoSnapshotUtil : IDisposable {
        public event EventHandler? OnPreviewUndo;
        public event EventHandler? OnUndoDone;
        public event EventHandler? OnPreviewRedo;
        public event EventHandler? OnRedoDone;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        /// <param name="maxStackSize">最大栈大小，0表示无限制</param>
        public UndoRedoSnapshotUtil(int maxStackSize = 0) {
            _maxStackSize = maxStackSize;
        }

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

                // 应用LRU淘汰策略
                if (_maxStackSize > 0 && _undoStack.Count >= _maxStackSize) {
                    _undoStack.RemoveFirst();                    
                }

                _undoStack.AddLast(new Command(execute, undo, opType));
                Debug.WriteLine($"undoStack size: {_undoStack.Count}");
                Debug.WriteLine($"redoStack size: {_redoStack.Count}");
            }
            finally {
                _rwSlim.ExitWriteLock();
            }
        }

        /// <summary> 
        /// 撤销
        /// </summary>
        public async Task<bool> TryUndoAsync() {
            try {
                _rwSlim.EnterWriteLock();
                if (_undoStack.Count == 0) return false;

                OnPreviewUndo?.Invoke(this, EventArgs.Empty);
                var command = _undoStack.Last();
                await command.Undo();
                _undoStack.RemoveLast();
                _redoStack.AddLast(command);
                OnUndoDone?.Invoke(this, EventArgs.Empty);

                Debug.WriteLine($"undoStack size: {_undoStack.Count}");
                Debug.WriteLine($"redoStack size: {_redoStack.Count}");

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
                var command = _redoStack.Last();
                await command.Execute();
                _redoStack.RemoveLast();
                _undoStack.AddLast(command);
                OnRedoDone?.Invoke(this, EventArgs.Empty);

                Debug.WriteLine($"undoStack size: {_undoStack.Count}");
                Debug.WriteLine($"redoStack size: {_redoStack.Count}");

                return true;
            }
            finally { _rwSlim.ExitWriteLock(); }
        }

        public void Dispose() {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        private record Command(Func<Task> Execute, Func<Task> Undo, int OpType);

        private readonly LinkedList<Command> _undoStack = new();
        private readonly LinkedList<Command> _redoStack = new();
        // 轻量级读写锁（比lock更好）&& 让 _currentState 也能保证线程安全
        private readonly ReaderWriterLockSlim _rwSlim = new(LockRecursionPolicy.SupportsRecursion);
        private readonly int _maxStackSize;
    }
}
