namespace VirtualPaper.Common.Utils {
    public class OperationTracker {
        private readonly object _lock = new();
        private int _pendingOperations;
        private TaskCompletionSource<bool>? _completionSource;

        public Task WaitForCompletionAsync() {
            lock (_lock) {
                if (_pendingOperations == 0)
                    return Task.CompletedTask;

                _completionSource ??= new TaskCompletionSource<bool>();
                return _completionSource.Task;
            }
        }

        public IDisposable BeginOperation() {
            lock (_lock) {
                _pendingOperations++;
                _completionSource?.TrySetCanceled();
                _completionSource = null;

                return new OperationScope(this);
            }
        }

        private void EndOperation() {
            lock (_lock) {
                if (--_pendingOperations == 0) {
                    _completionSource?.TrySetResult(true);
                    _completionSource = null;
                }
            }
        }

        private class OperationScope : IDisposable {
            private readonly OperationTracker _tracker;
            public OperationScope(OperationTracker tracker) => _tracker = tracker;
            public void Dispose() => _tracker.EndOperation();
        }
    }
}
