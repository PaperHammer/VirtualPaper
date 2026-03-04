using System;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using VirtualPaper.Common.Utils.UndoRedo;
using Windows.Foundation;

namespace Workloads.Creation.StaticImg.InkSystem.Utils {
    public sealed partial class StaticImgUndoRedoUtil : IDisposable {
        private readonly UndoRedoUtil<IUndoableCommand> _undoRedoCore;

        public bool CanUndo => _undoRedoCore.CanUndo;
        public bool CanRedo => _undoRedoCore.CanRedo;
        public bool IsUndoingOrRedoing => _undoRedoCore.IsUndoingOrRedoing;

        public StaticImgUndoRedoUtil(int maxStackSize = 20) {
            _undoRedoCore = new UndoRedoUtil<IUndoableCommand>(maxStackSize);
        }

        public void RecordCommand(IUndoableCommand command) {
            _undoRedoCore.Record(command);
        }

        public async Task UndoAsync() => await _undoRedoCore.UndoAsync();
        public async Task RedoAsync() => await _undoRedoCore.RedoAsync();

        public void Dispose() {
            _undoRedoCore.Dispose();
            GC.SuppressFinalize(this);
        }
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
