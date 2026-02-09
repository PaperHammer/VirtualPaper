namespace VirtualPaper.Common.Utils.TaskUtils {
    public class TaskBlocking {
        public IDisposable Block() {
            CancellationTokenRegistration reg = default;

            lock (_lockObj) {
                _registrations.Add(reg);

                if (_registrations.Count == 1)
                    _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            return new Unsubscriber(() => {
                lock (_lockObj) {
                    if (_registrations.Remove(reg) && _registrations.Count == 0)
                        _tcs.TrySetResult();
                }
                reg.Dispose();
            });
        }

        public Task WaitAsync() {
            lock (_lockObj) {
                if (_registrations.Count == 0)
                    return Task.CompletedTask;

                return _tcs.Task;
            }
        }

        private readonly object _lockObj = new();
        private readonly HashSet<CancellationTokenRegistration> _registrations = [];
        private TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public sealed class Unsubscriber : IDisposable {
        private readonly Action _dispose;
        public Unsubscriber(Action dispose) => _dispose = dispose;
        public void Dispose() => _dispose();
    }
}

