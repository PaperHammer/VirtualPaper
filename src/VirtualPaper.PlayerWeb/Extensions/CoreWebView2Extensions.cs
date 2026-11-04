using System.Security.Cryptography;
using System.Text;
using Microsoft.Web.WebView2.Core;
using VirtualPaper.Common;
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
                CoreWebView2HostResourceAccessKind.DenyCors
            );
        }

        public static void NavigateToLocalPath(this WebView webView, string filePath, string? textureFolder = null,
            string? wallpaperFolder = null) {
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
                CoreWebView2HostResourceAccessKind.DenyCors);
            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                WallpaperHost,
                wallpaperFolder ?? Path.Combine(Constants.CommonPaths.LibraryDir, Constants.FolderName.WpStoreFolderName),
                CoreWebView2HostResourceAccessKind.DenyCors);

            // Mapping changes take effect on the next navigation, before any texture requests.
            if (textureFolder != null) {
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping("texture.wallpaper.localhost",
                    textureFolder, CoreWebView2HostResourceAccessKind.Allow);
            }

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

    }
}
