namespace VirtualPaper.ML.Inference;

public interface IInferenceModelProvider<T> where T : class, IDisposable {
    Task<InferenceModelLease<T>> AcquireAsync(bool useGpu, CancellationToken token = default);
}

/// <summary>同设备的任务复用一份模型；切换设备前等待已有任务结束，不同时驻留 CPU/GPU 两套会话。</summary>
public sealed class InferenceModelPool<T>(InferenceDevicePolicy policy, Func<InferenceDevicePolicy, T> create)
    : IInferenceModelProvider<T>, IDisposable where T : class, IDisposable {
    public Task<InferenceModelLease<T>> AcquireAsync(bool useGpu, CancellationToken token = default) =>
        // 模型切换可能释放原生资源，始终放在后台，不占用调用方的 UI 线程。
        Task.Run(() => AcquireCoreAsync(policy.HardwareAccelerationEnabled && useGpu, token), token);

    private async Task<InferenceModelLease<T>> AcquireCoreAsync(bool useGpu, CancellationToken token) {
        while (true) {
            token.ThrowIfCancellationRequested();
            Task wait;
            lock (_lock) {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_active == 0 || useGpu == _useGpu) {
                    if (_model == null || useGpu != _useGpu) {
                        var previous = _model;
                        _model = null;
                        previous?.Dispose();
                        token.ThrowIfCancellationRequested();
                        _model = create(policy.WithPreference(useGpu
                            ? InferenceDevicePreference.GpuPreferred : InferenceDevicePreference.CpuPreferred));
                        _useGpu = useGpu;
                    }
                    _active++;
                    return new(_model, Release);
                }
                wait = _available.Task;
            }
            await wait.WaitAsync(token).ConfigureAwait(false);
        }
    }

    private void Release() {
        T? dispose = null;
        lock (_lock) {
            if (--_active == 0) {
                if (_disposed) { dispose = _model; _model = null; }
                SignalAvailable();
            }
        }
        dispose?.Dispose();
    }

    private void SignalAvailable() {
        _available.TrySetResult();
        _available = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public void Dispose() {
        T? dispose = null;
        lock (_lock) {
            if (_disposed) return;
            _disposed = true;
            if (_active == 0) { dispose = _model; _model = null; }
            SignalAvailable();
        }
        // 使用中的模型由最后一个任务释放；等待任务会被唤醒并停止获取。
        dispose?.Dispose();
    }

    private readonly object _lock = new();
    private TaskCompletionSource _available = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private T? _model;
    private bool _useGpu, _disposed;
    private int _active;
}

public sealed class InferenceModelLease<T>(T model, Action release) : IDisposable where T : class, IDisposable {
    public T Model { get; } = model;
    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    private Action? _release = release;
}
