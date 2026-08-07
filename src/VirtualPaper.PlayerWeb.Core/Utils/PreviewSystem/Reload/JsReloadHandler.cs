using System;
using System.IO;
using VirtualPaper.PlayerWeb.Core.Utils.PreviewSystem.Reload.Interfaces;

namespace VirtualPaper.PlayerWeb.Core.Utils.PreviewSystem.Reload {
    /// <summary>
    /// Hot-reloads JavaScript by sending a postMessage into the content
    /// iframe.  The iframe's hotreload.js removes the old &lt;script&gt;
    /// element and creates a new one with a cache-busted src, causing the
    /// script to be re-fetched and re-executed.
    /// </summary>
    public class JsReloadHandler : IPreviewReloadHandler {
        public bool CanHandle(string path) {
            return Path.GetExtension(path).Equals(".js", StringComparison.OrdinalIgnoreCase);
        }

        public void Reload(string path, IWebViewBridge bridge) {
            bridge.PostMessageToIframe("reloadJs", path);
        }
    }
}
