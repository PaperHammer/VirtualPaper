namespace VirtualPaper.ML.Inference;

public enum InferenceDevice { Uninitialized, Cpu, DirectML }

public enum InferenceSelectionReason {
    None,
    HardwareAccelerationDisabled,
    CpuPreferred,
    ModelUnsupported,
    GpuPreferred,
    GpuFailed
}

public sealed record InferenceState(bool HardwareAccelerationEnabled, InferenceDevice Device, InferenceSelectionReason Reason) {
    public bool IsCpuFallback => Device == InferenceDevice.Cpu && Reason == InferenceSelectionReason.GpuFailed;
}

public interface IInferenceStatusSource {
    InferenceState InferenceState { get; }
    event EventHandler<InferenceState>? InferenceStateChanged;
}

public abstract class InferenceModel : IInferenceStatusSource {
    protected InferenceModel(InferenceDevicePolicy policy) {
        ArgumentNullException.ThrowIfNull(policy);
        DevicePolicy = policy;
        _inferenceState = new(policy.HardwareAccelerationEnabled, InferenceDevice.Uninitialized, InferenceSelectionReason.None);
    }

    protected InferenceDevicePolicy DevicePolicy { get; }
    public InferenceState InferenceState => Volatile.Read(ref _inferenceState);
    public event EventHandler<InferenceState>? InferenceStateChanged;

    protected void ReportInferenceState(InferenceState state) {
        Volatile.Write(ref _inferenceState, state);
        InferenceStateChanged?.Invoke(this, state);
    }

    private InferenceState _inferenceState;
}
