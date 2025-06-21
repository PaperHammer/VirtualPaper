namespace VirtualPaper.Common.Runtime.Draft {
    public interface IRuntime {
        Task SaveAsync();
        Task UndoAsync();
        Task RedoAsync();
    }
}
