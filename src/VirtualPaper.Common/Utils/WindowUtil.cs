using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using VirtualPaper.Common.Utils.PInvoke;

namespace VirtualPaper.Common.Utils {
    public static class WindowUtil {
        public static void SetParentSafe(IntPtr child, IntPtr parent) {
            IntPtr ret = Native.SetParent(child, parent);
            if (ret.Equals(IntPtr.Zero)) {
                //LogUtil.LogWin32Error("Failed to set window parent");
            }
        }

        /// <summary>
        /// Makes window toolwindow and force remove from taskbar.
        /// </summary>
        /// <param name="handle">window handle</param>
        public static void RemoveWindowFromTaskbar(IntPtr handle) {
            var styleCurrentWindowExtended = Native.GetWindowLongPtr(handle, (int)Native.GWL.GWL_EXSTYLE);

            var styleNewWindowExtended = styleCurrentWindowExtended.ToInt64() |
                   (Int64)Native.WindowStyles.WS_EX_NOACTIVATE |
                   (Int64)Native.WindowStyles.WS_EX_TOOLWINDOW;

            //update window styles
            //https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptra
            //Certain window data is cached, so changes you make using SetWindowLongPtr will not take effect until you call the SetWindowPos function?
            Native.ShowWindow(handle, (int)Native.SHOWWINDOW.SW_HIDE);
            if (Native.SetWindowLongPtr(new HandleRef(null, handle), (int)Native.GWL.GWL_EXSTYLE, (IntPtr)styleNewWindowExtended) == IntPtr.Zero) {
                //LogUtil.LogWin32Error("Failed to modify window style");
            }
            Native.ShowWindow(handle, (int)Native.SHOWWINDOW.SW_SHOW);
        }

        /// <summary>
        /// Removes window border and some menuitems. Won'T remove everything in apps with custom UI system.<para>
        /// Ref: https://github.com/Codeusa/Borderless-Gaming
        /// </para>
        /// </summary>
        /// <param name="handle">Window handle</param>
        public static void BorderlessWinStyle(IntPtr handle) {
            // Get window styles
            var styleCurrentWindowStandard = Native.GetWindowLongPtr(handle, (int)Native.GWL.GWL_STYLE);
            var styleCurrentWindowExtended = Native.GetWindowLongPtr(handle, (int)Native.GWL.GWL_EXSTYLE);

            // Compute new styles (XOR of the inverse of All the bits to filter)
            var styleNewWindowStandard =
                              styleCurrentWindowStandard.ToInt64()
                              & ~(
                                    (Int64)Native.WindowStyles.WS_CAPTION // composite of Border and DialogFrame          
                                  | (Int64)Native.WindowStyles.WS_THICKFRAME
                                  | (Int64)Native.WindowStyles.WS_SYSMENU
                                  | (Int64)Native.WindowStyles.WS_MAXIMIZEBOX // same as TabStop
                                  | (Int64)Native.WindowStyles.WS_MINIMIZEBOX // same as Group
                              );


            var styleNewWindowExtended =
                styleCurrentWindowExtended.ToInt64()
                & ~(
                      (Int64)Native.WindowStyles.WS_EX_DLGMODALFRAME
                    | (Int64)Native.WindowStyles.WS_EX_COMPOSITED
                    | (Int64)Native.WindowStyles.WS_EX_WINDOWEDGE
                    | (Int64)Native.WindowStyles.WS_EX_CLIENTEDGE
                    | (Int64)Native.WindowStyles.WS_EX_LAYERED
                    | (Int64)Native.WindowStyles.WS_EX_STATICEDGE
                    | (Int64)Native.WindowStyles.WS_EX_TOOLWINDOW
                    | (Int64)Native.WindowStyles.WS_EX_APPWINDOW
                );

            // update window styles
            if (Native.SetWindowLongPtr(new HandleRef(null, handle), (int)Native.GWL.GWL_STYLE, (IntPtr)styleNewWindowStandard) == IntPtr.Zero) {
                //LogUtil.LogWin32Error("Failed to modify window style(1)");
            }

            if (Native.SetWindowLongPtr(new HandleRef(null, handle), (int)Native.GWL.GWL_EXSTYLE, (IntPtr)styleNewWindowExtended) == IntPtr.Zero) {
                //LogUtil.LogWin32Error("Failed to modify window style(2)");
            }

            // remove the menu and menuitems and force a redraw
            var menuHandle = Native.GetMenu(handle);
            if (menuHandle != IntPtr.Zero) {
                var menuItemCount = Native.GetMenuItemCount(menuHandle);

                for (var i = 0; i < menuItemCount; i++) {
                    Native.RemoveMenu(menuHandle, 0, Native.MF_BYPOSITION | Native.MF_REMOVE);
                }
                Native.DrawMenuBar(handle);
            }
        }

        private const int LWA_ALPHA = 0x2;
        private const int LWA_COLORKEY = 0x1;

        /// <summary>
        /// Set window alpha.
        /// </summary>
        /// <param name="Handle"></param>
        public static void SetWindowTransparency(IntPtr Handle) {
            var styleCurrentWindowExtended = Native.GetWindowLongPtr(Handle, (-20));
            var styleNewWindowExtended =
                styleCurrentWindowExtended.ToInt64() ^
                Native.WindowStyles.WS_EX_LAYERED;

            Native.SetWindowLongPtr(new HandleRef(null, Handle), (int)Native.GWL.GWL_EXSTYLE, (IntPtr)styleNewWindowExtended);
            Native.SetLayeredWindowAttributes(Handle, 0, 128, LWA_ALPHA);
        }

        public static bool IsExcludedDesktopWindowClass(IntPtr hwnd) {
            const int maxChars = 256;
            StringBuilder className = new(maxChars);
            return Native.GetClassName((int)hwnd, className, maxChars) > 0 &&
                WindowClassExclusions.DesktopClasses.Contains(className.ToString());
        }

