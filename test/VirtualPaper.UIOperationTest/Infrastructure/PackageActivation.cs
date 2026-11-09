using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using VirtualPaper.Common;

namespace VirtualPaper.UIOperationTest.Infrastructure;

internal static class PackageActivation {
    public static string? ExpectedFullName => Environment.GetEnvironmentVariable("VIRTUALPAPER_TEST_PACKAGE_FULL_NAME");

    public static Process Start() {
        string appId = Environment.GetEnvironmentVariable("VIRTUALPAPER_TEST_PACKAGE_AUMID")
            ?? throw new InvalidOperationException("Installed package AUMID is missing.");
        string root = TestProcessEnvironment.DataRoot!;
        if (root.Contains('"')) throw new InvalidOperationException("Invalid package test data root.");
        var manager = (IApplicationActivationManager)Activator.CreateInstance(
            Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"))!)!;
        try {
            manager.ActivateApplication(appId,
                $"--virtualpaper-test-session {TestProcessEnvironment.SessionId} --virtualpaper-test-data-root \"{root.TrimEnd('\\')}\"",
                0, out uint processId);
            var process = Process.GetProcessById((int)processId);
            _ = process.Handle;
            AssertIdentity(process);
            return process;
        }
        finally { Marshal.FinalReleaseComObject(manager); }
    }

    public static void AssertIdentity(Process process) {
        if (ExpectedFullName == null) return;
        uint size = 0;
        int error = GetPackageFullName(process.Handle, ref size, null);
        if (error != 122) throw new InvalidOperationException($"PID {process.Id} has no readable package identity: {error}");
        var name = new StringBuilder((int)size);
        error = GetPackageFullName(process.Handle, ref size, name);
        if (error != 0 || name.ToString() != ExpectedFullName)
            throw new InvalidOperationException($"PID {process.Id} package identity mismatch: {name}, error {error}");
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetPackageFullName(nint process, ref uint length, StringBuilder? name);

    [ComImport, Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager {
        void ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appId,
            [MarshalAs(UnmanagedType.LPWStr)] string arguments, uint options, out uint processId);
        void ActivateForFile(nint appId, nint items, nint verb, out uint processId);
        void ActivateForProtocol(nint appId, nint items, out uint processId);
    }
}
