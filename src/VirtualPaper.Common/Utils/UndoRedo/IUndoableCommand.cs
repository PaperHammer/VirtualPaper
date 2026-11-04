namespace VirtualPaper.Common.Utils.UndoRedo {
    public interface IUndoableCommand {
        Task ExecuteAsync();
        Task UndoAsync();
        string Description { get; }
    }

    /// <summary>
    /// 提供撤销命令所保留的托管内存估算值。
    /// 实现应统计命令独占的快照和压缩载荷，但不应统计共享的模型或渲染资源。
    /// </summary>
    public interface IMemoryAwareUndoableCommand {
        long EstimatedMemoryBytes { get; }
    }
}
