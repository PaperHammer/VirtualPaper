namespace VirtualPaper.PlayerWeb.Core.Utils.PreviewSystem.Reload.Interfaces {
    public interface IPreviewReloadHandler {
        bool CanHandle(string path);

        void Reload(string path, IWebViewBridge bridge);

    }
}
