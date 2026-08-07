using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtualPaper.PlayerWeb.Core.Utils.PreviewSystem.Reload;
using VirtualPaper.PlayerWeb.Core.Utils.PreviewSystem.Reload.Interfaces;
using VirtualPaper.PlayerWeb.Core.Utils.PreviewSystem.Server;

namespace VirtualPaper.PlayerWeb.Core.Utils.PreviewSystem {
    public class PreviewManager {
        public PreviewManager(IWebViewBridge bridge, PreviewConfig? config = null) {
            this._bridge = bridge;
            this._config = config ?? new PreviewConfig();
            this._server = new PreviewServer();
            _handlers.Add(new HtmlReloadHandler());
            _handlers.Add(new CssReloadHandler());
            _handlers.Add(new JsReloadHandler());
            _handlers.Add(new DefaultReloadHandler());
        }

        public async Task OpenPreviewAsync(string projectRoot, string entry) {
            await _server.StartAsync(projectRoot);
            var url = _server.GetUrl(entry);
            _bridge.Navigate(url);
        }

        /// <summary>
        /// Starts the PreviewServer and returns the HTTP URL for the given
        /// entry file, without navigating the WebView2.  The caller should
        /// pass the URL to JavaScript (e.g. as iframe src).
        /// </summary>
        public async Task<string> StartServerAsync(string projectRoot, string entry) {
            await _server.StartAsync(projectRoot);
            return _server.GetUrl(entry);
        }

        public void OnFileChanged(string path) {
            if (!_config.IsPreviewFile(path))
                return;

            var handler = _handlers.FirstOrDefault(x => x.CanHandle(path));
            handler?.Reload(path, _bridge);
        }

        public void AddHandler(IPreviewReloadHandler handler) {
            _handlers.Insert(0, handler);
        }

        public async Task DisposeAsync() {
            await _server.StopAsync();
        }

        private readonly IWebViewBridge _bridge;
        private readonly PreviewServer _server;
        private readonly PreviewConfig _config;
        private readonly List<IPreviewReloadHandler> _handlers = [];
    }
}
