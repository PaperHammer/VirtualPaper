namespace VirtualPaper.PlayerWeb.Core.Utils.PreviewSystem.Server {
    public class PreviewServerOptions {
        /// <summary>
        /// 默认监听地址
        /// </summary>
        public string Host { get; set; }  = "127.0.0.1";

        /// <summary>
        /// 0表示自动分配端口
        /// </summary>
        public int Port { get; set; } = 0;

        /// <summary>
        /// If set, the script is injected into every HTML response
        /// before &lt;/body&gt;.  Used for HMR hotreload.js injection.
        /// </summary>
        public string? InjectionScript { get; set; }

        /// <summary>
        /// Optional origin allowed to load resources from this server.
        /// This is used by the 3D preview because WebGL textures are served
        /// from a separate loopback port.
        /// </summary>
        public string? AllowedOrigin { get; set; }
    }
}
