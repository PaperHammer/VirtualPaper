using VirtualPaper.Common.Utils.PInvoke;

namespace VirtualPaper.Common.Utils.Shell {
    /// <summary>
    /// 桌面窗口层级设置
    /// </summary>
    public static class DesktopUtil {
        /// <summary>
        /// 初始系统桌面图标可见性设置<br>
        /// Issue: does not update if user changes setting.</br>
        /// </summary>
        public static bool DesktopIconVisibilityDefault { get; }

        static DesktopUtil() {
            DesktopIconVisibilityDefault = GetDesktopIconVisibility();
        }

        public static bool GetDesktopIconVisibility() {
            Native.SHELLSTATE state = new();
            Native.SHGetSetSettings(ref state, Native.SSF.SSF_HIDEICONS, false); //get _state
            return !state.fHideIcons;
        }

        //ref: https://stackoverflow.com/questions/6402834/how-to-hide-desktop-icons-programmatically/
        public static void SetDesktopIconVisibility(bool isVisible) {
            //Does not work in Win10
            //Native.SHGetSetSettings(ref _state, Native.SSF.SSF_HIDEICONS, true);

            if (GetDesktopIconVisibility() ^ isVisible) //XOR!!!
            {
                _ = Native.SendMessage(GetDesktopSHELLDLL_DefView(), (int)Native.WM.COMMAND, (IntPtr)0x7402, IntPtr.Zero);
            }
        }

        private static IntPtr GetDesktopSHELLDLL_DefView() {
            var hShellViewWin = IntPtr.Zero;
            var hWorkerW = IntPtr.Zero;

            var hProgman = Native.FindWindow("Progman", "Program Manager");
            var hDesktopWnd = Native.GetDesktopWindow();

            if (hProgman != IntPtr.Zero) {
                hShellViewWin = Native.FindWindowEx(hProgman, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (hShellViewWin == IntPtr.Zero) {
                    do {
                        hWorkerW = Native.FindWindowEx(hDesktopWnd, hWorkerW, "WorkerW", null);
                        hShellViewWin = Native.FindWindowEx(hWorkerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                    } while (hShellViewWin == IntPtr.Zero && hWorkerW != IntPtr.Zero);
                }
            }
            return hShellViewWin;
        }

        /// <summary>
        /// 强制重绘桌面 - 清除壁纸，即使在关闭后仍保留在屏幕上。
        /// </summary>
        public static void RefreshDesktop() {
            //todo: Find a better way to do this?
            _ = Native.SystemParametersInfo(Native.SPI_SETDESKWALLPAPER, 0, null, Native.SPIF_UPDATEINIFILE);
        }
    }
}
