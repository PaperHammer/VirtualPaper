using System.Security.Cryptography;
using System.Text;
using Microsoft.Web.WebView2.Core;
using VirtualPaper.Common;
using VirtualPaper.Common.Utils.PInvoke;
using WebView = Microsoft.Web.WebView2.WinForms.WebView2;

namespace VirtualPaper.PlayerWeb.Extensions {
    public static class CoreWebView2Extensions {
        public static string WallpaperHost => "wallpaper.localhost";

        public static void MapWallpaperVirtualHost(this WebView webView) {
            string folder = Constants.CommonPaths.LibraryDir;
            var hostName = $"{GetStableHostName(folder)}.localhost";
            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                hostName,
                folder,
                CoreWebView2HostResourceAccessKind.Allow
            );
        }

        public static void NavigateToLocalPath(this WebView webView, string filePath) {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var fileName = Path.GetFileName(filePath);
            // Hex format to creates valid hostname and prevent cache conflicts between folders.
            // Append `.localhost` to trigger immediate NXDOMAIN, bypassing DNS delay in WebView2.
            // Issue: https://github.com/MicrosoftEdge/WebView2Feedback/issues/2381
            var hostName = $"{GetStableHostName(filePath)}.localhost";
            var directoryPath = Path.GetDirectoryName(filePath);
            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                hostName,
                directoryPath,
                CoreWebView2HostResourceAccessKind.Allow);
            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                WallpaperHost,
                Path.Combine(Constants.CommonPaths.LibraryDir, Constants.FolderName.WpStoreFolderName),
                CoreWebView2HostResourceAccessKind.Allow);

            webView.CoreWebView2.Navigate($"https://{hostName}/{fileName}");
        }

        /// <summary>
        /// Generates a stable, unique, and hostname-safe identifier for a given file's directory.
        /// </summary>
        public static string GetStableHostName(string filePath) {
            var folderPath = Path.GetDirectoryName(filePath) ?? filePath;
            var bytes = Encoding.UTF8.GetBytes(folderPath);
            var hash = SHA1.HashData(bytes);

            return BitConverter.ToString(hash, 0, 8).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// This should be called after the WebView has finished initializing (e.g. after NavigationCompleted),
        /// otherwise the internal Chromium window may not yet exist.
        /// </summary>
        public static bool TryGetCefD3DRenderingSubProcessId(this WebView webView, out int cefD3DRenderingSubProcessId) {
            cefD3DRenderingSubProcessId = 0;
            if (!TryGetIntermediateD3DWindow(webView, out IntPtr intermediateD3DWindow))
                return false;

            var result = Native.GetWindowThreadProcessId(intermediateD3DWindow, out cefD3DRenderingSubProcessId);
            return result != 0;
        }

        /// <summary>
        /// This should be called after the WebView has finished initializing (e.g. after NavigationCompleted),
        /// otherwise the internal Chromium window may not yet exist.
        /// </summary>
        public static bool TryGetIntermediateD3DWindow(this WebView webView, out IntPtr intermediateD3DWindow) {
            intermediateD3DWindow = IntPtr.Zero;
            if (!TryGetChrome_WidgetWin_0(webView, out IntPtr chrome_WidgetWin_0))
                return false;

            var chrome_WidgetWin_1 = Native.FindWindowEx(chrome_WidgetWin_0, IntPtr.Zero, "Chrome_WidgetWin_1", null);
            if (chrome_WidgetWin_1 == IntPtr.Zero)
                return false;

            intermediateD3DWindow = Native.FindWindowEx(chrome_WidgetWin_1, IntPtr.Zero, "Intermediate D3D Window", null);
            return intermediateD3DWindow != IntPtr.Zero;
        }

        /// <summary>
        /// This should be called after the WebView has finished initializing (e.g. after NavigationCompleted),
        /// otherwise the internal Chromium window may not yet exist.
        /// </summary>
        public static bool TryGetChrome_WidgetWin_0(this WebView webView, out IntPtr chrome_WidgetWin_0) {
            // WindowsForms10.Window.8.app.0.141b42a_r9_ad1
            chrome_WidgetWin_0 = Native.FindWindowEx(webView.Handle, IntPtr.Zero, "Chrome_WidgetWin_0", null);
            return chrome_WidgetWin_0 != IntPtr.Zero;
        }
    }
}
