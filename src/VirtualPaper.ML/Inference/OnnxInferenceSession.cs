using Microsoft.ML.OnnxRuntime;
using VirtualPaper.Common.Logging;

namespace VirtualPaper.ML.Inference;

/// <summary>CPU 保留原并发；GPU 会话串行使用，故障后整次调用仅重试一次。</summary>
internal sealed class OnnxInferenceSession : IDisposable {
    private readonly InferenceDevicePolicy _policy;
    private readonly Func<bool, InferenceSession> _create;
    private readonly Action<InferenceState> _report;
    private readonly bool _serialize;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private InferenceSession? _session;
    private bool _isGpu;
    private bool _disposed;

    internal OnnxInferenceSession(string modelPath, Func<SessionOptions> cpuOptions,
        InferenceDevicePolicy policy, Action<InferenceState> report, bool supportsGpu = true)
        : this(policy, gpu => {
            using var options = cpuOptions();
            if (gpu) {
                options.EnableMemoryPattern = false;
                options.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
                options.AppendExecutionProvider_DML(0);
            }
            return new InferenceSession(modelPath, options);
        }, report, supportsGpu) { }

    // 测试可注入真实小型 CPU 会话及受控 GPU 创建失败，不依赖测试机拥有 GPU。
    internal OnnxInferenceSession(InferenceDevicePolicy policy, Func<bool, InferenceSession> create,
        Action<InferenceState> report, bool supportsGpu = true) {
        _policy = policy;
        _create = create;
        _report = report;
        var selection = policy.SelectDevice(supportsGpu);
        // 初始选择 CPU 的会话保留原并发；GPU 故障回退仍与执行互斥。
        _serialize = selection.Device == InferenceDevice.DirectML || selection.IsCpuFallback;
        if (selection.Device == InferenceDevice.DirectML) {
            InferenceSession? accelerated = null;
            try { accelerated = create(true); }
            catch (Exception ex) when (GpuInferenceFailure.IsDeviceFailure(ex)) {
                policy.DisableGpu();
                selection = policy.SelectDevice(supportsGpu);
                ArcLog.GetLogger<OnnxInferenceSession>().Warn($"GPU initialization failed; using CPU: {ex.Message}");
            }
            if (accelerated != null) { SetSession(accelerated, selection); return; }
        }
        SetSession(create(false), selection);
    }

    internal InferenceSession Native => _session ?? throw new ObjectDisposedException(nameof(OnnxInferenceSession));
    internal bool IsGpu => _isGpu;
    internal IReadOnlyDictionary<string, NodeMetadata> InputMetadata => Native.InputMetadata;
    internal IReadOnlyDictionary<string, NodeMetadata> OutputMetadata => Native.OutputMetadata;
    internal ModelMetadata ModelMetadata => Native.ModelMetadata;

    internal IDisposableReadOnlyCollection<DisposableNamedOnnxValue> Run(
        IReadOnlyCollection<NamedOnnxValue> inputs, IReadOnlyCollection<string> outputs, RunOptions options) =>
        Native.Run(inputs, outputs, options);

    internal T Execute<T>(Func<T> operation, CancellationToken ct) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ct.ThrowIfCancellationRequested();
        if (!_serialize) return operation();
        _gate.Wait(ct);
        try {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ct.ThrowIfCancellationRequested();
            try { return operation(); }
            catch (Exception ex) when (_isGpu && !ct.IsCancellationRequested && GpuInferenceFailure.IsExecutionFailure(ex)) {
                ct.ThrowIfCancellationRequested();
                _policy.DisableGpu();
                ArcLog.GetLogger<OnnxInferenceSession>().Warn($"GPU execution failed; retrying this operation once on CPU: {ex.Message}");
                _session?.Dispose();
                _session = null;
                SetSession(_create(false), _policy.SelectDevice());
                ct.ThrowIfCancellationRequested();
                return operation();
            }
        }
        finally { _gate.Release(); }
    }

    private void SetSession(InferenceSession session, InferenceState state) {
        _session = session;
        _isGpu = state.Device == InferenceDevice.DirectML;
        ArcLog.GetLogger<OnnxInferenceSession>().Info($"Inference backend: {state.Device}; selection: {state.Reason}");
        try { _report(state); }
        catch { session.Dispose(); _session = null; throw; }
    }

    public void Dispose() {
        // 加速会话的释放与执行互斥，不能在 DirectML.Run 期间释放原生资源。
        if (_serialize) _gate.Wait();
        try {
            if (_disposed) return;
            _disposed = true;
            _session?.Dispose();
            _session = null;
        }
        finally { if (_serialize) _gate.Release(); }
    }
}
