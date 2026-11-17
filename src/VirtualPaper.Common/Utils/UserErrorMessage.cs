using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using static VirtualPaper.Common.Errors;

namespace VirtualPaper.Common.Utils;

/// <summary>Classifies failures for display without exposing framework messages or RPC payloads.</summary>
public static class UserErrorMessage {
    public static string GetResourceKey(Exception exception) {
        ArgumentNullException.ThrowIfNull(exception);
        return Classify(exception) ?? "UserError_Unexpected";
    }

    private static string? Classify(Exception exception) => exception switch {
        AggregateException aggregate when aggregate.InnerExceptions.Count == 1 => Classify(aggregate.InnerExceptions[0]),
        TargetInvocationException { InnerException: { } inner } => Classify(inner),
        FileAccessException or FileCreateException => exception.InnerException is { } inner
            ? Classify(inner) ?? "UserError_FileAccess" : "UserError_FileAccess",
        OperationCanceledException { InnerException: TimeoutException } => "UserError_Timeout",
        OperationCanceledException => "UserError_Canceled",
        TimeoutException => "UserError_Timeout",
        FileNotFoundException or DirectoryNotFoundException or WallpaperNotFoundException => "UserError_NotFound",
        UnauthorizedAccessException => "UserError_AccessDenied",
        PathTooLongException => "UserError_PathTooLong",
        JsonException or InvalidDataException => "UserError_InvalidData",
        HttpRequestException or SocketException => "UserError_Network",
        WebException web => web.Status == WebExceptionStatus.Timeout ? "UserError_Timeout" : "UserError_Network",
        Win32Exception win32 => ClassifyWindowsError(win32.NativeErrorCode),
        IOException io => (unchecked((uint)io.HResult) & 0xffff0000) == 0x80070000
            ? ClassifyWindowsError(io.HResult & 0xffff) ?? "UserError_FileAccess" : "UserError_FileAccess",
        WorkerWException => "UserError_DesktopUnavailable",
        ScreenNotFoundException => "UserError_DisplayMissing",
        WallpaperPluginNotFoundException => "UserError_ComponentMissing",
        _ => null
    };

    private static string? ClassifyWindowsError(int code) => code switch {
        2 or 3 => "UserError_NotFound",
        5 => "UserError_AccessDenied",
        32 or 33 => "UserError_FileBusy",
        39 or 112 => "UserError_StorageFull",
        206 => "UserError_PathTooLong",
        _ => null
    };
}
