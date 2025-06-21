using System;
using System.Threading.Tasks;
using VirtualPaper.Common.Utils.UnReUtil;

namespace Workloads.Creation.StaticImg.Models.ToolItems.Utils {
    public sealed partial class SI_UndoRedoUtil : IDisposable {
        public bool CanUndo => _undoRedoCore.CanUndo;
        public bool CanRedo => _undoRedoCore.CanRedo;

        public SI_UndoRedoUtil() {
            _undoRedoCore = new UndoRedoSnapshotUtil();
        }

        /// <summary>
        /// 记录同步操作
        /// </summary>
        /// <param name="execute">恢复</param>
        /// <param name="undo">撤销</param>
        /// <param name="opType">操作类型</param>
        public void RecordCommand(Action execute, Action undo, SI_UndoRedo_OP_Type opType) 
            => _undoRedoCore.RecordCommand(execute, undo, (int)opType);
        /// <summary>
        /// 记录异步操作
        /// </summary>
        /// <param name="execute">恢复</param>
        /// <param name="undo">撤销</param>
        /// <param name="opType">操作类型</param>
        public void RecordCommand(Func<Task> execute, Func<Task> undo, SI_UndoRedo_OP_Type opType) 
            => _undoRedoCore.RecordCommand(execute, undo, (int)opType);        
        public async Task UndoAsync() => await _undoRedoCore.TryUndoAsync();
        public async Task RedoAsync() => await _undoRedoCore.TryRedoAsync();
        public void Dispose() => _undoRedoCore.Dispose();

        private readonly UndoRedoSnapshotUtil _undoRedoCore;        
    }

    public enum SI_UndoRedo_OP_Type {
        /// <summary> 
        /// 影响图层特定区域的操作（如绘制、擦除） 
        /// </summary>
        Region,

        /// <summary> 
        /// 针对可序列化表示对象的操作（如图层集合的添加、删除、移动图层）
        /// </summary>
        Serializable
    }
}
