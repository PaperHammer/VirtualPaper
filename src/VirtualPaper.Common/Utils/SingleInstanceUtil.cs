using System.Diagnostics;

namespace VirtualPaper.Common.Utils {
    public static class SingleInstanceUtil {
        public static bool IsAppMutexRunning(string mutexName) {
            Mutex? mutex = null;
            try {
                return Mutex.TryOpenExisting(mutexName, out mutex);
            }
            finally {
                mutex?.Dispose();
            }
        }

        public static bool IsAppProcessRunning(string processName) =>
            Process.GetProcessesByName(processName).Length != 0;

        public static bool IsNamedPipeExists(string pipeName) =>
            Directory.GetFiles("\\\\.\\pipe\\").Any(f => f.Equals("\\\\.\\pipe\\" + pipeName));
    }
}
