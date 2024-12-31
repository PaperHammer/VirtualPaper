using VirtualPaper.Common.Utils.PInvoke;

namespace VirtualPaper.ScreenSaver.Utils {
    internal class TaskManagerListener() {
        public async void StartListening() {
            await Task.Run(async () => {
                while (true) {
                    if (IsTaskManagerActive()) {
                        App.ShutDown();
                        return;
                    }

                    await Task.Delay(1000);
                }
            });
        }

        private bool IsTaskManagerActive() {
            IntPtr hWnd = Native.FindWindow("TaskManagerWindow", null);
            if (hWnd == IntPtr.Zero) {
                return false;
            }

            if (!Native.IsWindowVisible(hWnd)) {
                return false;
            }

            _ = Native.GetWindowThreadProcessId(hWnd, out int processId);
            if (processId == Environment.ProcessId) {
                return false;
            }

            return true;
        }
    }
}
