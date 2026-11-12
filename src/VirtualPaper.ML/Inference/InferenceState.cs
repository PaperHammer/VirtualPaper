namespace VirtualPaper.ML.Inference;

public enum InferenceDevice { Uninitialized, Cpu, DirectML }

public sealed record InferenceState(bool AccelerationRequested, InferenceDevice Device, bool IsCpuFallback);

public interface IInferenceStatusSource {
    InferenceState InferenceState { get; }
    event EventHandler<InferenceState>? InferenceStateChanged;
}

public abstract class InferenceModel : IInferenceStatusSource {
    protected InferenceModel(InferenceDevicePolicy policy) {
        ArgumentNullException.ThrowIfNull(policy);
        DevicePolicy = policy;
        _inferenceState = new(policy.AllowGpu, InferenceDevice.Uninitialized, false);
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
