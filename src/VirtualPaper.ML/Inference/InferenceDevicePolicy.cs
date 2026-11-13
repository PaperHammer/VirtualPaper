namespace VirtualPaper.ML.Inference;

public enum InferenceDevicePreference { CpuPreferred, GpuPreferred }

/// <summary>每种模型独立保存本次 UI 会话的 GPU 可用性；不会修改用户保存的偏好。</summary>
public sealed class InferenceDevicePolicy {
    public InferenceDevicePolicy(bool hardwareAccelerationEnabled = false,
        InferenceDevicePreference preference = InferenceDevicePreference.GpuPreferred)
        : this(hardwareAccelerationEnabled, preference, new GpuAvailability()) { }

    private InferenceDevicePolicy(bool enabled, InferenceDevicePreference preference, GpuAvailability availability) {
        HardwareAccelerationEnabled = enabled;
        Preference = preference;
        _availability = availability;
    }

    public bool HardwareAccelerationEnabled { get; }
    public InferenceDevicePreference Preference { get; }
    internal bool CanUseGpu => HardwareAccelerationEnabled && Preference == InferenceDevicePreference.GpuPreferred
        && Volatile.Read(ref _availability.Disabled) == 0;
    internal void DisableGpu() => Interlocked.Exchange(ref _availability.Disabled, 1);

    // 每个任务固定自己的偏好，但同一模型的 GPU 故障记录仍在本次 UI 会话内共享。
    public InferenceDevicePolicy WithPreference(InferenceDevicePreference preference) =>
        preference == Preference ? this : new(HardwareAccelerationEnabled, preference, _availability);

    internal InferenceState SelectDevice(bool supportsGpu = true) {
        InferenceSelectionReason reason = !HardwareAccelerationEnabled ? InferenceSelectionReason.HardwareAccelerationDisabled
            : !supportsGpu ? InferenceSelectionReason.ModelUnsupported
            : Preference == InferenceDevicePreference.CpuPreferred ? InferenceSelectionReason.CpuPreferred
            : Volatile.Read(ref _availability.Disabled) != 0 ? InferenceSelectionReason.GpuFailed
            : InferenceSelectionReason.GpuPreferred;
        return new(HardwareAccelerationEnabled,
            reason == InferenceSelectionReason.GpuPreferred ? InferenceDevice.DirectML : InferenceDevice.Cpu, reason);
    }

    private readonly GpuAvailability _availability;
    private sealed class GpuAvailability { internal int Disabled; }
}

/// <summary>深度模型实例虽短暂，失败记录仍按模型类型保留到本次 UI 退出。</summary>
public sealed class InferenceDevicePolicies(bool hardwareAccelerationEnabled) {
    // 超分的最小输入块也有 512×512，计算较重；其余两条当前流程优先避免 GPU 冷启动成本。
    public InferenceDevicePolicy SuperResolution { get; } = new(hardwareAccelerationEnabled, InferenceDevicePreference.GpuPreferred);
    public InferenceDevicePolicy StyleTransfer { get; } = new(hardwareAccelerationEnabled, InferenceDevicePreference.CpuPreferred);
    public InferenceDevicePolicy DepthEstimate { get; } = new(hardwareAccelerationEnabled, InferenceDevicePreference.CpuPreferred);
}
