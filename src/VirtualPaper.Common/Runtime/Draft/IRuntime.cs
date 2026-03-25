using VirtualPaper.Common.Utils.UndoRedo.Events;

namespace VirtualPaper.Common.Runtime.Draft {
    public interface IRuntime {
        event EventHandler<IsSavedChangedEventArgs>? IsSavedChanged;
        Task<bool> SaveAsync();
        Task UndoAsync();
        Task RedoAsync();
    }
}
