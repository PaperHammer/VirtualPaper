using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using VirtualPaper.Common;

namespace VirtualPaper.InstallerTest;

// IPackageDebugSettings launches this short-lived helper before the activated
// process runs, allowing file activation to retain the normal test environment.
internal static class PackageActivationResume {
    internal static int Run(string[] args) {
        Process? process = null;
        bool owned = false;
        string? errorPath = null;
        try {
            if (args.Length != 5 || !Guid.TryParseExact(args[0], "N", out _) || args[1] != "-p" || args[3] != "-tid"
                || !int.TryParse(args[2], out int pid) || !uint.TryParse(args[4], out uint tid))
                throw new ArgumentException("Expected a package process and its suspended thread.");
            using var request = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetTempPath(), "VPA-" + args[0] + ".json")));
            string expected = request.RootElement.GetProperty("Package").GetString()!;
            Environment.SetEnvironmentVariable(TestProcessEnvironment.SessionVariable, request.RootElement.GetProperty("Session").GetString());
            Environment.SetEnvironmentVariable(TestProcessEnvironment.DataRootVariable, request.RootElement.GetProperty("Root").GetString());
            if (!TestProcessEnvironment.IsEnabled) throw new InvalidOperationException("File activation requires process-test isolation.");
            string directory = Path.Combine(TestProcessEnvironment.DataRoot!, "activation");
            Directory.CreateDirectory(directory);
            errorPath = Path.Combine(directory, args[0] + ".error");
            process = Process.GetProcessById(pid);
            uint length = 0;
            if (GetPackageFullName(process.Handle, ref length, null) != 122)
                throw new InvalidOperationException("The activation process is not packaged.");
            var identity = new StringBuilder((int)length);
            if (GetPackageFullName(process.Handle, ref length, identity) != 0 || identity.ToString() != expected
                || process.ProcessName != "VirtualPaper")
                throw new InvalidOperationException("Refusing to resume an unrelated process.");
            owned = true;
            using var thread = OpenThread(0x0002 | 0x0800, false, tid); // suspend/resume and limited query
            if (thread.IsInvalid || GetProcessIdOfThread(thread) != pid)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "The suspended thread does not belong to the package process.");
            if (ResumeThread(thread) == uint.MaxValue) throw new Win32Exception(Marshal.GetLastWin32Error());
            string marker = Path.Combine(directory, args[0] + ".resumed");
            File.WriteAllText(marker + ".tmp", $"{pid}\n{expected}");
            File.Move(marker + ".tmp", marker);
            return 0;
        }
        catch (Exception ex) {
            Console.Error.WriteLine(ex);
            if (errorPath != null) File.WriteAllText(errorPath, ex.ToString());
            // A failed isolation check must not let this test-owned process enter daily startup.
            if (owned && process is { HasExited: false }) process.Kill();
            return 1;
        }
        finally { process?.Dispose(); }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetPackageFullName(nint process, ref uint length, StringBuilder? name);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeWaitHandle OpenThread(uint access, bool inheritHandle, uint threadId);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetProcessIdOfThread(SafeWaitHandle thread);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(SafeWaitHandle thread);
}
