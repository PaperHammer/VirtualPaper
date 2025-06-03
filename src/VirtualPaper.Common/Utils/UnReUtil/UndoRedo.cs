namespace VirtualPaper.Common.Utils.UnReUtil {
    public class UndoRedo<T> {
        private readonly Stack<IUndoableCommand<T>> _undoStack = [];
        private readonly Stack<IUndoableCommand<T>> _redoStack = [];

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public async Task ExecuteAsync(T target, IUndoableCommand<T> command) {
            await command.ExecuteAsync(target);
            _undoStack.Push(command);
            _redoStack.Clear();
        }

        public async Task UndoAsync(T target) {
            if (!CanUndo) return;

            var cmd = _undoStack.Pop();
            await cmd.UndoAsync(target);
            _redoStack.Push(cmd);
        }

        public async Task RedoAsync(T target) {
            if (!CanRedo) return;

            var cmd = _redoStack.Pop();
            await cmd.ExecuteAsync(target);
            _undoStack.Push(cmd);
        }
    }
}