        public static bool IsVisibleTopLevelWindows(IntPtr hwnd) {
            if (Native.IsWindowVisible(hwnd) &&
                !IsCloakedWindow(hwnd) && !IsTransparentWindow(hwnd) &&
                !Native.IsIconic(hwnd) &&
                !IsToolWindow(hwnd) &&
                // Check the window does not have WS_EX_NOACTIVATE (or if it does, it has WS_EX_APPWINDOW)
                (!IsNoActivateWindow(hwnd) || IsAppWindow(hwnd)) &&
                Native.GetWindowRect(hwnd, out _) != 0 &&
                Native.GetWindowTextLength(hwnd) != 0 &&
                IsTopLevelWindow(hwnd))
                return true;

            return false;
        }

        public static bool IsCloakedWindow(IntPtr hwnd) {
            Native.DwmGetWindowAttribute(hwnd, (int)Native.DWMWINDOWATTRIBUTE.Cloaked, out int cloakedVal, sizeof(int));
            return cloakedVal != 0;
        }

        public static bool IsToolWindow(IntPtr hwnd) {
            int exStyle = GetExtendedWindowStyle(hwnd);
            return HasFlag(exStyle, Native.WindowStyles.WS_EX_TOOLWINDOW);
        }

        public static bool IsAppWindow(IntPtr hwnd) {
            int exStyle = GetExtendedWindowStyle(hwnd);
            return HasFlag(exStyle, Native.WindowStyles.WS_EX_APPWINDOW);
        }

        public static bool IsNoActivateWindow(IntPtr hwnd) {
            int exStyle = GetExtendedWindowStyle(hwnd);
            return HasFlag(exStyle, Native.WindowStyles.WS_EX_NOACTIVATE);
        }

        private static bool IsWindowCoveringTarget(Native.RECT windowRect, Rectangle targetArea, double threshold) {
            int left = Math.Max(windowRect.Left, targetArea.Left);
            int top = Math.Max(windowRect.Top, targetArea.Top);
            int right = Math.Min(windowRect.Right, targetArea.Right);
            int bottom = Math.Min(windowRect.Bottom, targetArea.Bottom);

            if (!(right >= left && bottom >= top))
                return false;

            int intersectionWidth = Math.Max(0, right - left);
            int intersectionHeight = Math.Max(0, bottom - top);
            long intersectionArea = (long)intersectionWidth * intersectionHeight;

            var targetAreaSize = (long)(targetArea.Width * targetArea.Height);
            if (targetAreaSize <= 0)
                return false;

            double coverageRatio = intersectionArea / (double)targetAreaSize;
            return coverageRatio >= threshold;
        }

        public static bool IsWindowCoveringTarget(Rectangle windowRect, Rectangle targetArea, double threshold) {
            var intersection = Rectangle.Intersect(windowRect, targetArea);
            var ratio = (intersection.Width * intersection.Height) / (double)(targetArea.Width * targetArea.Height);
            return ratio >= threshold;
        }

        public static bool IsWindowCoveringTarget(IntPtr windowHwnd, Rectangle targetArea, double threshold) {
            if (Native.GetWindowRect(windowHwnd, out var windowRect) != 0)
                return IsWindowCoveringTarget(ToRectangle(windowRect), targetArea, threshold);
            return false;

        }

        public static bool IsTopLevelWindow(IntPtr hWnd) {
            return Native.GetAncestor(hWnd, Native.GetAncestorFlags.GetRoot) == hWnd;
        }

        public static bool IsUWPApp(IntPtr hwnd) {
            return HasClass(hwnd, "ApplicationFrameWindow");
        }

        public static bool IsTransparentWindow(IntPtr hwnd) {
            var exStyle = Native.GetWindowLong(hwnd, (int)Native.GWL.GWL_EXSTYLE);
            bool isLayered = (exStyle & Native.WindowStyles.WS_EX_LAYERED) != 0;
            bool isTransparent = (exStyle & Native.WindowStyles.WS_EX_TRANSPARENT) != 0;
            return isLayered || isTransparent;
        }

        public static bool HasClass(IntPtr hwnd, string expectedClassName) {
            if (hwnd == IntPtr.Zero)
                return false;

            const int maxChars = 256;
            var className = new StringBuilder(maxChars);
            return Native.GetClassName((int)hwnd, className, maxChars) > 0 &&
                className.ToString().Equals(expectedClassName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasFlag(int value, uint flag) {
            return (value & flag) == flag;
        }

        static Rectangle ToRectangle(Native.RECT rect) {
            return new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        private static int GetExtendedWindowStyle(IntPtr hWnd) {
            IntPtr exStylePtr = Native.GetWindowLongPtr(hWnd, (int)Native.GWL.GWL_EXSTYLE);
            return (int)(long)exStylePtr;
        }
    }

    public static class WindowClassExclusions {
        public static HashSet<string> DesktopClasses => new(StringComparer.OrdinalIgnoreCase) {
            // Startmeu, taskview (win10), action center etc
            "Windows.UI.Core.CoreWindow",
            // Alt+tab screen (win10)
            "MultitaskingViewFrame",
            // Taskview (win11)
            "XamlExplorerHostIslandWindow",
            // Widget window (win11)
            "WindowsDashboard",
            // Taskbar(s)
            "Shell_TrayWnd",
            "Shell_SecondaryTrayWnd",
            // Systray notifyicon expanded popup
            "NotifyIconOverflowWindow",
            // Rainmeter widgets
            "RainmeterMeterWindow",
            // Coodesker, ref: https://github.com/rocksdanister/lively/issues/760
            "_cls_desk_"
        };
    }
}
