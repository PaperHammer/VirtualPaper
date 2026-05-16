using System.Diagnostics;
using VirtualPaper.Common.Utils.PInvoke;
using VirtualPaper.Utils.Interfcaes;

namespace VirtualPaper.Utils.Services {
    public class NativeService : INativeService {
        /// <summary>
        /// 创建 WorkerW 窗口（在 Progman 和桌面图标层之间插入）
        /// 标准做法：向 Progman 发送 0x052C 消息触发 WorkerW 创建，再枚举找到它
        /// </summary>
        public nint CreateWorkerW() {
            // Fetch the Progman window
            var progman = Native.FindWindow("Progman", null);

            nint result = nint.Zero;

            // Send 0x052C to Progman. This message directs Progman to spawn a 
            // WorkerW behind the desktop icons. If it is already there, nothing 
            // happens.
            Native.SendMessageTimeout(progman,
                                   0x052C,
                                   new IntPtr(0xD),
                                   new IntPtr(0x1),
                                   Native.SendMessageTimeoutFlags.SMTO_NORMAL,
                                   1000,
                                   out result);
            // Spy++ output
            // .....
            // 0x00010190 "" WorkerW
            //   ...
            //   0x000100EE "" SHELLDLL_DefView
            //     0x000100F0 "FolderView" SysListView32
            // 0x00100B8A "" WorkerW       <-- This is the WorkerW curInstance we are after!
            // 0x000100EC "Program Manager" Progman
            var _workerW = IntPtr.Zero;

            // We enumerate All Windows, until we find one, that has the SHELLDLL_DefView 
            // as a child. 
            // If we found that window, we take its next sibling and assign it to _workerW.
            Native.EnumWindows(new Native.EnumWindowsProc((tophandle, topparamhandle) => {
                IntPtr p = Native.FindWindowEx(tophandle,
                                            IntPtr.Zero,
                                            "SHELLDLL_DefView",
                                            IntPtr.Zero);

                if (p != IntPtr.Zero) {
                    // Gets the WorkerW Window after the current one.
                    _workerW = Native.FindWindowEx(IntPtr.Zero,
                                                    tophandle,
                                                    "WorkerW",
                                                    IntPtr.Zero);
                }

                return true;
            }), IntPtr.Zero);

            // Some Windows 11 builds have a different Progman window layout.
            // If the above code failed to find WorkerW, we should try this.
            // Spy++ output
            // 0x000100EC "Program Manager" Progman
            //   0x000100EE "" SHELLDLL_DefView
            //     0x000100F0 "FolderView" SysListView32
            //   0x00100B8A "" WorkerW       <-- This is the WorkerW curInstance we are after!
            if (_workerW == IntPtr.Zero) {
                _workerW = Native.FindWindowEx(progman,
                                                IntPtr.Zero,
                                                "WorkerW",
                                                IntPtr.Zero);
            }

            return _workerW;
        }

        /// <summary>
        /// 将子窗口（壁纸进程窗口）设置为 WorkerW 的子窗口
        /// </summary>
        public bool TrySetParentWorkerW(nint childHandle, nint parentHandle) {
            IntPtr ret = Native.SetParent(childHandle, parentHandle);
            if (ret.Equals(IntPtr.Zero))
                return false;

            return true;
        }

        /// <summary>
        /// 调整窗口位置和大小以匹配显示器区域
        /// </summary>
        public bool SetWindowPos(nint handle, int hWndInsertAfter, int x, int y, int width, int height, int wFlags) {
            return Native.SetWindowPos(handle, hWndInsertAfter, x, y, width, height, wFlags);
        }

        /// <summary>
        /// 刷新桌面（强制重绘 WorkerW 区域）
        /// </summary>
        public void RefreshDesktop() {
            _ = Native.SystemParametersInfo(Native.SPI_SETDESKWALLPAPER, 0, null, Native.SPIF_UPDATEINIFILE);
        }

        /// <summary>
        /// 获取 WorkerW 的屏幕坐标矩形
        /// </summary>
        public nint GetWorkerWRect(out Native.RECT rect) {
            var workerW = CreateWorkerW();
            rect = default;

            if (workerW != IntPtr.Zero) {
                Native.GetWindowRect(workerW, out rect);
            }

            return workerW;
        }

        /// <summary>
        /// 将窗口坐标从屏幕坐标映射到 WorkerW 的客户端坐标
        /// 用于多显示器场景下正确定位壁纸窗口
        /// </summary>
        public bool MapWindowPoints(nint handle, nint workerW, ref Native.RECT rect, int cPoints) {
            int result = Native.MapWindowPoints(handle, workerW, ref rect, cPoints);

            return result != 0;
        }

        public int SHQueryUserNotificationState(out Native.QUERY_USER_NOTIFICATION_STATE state) {
            return Native.SHQueryUserNotificationState(out state);
        }

        public nint GetForegroundWindow() {
            return Native.GetForegroundWindow();
        }

        public uint GetWindowThreadProcessId(nint hwnd, out int processId) {
            return Native.GetWindowThreadProcessId(hwnd, out processId);
        }

        public string GetProcessNameById(int processId) {
            return Process.GetProcessById(processId).ProcessName;
        }

        public void LockWorkStation() {
            Native.LockWorkStation();
        }
    }
}
