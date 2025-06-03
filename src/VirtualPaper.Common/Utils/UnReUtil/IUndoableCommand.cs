namespace VirtualPaper.Common.Utils.UnReUtil {
    /*
     * StaticImg：Modify、LayerChange
     */
    public interface IUndoableCommand<T> {
        Task ExecuteAsync(T target);
        Task UndoAsync(T target);
        //string Description { get; }
    }
}
