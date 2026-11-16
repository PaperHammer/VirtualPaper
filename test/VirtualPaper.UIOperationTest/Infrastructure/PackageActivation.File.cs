using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using VirtualPaper.Common;

namespace VirtualPaper.UIOperationTest.Infrastructure;

internal static partial class PackageActivation {
    // See Microsoft's "CLR Profilers and Windows Store Apps" startup-load example.
    // The temporary debug environment is scoped to this owned test package only.
    private static Process StartFile(string path) {
        string appId = Environment.GetEnvironmentVariable("VIRTUALPAPER_TEST_PACKAGE_AUMID")
            ?? throw new InvalidOperationException("Installed package AUMID is missing.");
        using var environment = new FileActivationEnvironment();
        ShellFileAssociation.Invoke(path, appId);
        var timer = Stopwatch.StartNew();
        while (true) {
            if (File.Exists(environment.MarkerPath)) {
                string[] marker = File.ReadAllLines(environment.MarkerPath);
                if (marker.Length != 2 || marker[1] != ExpectedFullName) throw new InvalidOperationException("File activation identity marker mismatch.");
                var process = Process.GetProcessById(int.Parse(marker[0]));
                _ = process.Handle;
                AssertIdentity(process);
                return process;
            }
            if (timer.Elapsed > TimeSpan.FromSeconds(10))
                throw new TimeoutException("The isolated Shell file-activation process was not resumed.");
            Thread.Sleep(50);
        }
    }

    private sealed class FileActivationEnvironment : IDisposable {
        private readonly IPackageDebugSettings _settings;
        private readonly string _package;
        private readonly string _requestPath;
        internal string MarkerPath { get; }

        public FileActivationEnvironment() {
            _package = ExpectedFullName ?? throw new InvalidOperationException("A test package is required.");
            if (!TestProcessEnvironment.IsEnabled) throw new InvalidOperationException("Process-test isolation is required.");
            string requestId = Guid.NewGuid().ToString("N");
            MarkerPath = Path.Combine(TestProcessEnvironment.DataRoot!, "activation", requestId + ".resumed");
            _requestPath = Path.Combine(Path.GetTempPath(), "VPA-" + requestId + ".json");
            string helper = Environment.GetEnvironmentVariable("VIRTUALPAPER_TEST_ACTIVATION_RESUMER")
                ?? throw new InvalidOperationException("The package activation helper is missing.");
            if (!File.Exists(helper)) throw new FileNotFoundException("Package activation helper not found.", helper);
            _settings = (IPackageDebugSettings)Activator.CreateInstance(
                Type.GetTypeFromCLSID(new Guid("B1AEC16F-2383-4852-B0E9-8F0B1DC66B4D"))!)!;
            var values = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
                .Where(entry => ((string)entry.Key).StartsWith("VIRTUALPAPER_TEST_", StringComparison.Ordinal))
                .Select(entry => $"{entry.Key}={entry.Value}");
            nint block = Marshal.StringToCoTaskMemUni(string.Join('\0', values) + "\0\0");
            // Keep the debugger command short. Its own isolation parameters
            // live in a per-request file, not the target's injected environment.
            string command = $"\"{helper}\" --resume-package-activation {requestId}";
            try {
                File.WriteAllText(_requestPath, JsonSerializer.Serialize(new {
                    Package = _package, Session = TestProcessEnvironment.SessionId, Root = TestProcessEnvironment.DataRoot
                }));
                _settings.EnableDebugging(_package, command, block);
            }
            catch { File.Delete(_requestPath); Marshal.FinalReleaseComObject(_settings); throw; }
            finally { Marshal.FreeCoTaskMem(block); }
        }

        public void Dispose() {
            try { _settings.DisableDebugging(_package); }
            finally { Marshal.FinalReleaseComObject(_settings); File.Delete(_requestPath); }
        }
    }

    [ComImport, Guid("F27C3930-8029-4AD1-94E3-3DBA417810C1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPackageDebugSettings {
        void EnableDebugging([MarshalAs(UnmanagedType.LPWStr)] string packageFullName,
            [MarshalAs(UnmanagedType.LPWStr)] string debuggerCommandLine, nint environment);
        void DisableDebugging([MarshalAs(UnmanagedType.LPWStr)] string packageFullName);
    }

}
