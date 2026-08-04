using System;
using System.IO;
using VirtualPaper.PlayerWeb.Core.Utils.PreviewSystem.Reload.Interfaces;

namespace VirtualPaper.PlayerWeb.Core.Utils.PreviewSystem.Reload {
    /// <summary>
    /// HTML changes require a full page reload because the DOM structure
    /// may have changed in ways that cannot be surgically patched.
    /// </summary>
    public class HtmlReloadHandler : IPreviewReloadHandler {
        public bool CanHandle(string path) {
            return Path.GetExtension(path).Equals(".html", StringComparison.OrdinalIgnoreCase)
                || Path.GetExtension(path).Equals(".htm", StringComparison.OrdinalIgnoreCase);
        }

        public void Reload(string path, IWebViewBridge bridge) {
            // HTML change → tell the iframe to reload itself.
            bridge.PostMessageToIframe("reloadPage", path);
        }
    }
}
