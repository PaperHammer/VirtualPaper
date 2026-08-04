using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace VirtualPaper.PlayerWeb.Core.Utils.PreviewSystem {
    /// <summary>
    /// Abstraction over WebView2 so handlers can trigger reloads without
    /// depending on the concrete WebView2 control.
    /// </summary>
    public interface IWebViewBridge {
        void Navigate(string url);

        /// <summary>Full page reload (used by HTML changes).</summary>
        void ReloadPage();

        /// <summary>Execute arbitrary script in the shell page context.</summary>
        void ExecuteScript(string script);

        /// <summary>
        /// Send an HMR command to the content iframe via postMessage.
        /// The iframe must have hotreload.js loaded to handle the message.
        /// </summary>
        /// <param name="type">Message type: reloadCss, reloadJs, reloadResource, reloadPage</param>
        /// <param name="path">Relative path of the changed file (e.g. "css/style.css")</param>
        void PostMessageToIframe(string type, string path);
    }

    public class WebView2Bridge : IWebViewBridge {
        public WebView2Bridge(CoreWebView2 webView) {
            _webView = webView;
        }

        public void Navigate(string url) {
            _webView.Navigate(url);
        }

        public void ReloadPage() {
            _webView.Reload();
        }

        public void ExecuteScript(string script) {
            _ = _webView.ExecuteScriptAsync(script);
        }

        /// <summary>
        /// Sends an HMR message to the shell page via WebView2's native
        /// postMessage.  The shell page has a listener (injected in
        /// <see cref="InitHmrBridge"/>) that forwards it as a postMessage
        /// to all iframes.
        /// </summary>
        public void PostMessageToIframe(string type, string path) {
            var msg = System.Text.Json.JsonSerializer.Serialize(new {
                source = "virtualpaper-hmr",
                type,
                path = path.Replace('\\', '/')
            });
            _webView.PostWebMessageAsJson(msg);
        }

        /// <summary>
        /// Injects a script into the shell page that listens for WebView2
        /// messages and forwards them as postMessage to all iframes.
        /// Must be called once after the page is loaded.
        /// </summary>
        public void InitHmrBridge() {
            _webView.AddScriptToExecuteOnDocumentCreatedAsync(@"
window.chrome.webview.addEventListener('message', function(e) {
    try {
        var msg = JSON.parse(e.data);
        if (msg.source !== 'virtualpaper-hmr') return;
        var iframes = document.querySelectorAll('iframe');
        for (var i = 0; i < iframes.length; i++) {
            try { iframes[i].contentWindow.postMessage(msg, '*'); } catch(e) {}
        }
    } catch(e) {}
});
");
        }

        private readonly CoreWebView2 _webView;
    }
}
