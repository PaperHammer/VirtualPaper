using System;
using System.Threading.Tasks;
using VirtualPaper.Common.Utils.UndoRedo;
using VirtualPaper.Common.Utils.UndoRedo.Events;

namespace Workloads.Creation.StaticImg.InkSystem.Utils {
    public sealed partial class StaticImgUndoRedoUtil : IDisposable {
        public event EventHandler<IsSavedChangedEventArgs>? IsSavedChanged;

        public bool CanUndo => _undoRedoCore.CanUndo;
        public bool CanRedo => _undoRedoCore.CanRedo;
        public bool IsUndoingOrRedoing => _undoRedoCore.IsUndoingOrRedoing;

        public StaticImgUndoRedoUtil(int maxStackSize = 20) {
            _undoRedoCore = new UndoRedoUtil<IUndoableCommand>(maxStackSize);
            _undoRedoCore.IsSavedChanged += UndoRedoCore_IsSavedChanged;
        }

        private void UndoRedoCore_IsSavedChanged(object? sender, IsSavedChangedEventArgs e) {
            IsSavedChanged?.Invoke(this, e);
        }

        public void RecordCommand(IUndoableCommand command) {
            _undoRedoCore.Record(command);
        }

        public async Task UndoAsync() => await _undoRedoCore.UndoAsync();
        public async Task RedoAsync() => await _undoRedoCore.RedoAsync();
        public void MarkAsSaved() => _undoRedoCore.MarkAsSaved();

        public void Dispose() {
            _undoRedoCore.Dispose();
            GC.SuppressFinalize(this);
        }
        
        private readonly UndoRedoUtil<IUndoableCommand> _undoRedoCore;
    }

    class ActionCommand : IUndoableCommand {
        private readonly Action _execute;
        private readonly Action _undo;
        public string Description { get; }

        public ActionCommand(Action execute, Action undo, string description) {
            _execute = execute;
            _undo = undo;
            Description = description;
        }

        public Task ExecuteAsync() {
            _execute();
            return Task.CompletedTask;
        }

        public Task UndoAsync() {
            _undo();
            return Task.CompletedTask;
        }
    }

    class AsyncCommand : IUndoableCommand {
        private readonly Func<Task> _execute;
        private readonly Func<Task> _undo;
        public string Description { get; }

        public AsyncCommand(Func<Task> execute, Func<Task> undo, string description) {
            _execute = execute;
            _undo = undo;
            Description = description;
        }

        public Task ExecuteAsync() => _execute();
        public Task UndoAsync() => _undo();
    }
}
