using System;
using System.Threading;
using System.Threading.Tasks;
using VirtualPaper.Common.Utils.UndoRedo;
using VirtualPaper.Common.Utils.UndoRedo.Events;
using Workloads.Creation.StaticImg.Core.UndoRedoCommand;

namespace Workloads.Creation.StaticImg.InkSystem.Utils {
    public sealed partial class StaticImgUndoRedoUtil : IDisposable {
        public event EventHandler<IsSavedChangedEventArgs>? IsSavedChanged;

        public bool CanUndo => _undoRedoCore.CanUndo;
        public bool CanRedo => _undoRedoCore.CanRedo;
        public bool IsUndoingOrRedoing => _undoRedoCore.IsUndoingOrRedoing;
        public long UndoMemoryBytes => _undoRedoCore.UndoMemoryBytes;
        public long RedoMemoryBytes => _undoRedoCore.RedoMemoryBytes;
        public long TotalMemoryBytes => _undoRedoCore.TotalMemoryBytes;
        public long UndoDiskBytes => _undoRedoCore.UndoStorageBytes;
        public long RedoDiskBytes => _undoRedoCore.RedoStorageBytes;
        public long TotalDiskBytes => _undoRedoCore.TotalStorageBytes;

        public StaticImgUndoRedoUtil(
            bool isSaved,
            int maxStackSize = DefaultMaxStackSize,
            long maxMemoryBytes = DefaultMaxMemoryBytes,
            long maxDiskBytes = DefaultMaxDiskBytes,
            string? sessionId = null,
            string? diskRootDirectory = null) {
            _diskStore = new UndoDiskStore(
                sessionId ?? Guid.NewGuid().ToString("N"),
                diskRootDirectory);
            _undoRedoCore = new UndoRedoUtil<IUndoableCommand>(
                isSaved,
                maxStackSize,
                maxMemoryBytes,
                maxStorageBytes: maxDiskBytes,
                commandStorageEstimator: EstimateCommandDiskStorage,
                tryReduceCommandMemoryAsync: TrySpillCommandToDiskAsync,
                commandReleased: ReleaseCommand);
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
        public Task WaitForPendingMaintenanceAsync() =>
            _undoRedoCore.WaitForPendingMaintenanceAsync();

        public void Dispose() {
            if (_isDisposed) return;
            _isDisposed = true;
            _undoRedoCore.Dispose();
            _diskStore.Dispose();
            GC.SuppressFinalize(this);
        }

        private Task<bool> TrySpillCommandToDiskAsync(
            IUndoableCommand command,
            CancellationToken cancellationToken) {
            if (_isDisposed || command is not IDiskSpillableUndoCommand spillable)
                return Task.FromResult(false);
            return spillable.TrySpillToDiskAsync(_diskStore, cancellationToken);
        }

        private static long EstimateCommandDiskStorage(IUndoableCommand command) =>
            command is IDiskSpillableUndoCommand spillable
                ? spillable.DiskStorageBytes
                : 0;

        private static void ReleaseCommand(IUndoableCommand command) {
            if (command is IDisposable disposable) disposable.Dispose();
        }
        
        private readonly UndoRedoUtil<IUndoableCommand> _undoRedoCore;
        private readonly UndoDiskStore _diskStore;
        private bool _isDisposed;
        public const int DefaultMaxStackSize = 100;
        public const long DefaultMaxMemoryBytes = 256L * 1024 * 1024;
        public const long DefaultMaxDiskBytes = 2L * 1024 * 1024 * 1024;
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
