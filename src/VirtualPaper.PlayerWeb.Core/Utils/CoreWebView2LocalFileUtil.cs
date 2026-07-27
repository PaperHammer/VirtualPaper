using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Web.WebView2.Core;

namespace VirtualPaper.PlayerWeb.Core.Utils {
    public static class CoreWebView2LocalFileUtil {
        public static string MapLocalFile(this CoreWebView2 coreWebView2, string filePath, CoreWebView2HostResourceAccessKind accessKind = CoreWebView2HostResourceAccessKind.DenyCors) {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            var directoryPath = Path.GetDirectoryName(filePath) ?? throw new ArgumentException($"File path has no directory: {filePath}", nameof(filePath));
            var hostName = $"{GetStableHostName(directoryPath)}.localhost";
            coreWebView2.SetVirtualHostNameToFolderMapping(hostName, directoryPath, accessKind);
            return hostName;
        }

        public static string NavigateToLocalFile(this CoreWebView2 coreWebView2, string filePath, CoreWebView2HostResourceAccessKind accessKind = CoreWebView2HostResourceAccessKind.DenyCors) {
            var hostName = coreWebView2.MapLocalFile(filePath, accessKind);
            var fileName = Path.GetFileName(filePath);
            var uri = $"https://{hostName}/{fileName}";
            coreWebView2.Navigate(uri);
            return uri;
        }

        public static string GetStableHostName(string path) {
            var normalizedPath = Path.GetFullPath(path);
            var bytes = Encoding.UTF8.GetBytes(normalizedPath);
            var hash = SHA1.HashData(bytes);
            return BitConverter.ToString(hash, 0, 8).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
