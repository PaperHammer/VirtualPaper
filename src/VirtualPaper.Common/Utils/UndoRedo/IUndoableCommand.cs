namespace VirtualPaper.Common.Utils.UndoRedo {
    public interface IUndoableCommand {
        Task ExecuteAsync();
        Task UndoAsync();
        string Description { get; }
    }
}
