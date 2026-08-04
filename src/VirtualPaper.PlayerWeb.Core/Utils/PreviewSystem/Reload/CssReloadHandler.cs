using System;
using System.IO;
using VirtualPaper.PlayerWeb.Core.Utils.PreviewSystem.Reload.Interfaces;

namespace VirtualPaper.PlayerWeb.Core.Utils.PreviewSystem.Reload {
    /// <summary>
    /// Hot-reloads CSS by sending a postMessage into the content iframe.
    /// The iframe's hotreload.js finds the matching &lt;link&gt; element
    /// and updates its href with a cache-busting query parameter, causing
    /// the browser to re-fetch and re-apply the stylesheet instantly.
    /// </summary>
    public class CssReloadHandler : IPreviewReloadHandler {
        public bool CanHandle(string path) {
            return Path.GetExtension(path).Equals(".css", StringComparison.OrdinalIgnoreCase);
        }

        public void Reload(string path, IWebViewBridge bridge) {
            bridge.PostMessageToIframe("reloadCss", path);
        }
    }
}
