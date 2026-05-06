using System.Diagnostics;

namespace VirtualPaper.Utils.Interfcaes {
    public interface IProcessLauncher {
        event EventHandler? Exited;
        event EventHandler<ProcessOutputEventArgs>? OutputDataReceived;

        int ProcessId { get; }
        bool HasExited { get; }
        void Launch(ProcessStartInfo startInfo);
        void BeginOutputReadLine();
        void WriteStdin(string msg);
        void Kill();
        void Dispose();
    }

    public class ProcessOutputEventArgs(string? data) : EventArgs {
        public string? Data { get; } = data;
    }
}
