using Microsoft.ML.OnnxRuntime;

namespace VirtualPaper.ML.Inference;

/// <summary>初始化只回退明确的设备故障；执行还覆盖原生算子的后端兼容错误。</summary>
internal static class GpuInferenceFailure {
    internal static bool IsExecutionFailure(Exception exception) {
        if (exception is AggregateException aggregate) {
            var failures = aggregate.Flatten().InnerExceptions;
            return failures.Count > 0 && failures.All(IsExecutionFailure);
        }
        // DirectML 的算子执行失败可能只有 RuntimeException，没有设备名（例如 AdaIN Reshape）。
        // 参数/模型校验错误、取消和 I/O 均不属于这个原生错误码。
        return IsDeviceFailure(exception) || exception is OnnxRuntimeException
            && exception.Message.StartsWith("[ErrorCode:RuntimeException]", StringComparison.Ordinal);
    }

    internal static bool IsDeviceFailure(Exception exception) {
        if (exception is AggregateException aggregate) {
            var failures = aggregate.Flatten().InnerExceptions;
            return failures.Count > 0 && failures.All(IsDeviceFailure);
        }
        if (exception is not (OnnxRuntimeException or DllNotFoundException or EntryPointNotFoundException or NotSupportedException))
            return false;
        string message = exception.Message;
        if (InvalidModelOrInput.Any(code => message.Contains($"[ErrorCode:{code}]", StringComparison.OrdinalIgnoreCase)))
            return false;
        return Markers.Any(marker => message.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly string[] Markers = [
        "DirectML", "DmlExecutionProvider", "DML device", "DML support", "AppendExecutionProvider_DML",
        "/dml/", "\\dml\\", "D3D12", "DXGI_ERROR_DEVICE", "DXGI_ERROR_DRIVER_INTERNAL_ERROR",
        "0x887A0005", "0x887A0006", "0x887A0007", "0x887A0020"
    ];

    private static readonly string[] InvalidModelOrInput = [
        "InvalidArgument", "NoSuchFile", "NoModel", "InvalidProtobuf", "InvalidGraph"
    ];
}
