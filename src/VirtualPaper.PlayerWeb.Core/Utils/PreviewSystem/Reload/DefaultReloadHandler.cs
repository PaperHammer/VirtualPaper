using VirtualPaper.PlayerWeb.Core.Utils.PreviewSystem.Reload.Interfaces;

namespace VirtualPaper.PlayerWeb.Core.Utils.PreviewSystem.Reload {
    /// <summary>
    /// Catch-all handler for file types that don't have a dedicated handler
    /// (images, JSON, shaders, etc.).  Sends a generic "reloadResource"
    /// message; extend hotreload.js to handle new types as needed.
    /// </summary>
    public class DefaultReloadHandler : IPreviewReloadHandler {
        public bool CanHandle(string path) {
            // Always returns true — must be registered LAST in PreviewManager.
            return true;
        }

        public void Reload(string path, IWebViewBridge bridge) {
            bridge.PostMessageToIframe("reloadResource", path);
        }
    }
}
