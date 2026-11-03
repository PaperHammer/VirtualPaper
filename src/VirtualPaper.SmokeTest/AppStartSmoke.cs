using System.Diagnostics;
using System.IO;
using Google.Protobuf.WellKnownTypes;
using GrpcDotNetNamedPipes;
using VirtualPaper.Common;
using VirtualPaper.Grpc.Service.Commands;

namespace VirtualPaper.SmokeTest;

internal static class AppStartSmoke {
    // Core normally runs in the tray. Open UI via its public RPC, never by launching UI.exe directly.
    public static void TestLaunchUI(string installDir, TimeSpan timeout, string dataRoot) {
        string corePath = Path.Combine(installDir, "VirtualPaper.exe");
        string uiPath = Path.Combine(installDir, "Plugins", "UI", "VirtualPaper.UI.exe");
        if (!File.Exists(corePath) || !File.Exists(uiPath))
            throw new FileNotFoundException("Installed Core/UI executable is missing.");
        string session = Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(TestProcessEnvironment.SessionVariable, session);
        Environment.SetEnvironmentVariable(TestProcessEnvironment.DataRootVariable, Path.GetFullPath(dataRoot));
        var start = new ProcessStartInfo(corePath) {
            WorkingDirectory = installDir, UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        // Inherited by UI: isolate singleton/IPC/configuration and suppress system registration.
        var commands = new Grpc_CommandsService.Grpc_CommandsServiceClient(
            new NamedPipeChannel(".", Constants.CoreField.GrpcPipeServerName));
        using var core = Process.Start(start) ?? throw new InvalidOperationException("Core did not start.");
        Console.WriteLine($"Core PID: {core.Id}");
        try {
            var elapsed = Stopwatch.StartNew();
            TimeSpan? readyAt = null;
            bool requested = false;
            while (elapsed.Elapsed < timeout) {
                if (core.HasExited)
                    throw new InvalidOperationException($"Core exited during startup: {core.ExitCode}");
                if (!requested) {
                    try {
                        commands.ShowUIAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(1))
                            .ResponseAsync.GetAwaiter().GetResult();
                        requested = true;
                    }
                    catch (global::Grpc.Core.RpcException) { /* Wait for Core's production RPC server. */ }
                }
                using var ui = FindInstalledUi(uiPath);
                if (ui != null) {
                    ui.Refresh();
                    if (!ui.HasExited && ui.MainWindowHandle != IntPtr.Zero) {
                        readyAt ??= elapsed.Elapsed;
                        if (elapsed.Elapsed - readyAt.Value >= TimeSpan.FromSeconds(2)) {
                            Console.WriteLine($"Core-launched UI ready (PID: {ui.Id}, HWND: {ui.MainWindowHandle}).");
                            return;
                        }
                    }
                    else readyAt = null;
                }
                else readyAt = null;
                Thread.Sleep(200);
            }
            throw new TimeoutException($"Core did not open a stable UI window within {timeout.TotalSeconds}s.");
        }
        finally {
            // Only this install's processes are ours; never kill daily instances by process name.
            if (!core.HasExited) {
                try {
                    commands.ShutDownAsync(new Empty(), deadline: DateTime.UtcNow.AddSeconds(2))
                        .ResponseAsync.GetAwaiter().GetResult();
                }
                catch (global::Grpc.Core.RpcException) { /* A shutting-down server may close its pipe first. */ }
                if (!core.WaitForExit(8000)) core.Kill(entireProcessTree: true);
                if (!core.WaitForExit(10000)) throw new TimeoutException("Smoke Core did not stop.");
            }
            using var ui = FindInstalledUi(uiPath);
            if (ui != null && !ui.HasExited) {
                ui.Kill(entireProcessTree: true);
                if (!ui.WaitForExit(10000)) throw new TimeoutException("Smoke UI did not stop.");
            }
        }
    }

    private static Process? FindInstalledUi(string uiPath) {
        foreach (var process in Process.GetProcessesByName("VirtualPaper.UI")) {
            try {
                if (string.Equals(process.MainModule?.FileName, uiPath, StringComparison.OrdinalIgnoreCase))
                    return process;
            }
            catch (InvalidOperationException) { /* Process exited during enumeration. */ }
            catch (System.ComponentModel.Win32Exception) { /* Another user's process. */ }
            process.Dispose();
        }
        return null;
    }
}
