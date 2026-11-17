using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using VirtualPaper.Common.Utils;
using static VirtualPaper.Common.Errors;

namespace VirtualPaper.Core.Test.T_Common;

[TestClass]
public class UserErrorMessageTests {
    public static IEnumerable<object[]> Cases {
        get {
            yield return [new FileNotFoundException("internal path"), "UserError_NotFound"];
            yield return [new DirectoryNotFoundException("internal path"), "UserError_NotFound"];
            yield return [new UnauthorizedAccessException("access denied HRESULT"), "UserError_AccessDenied"];
            yield return [new PathTooLongException("long internal path"), "UserError_PathTooLong"];
            yield return [new InvalidDataException("invalid metadata node"), "UserError_InvalidData"];
            yield return [new JsonException("$.Values: parser offset 42"), "UserError_InvalidData"];
            yield return [new IOException("unknown I/O failure"), "UserError_FileAccess"];
            yield return [new IOException("sharing violation", unchecked((int)0x80070020)), "UserError_FileBusy"];
            yield return [new IOException("lock violation", unchecked((int)0x80070021)), "UserError_FileBusy"];
            yield return [new IOException("disk full", unchecked((int)0x80070070)), "UserError_StorageFull"];
            yield return [new IOException("long path", unchecked((int)0x800700ce)), "UserError_PathTooLong"];
            yield return [new Win32Exception(5), "UserError_AccessDenied"];
            yield return [new Win32Exception(39), "UserError_StorageFull"];
            yield return [new Win32Exception(32), "UserError_FileBusy"];
            yield return [new Win32Exception(1234), "UserError_Unexpected"];
            yield return [new SocketException(10061), "UserError_Network"];
            yield return [new HttpRequestException("HTTP stack trace"), "UserError_Network"];
            yield return [new WebException("network timeout", WebExceptionStatus.Timeout), "UserError_Timeout"];
            yield return [new TimeoutException("internal task deadline"), "UserError_Timeout"];
            yield return [new OperationCanceledException("cancelled task"), "UserError_Canceled"];
            yield return [new TaskCanceledException("HTTP timed out", new TimeoutException()), "UserError_Timeout"];
            yield return [new AggregateException(new UnauthorizedAccessException()), "UserError_AccessDenied"];
            yield return [new TargetInvocationException(new JsonException()), "UserError_InvalidData"];
            yield return [new AggregateException(new OperationCanceledException(), new IOException()), "UserError_Unexpected"];
            yield return [new FileAccessException("config.json", "write json", new JsonException()), "UserError_InvalidData"];
            yield return [new FileAccessException("config.json", "write json", new InvalidOperationException()), "UserError_FileAccess"];
            yield return [new FileCreateException("raw message", "config.json"), "UserError_FileAccess"];
            yield return [new WorkerWException("WorkerW HWND=0"), "UserError_DesktopUnavailable"];
            yield return [new ScreenNotFoundException("MONITOR_1 missing"), "UserError_DisplayMissing"];
            yield return [new WallpaperPluginNotFoundException("player.exe missing"), "UserError_ComponentMissing"];
            yield return [new InvalidOperationException("Status(StatusCode=Internal, Detail=secret)"), "UserError_Unexpected"];
            yield return [new global::Grpc.Core.RpcException(new global::Grpc.Core.Status(global::Grpc.Core.StatusCode.Unavailable, "pipe connection failed")), "UserError_Unexpected"];
        }
    }

    [TestMethod]
    [DynamicData(nameof(Cases))]
    public void KnownFailure_ChoosesAUserFacingResourceWithoutParsingRawMessages(Exception exception, string key) {
        Assert.AreEqual(key, UserErrorMessage.GetResourceKey(exception));
    }
}
