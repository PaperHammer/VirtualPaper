using System.Runtime.InteropServices;
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
    }
}
