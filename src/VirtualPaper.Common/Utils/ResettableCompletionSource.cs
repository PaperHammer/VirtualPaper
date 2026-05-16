namespace VirtualPaper.Common.Utils {
    public class ResettableCompletionSource<T> {
        public Task<T> Task => _tcs.Task;

        public ResettableCompletionSource() {
            Reset();
        }

        /// <summary>
        /// 仅当上一个 Task 已完成时才重置，避免重复创建对象。
        /// </summary>
        /// <returns>true 表示已重置；false 表示当前 Task 尚未完成，无需重置。</returns>
        public bool Reset() {
            if (!_tcs.Task.IsCompleted) {
                return false;
            }

            _tcs = new TaskCompletionSource<T>();
            return true;
        }

        /// <summary>
        /// 强制重置，无论当前状态。会尝试取消旧的 Task。
        /// </summary>
        public void ForceReset() {
            _tcs.TrySetCanceled(); // 先让旧的完成，避免 await 永久挂起
            _tcs = new TaskCompletionSource<T>();
        }

        public bool TrySetResult(T result) => _tcs.TrySetResult(result);
        public bool TrySetException(Exception ex) => _tcs.TrySetException(ex);
        public bool TrySetCanceled() => _tcs.TrySetCanceled();

        private TaskCompletionSource<T> _tcs = new();
    }
}
