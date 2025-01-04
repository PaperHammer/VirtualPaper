﻿using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using VirtualPaper.Common.Utils.PInvoke;
using WinRT;

namespace VirtualPaper.UIComponent.Utils.Extensions
{
    //Note: Don't prefer extensions, remove when not required; suffix all methods with Ex.
    public static class WindowExtensions
    {
        public static void SetIconEx(this Window window, string iconName)
        {
            //Issue: https://github.com/microsoft/microsoft-ui-xaml/issues/4056
            LoadIcon(iconName, window);
        }

        public static void SetWindowSizeEx(this Window window, int width, int height)
        {
            //Issue: https://github.com/microsoft/microsoft-ui-xaml/issues/6353
            //IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(m_window);
            //var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            //var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            //appWindow.Resize(new Windows.Graphics.SizeInt32(1200, 720));
            //SetWindowSize(m_windowHandle, 875, 875);

            var hwnd = window.GetWindowHandleEx();
            var dpi = Native.GetDpiForWindow(hwnd);
            float scalingFactor = (float)dpi / 96;
            width = (int)(width * scalingFactor);
            height = (int)(height * scalingFactor);

            Native.SetWindowPos(hwnd, 0, 0, 0, width, height, (int)Native.SetWindowPosFlags.SWP_NOMOVE);
        }

        public static nint GetWindowHandleEx(this Window window)
        {
            var windowNative = window.As<IWindowNative>();
            return windowNative.WindowHandle;
        }

        //References:
        //https://github.com/microsoft/WindowsAppSDK/issues/41
        //https://docs.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute
        public static bool UseImmersiveDarkModeEx(this Window window, bool enabled)
        {
            var status = false;
            if (IsWindows10OrGreater(17763))
            {
                var hwnd = window.GetWindowHandleEx();
                int useImmersiveDarkMode = enabled ? 1 : 0;
                var attribute = IsWindows10OrGreater(18985) ? Native.DWMWA_USE_IMMERSIVE_DARK_MODE : Native.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
                status = Native.DwmSetWindowAttribute(hwnd, attribute, ref useImmersiveDarkMode, sizeof(int)) == 0;

                Native.ShowWindow(hwnd, (uint)Native.SHOWWINDOW.SW_HIDE);
                Native.ShowWindow(hwnd, (uint)Native.SHOWWINDOW.SW_SHOW);
                /*
                NativeMethods.SendMessage(hwnd, (int)NativeMethods.WM.NCPAINT, IntPtr.Zero, IntPtr.Zero);
                NativeMethods.SetWindowPos(hwnd, 0, 0, 0, 0, 0, 
                    (int)(NativeMethods.SetWindowPosFlags.SWP_DRAWFRAME | 
                    NativeMethods.SetWindowPosFlags.SWP_NOACTIVATE | 
                    NativeMethods.SetWindowPosFlags.SWP_NOMOVE | 
                    NativeMethods.SetWindowPosFlags.SWP_NOSIZE | 
                    NativeMethods.SetWindowPosFlags.SWP_NOZORDER));
                */
            }
            return status;
        }

        #region helpers

        private const int IMAGE_ICON = 1;
        private const int LR_LOADFROMFILE = 0x0010;

        private static void LoadIcon(string iconName, Window window)
        {
            //Get the Window's HWND
            var hwnd = window.As<IWindowNative>().WindowHandle;
            nint hIcon = Native.LoadImage(nint.Zero, iconName,
                      IMAGE_ICON, 32, 32, LR_LOADFROMFILE);

            _ = Native.SendMessage(hwnd, (int)Native.WM.SETICON, 0, hIcon);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("EECDBF0E-BAE9-4CB6-A68E-9598E1CB57BB")]
        internal interface IWindowNative
        {
            nint WindowHandle { get; }
        }

        private static bool IsWindows10OrGreater(int build = -1)
        {
            return Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= build;
        }

        #endregion
    }
}
