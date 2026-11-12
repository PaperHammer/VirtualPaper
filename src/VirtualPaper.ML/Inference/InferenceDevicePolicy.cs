namespace VirtualPaper.ML.Inference;

/// <summary>每种模型独立保存本次 UI 会话的 GPU 可用性；不会修改用户保存的偏好。</summary>
public sealed class InferenceDevicePolicy(bool allowGpu = false) {
    public bool AllowGpu { get; } = allowGpu;
    internal bool CanUseGpu => AllowGpu && Volatile.Read(ref _gpuDisabled) == 0;
    internal void DisableGpu() => Interlocked.Exchange(ref _gpuDisabled, 1);
    private int _gpuDisabled;
}

/// <summary>深度模型实例虽短暂，失败记录仍按模型类型保留到本次 UI 退出。</summary>
public sealed class InferenceDevicePolicies(bool allowGpu) {
    public InferenceDevicePolicy SuperResolution { get; } = new(allowGpu);
    public InferenceDevicePolicy StyleTransfer { get; } = new(allowGpu);
    public InferenceDevicePolicy DepthEstimate { get; } = new(allowGpu);
}
