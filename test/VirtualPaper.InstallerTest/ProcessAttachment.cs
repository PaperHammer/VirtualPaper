using System.Diagnostics;

namespace VirtualPaper.InstallerTest;

internal static class ProcessAttachment {
    public static Process RetainHandle(Process process) {
        try {
            // PID/name lookups do not retain a native handle. Acquire it while the
            // process is alive so ExitCode remains available after it terminates.
            _ = process.SafeHandle;
            return process;
        } catch {
            process.Dispose();
            throw;
        }
    }
}
