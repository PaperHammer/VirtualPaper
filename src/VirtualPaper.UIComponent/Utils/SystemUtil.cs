using System;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using VirtualPaper.Common.Utils.PInvoke;
using WinRT.Interop;

namespace VirtualPaper.UIComponent.Utils {
    public static class SystemUtil {
        public static double GetScaleAdjustment(object window) {
            DisplayArea displayArea = GetDisplayArea(window, DisplayAreaFallback.Primary);

            // Get DPI.
            int result = Native.GetDpiForMonitor(
                Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId), Native.Monitor_DPI_Type.MDT_Default, out uint dpiX, out uint _);
            if (result != 0) {
                throw new Exception("Could not get DPI for monitor.");
            }

            uint scaleFactorPercent = (uint)(((long)dpiX * 100 + (96 >> 1)) / 96);
            return scaleFactorPercent / 100.0;
        }

        public static DisplayArea GetDisplayArea(object window, DisplayAreaFallback displayAreaFallback) {
            return DisplayArea.GetFromWindowId(
                    Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(window)), displayAreaFallback);
        }
    }
}
