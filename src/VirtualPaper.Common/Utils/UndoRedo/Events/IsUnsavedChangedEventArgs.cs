namespace VirtualPaper.Common.Utils.UndoRedo.Events {
    public class IsSavedChangedEventArgs : EventArgs {
        /// <summary>
        /// true 表示已保存(与上次保存时状态一致)，false 表示未保存(存在尚未保存的修改)
        /// </summary>
        public bool IsSaved { get; }

        public IsSavedChangedEventArgs(bool isSaved) => IsSaved = isSaved;
    }
}
